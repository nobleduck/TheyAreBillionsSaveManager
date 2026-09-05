using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace BillionsSaveManager
{
    public class SaveStore
    {
        public readonly string SaveDirectory;
        public readonly string ArchiveDirectory;
        private readonly object gate = new object();
        public string LastRecoveryDirectory { get; private set; }

        public SaveStore(string saves, string archives)
        {
            SaveDirectory = Normalize(saves); ArchiveDirectory = Normalize(archives);
            if (Inside(SaveDirectory, ArchiveDirectory) || Inside(ArchiveDirectory, SaveDirectory))
                throw new LocalizedInvalidOperationException("存档目录与快照目录不能相同或互相包含。");
            NoLinks(SaveDirectory); NoLinks(ArchiveDirectory);
            if (!Directory.Exists(SaveDirectory)) throw new LocalizedDirectoryNotFoundException("找不到游戏存档目录：{0}", SaveDirectory);
            Directory.CreateDirectory(ArchiveDirectory);
        }

        public List<SavePair> ScanLocalPairs()
        {
            lock (gate)
            {
                NoLinks(SaveDirectory);
                var result = new List<SavePair>();
                foreach (string file in Directory.GetFiles(SaveDirectory, "*.zxsav"))
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    if (!File.Exists(Path.Combine(SaveDirectory, stem + ".zxcheck"))) continue;
                    try
                    {
                        ValidateName(stem); NoLinks(file); NoLinks(Path.Combine(SaveDirectory, stem + ".zxcheck"));
                        var info = new FileInfo(file);
                        if (info.Length == 0) continue;
                        string name = stem.EndsWith("_Backup", StringComparison.Ordinal) ? stem.Substring(0, stem.Length - 7) : stem;
                        ValidateName(name);
                        result.Add(new SavePair { SaveName = name, FileStem = stem, Folder = SaveDirectory, SavedAtUtc = info.LastWriteTimeUtc, Length = info.Length });
                    }
                    catch (IOException) { }
                    catch (InvalidOperationException) { }
                }
                return result.OrderByDescending(p => p.SavedAtUtc).ToList();
            }
        }

        public Snapshot Capture(string name, string reason, bool deduplicate)
        {
            lock (gate) { return CaptureInternal(name, reason, deduplicate, false); }
        }

        private Snapshot CaptureInternal(string name, string reason, bool deduplicate, bool allowEmpty)
        {
            ValidateName(name); NoLinks(SaveDirectory); NoLinks(ArchiveDirectory);
            var content = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var records = new List<FileRecord>();
            foreach (string stem in new[] { name, name + "_Backup" })
            {
                string data = Path.Combine(SaveDirectory, stem + ".zxsav"), check = Path.Combine(SaveDirectory, stem + ".zxcheck");
                if (!File.Exists(data) && !File.Exists(check)) continue;
                if (!File.Exists(data) || !File.Exists(check)) throw new LocalizedIOException("存档配对不完整，等待游戏完成保存后重试：{0}", stem);
                DateTime dataTime = File.GetLastWriteTimeUtc(data), checkTime = File.GetLastWriteTimeUtc(check);
                if (Math.Abs((dataTime - checkTime).TotalSeconds) > 5 || DateTime.UtcNow - (dataTime > checkTime ? dataTime : checkTime) < TimeSpan.FromSeconds(1))
                    throw new LocalizedIOException("存档仍在保存，或校验文件时间不匹配，请稍后重试：{0}", stem);
                var first = ReadStable(data); var second = ReadStable(check);
                // Re-read both after copying: never hold a lock that prevents the game from saving.
                if (HashFile(data) != first.Record.Sha256 || HashFile(check) != second.Record.Sha256 || File.GetLastWriteTimeUtc(data) != dataTime || File.GetLastWriteTimeUtc(check) != checkTime)
                    throw new LocalizedIOException("复制期间存档发生变化，本次快照已取消。");
                records.Add(first.Record); records.Add(second.Record);
                content.Add(first.Record.Name, first.Bytes); content.Add(second.Record.Name, second.Bytes);
            }
            if (!allowEmpty && records.Count == 0) throw new LocalizedIOException("没有可备份的完整存档。");
            if (deduplicate)
            {
                foreach (var previous in GetSnapshots().Where(s => s.SaveName == name))
                {
                    if (Signature(previous.Files) != Signature(records)) continue;
                    try { Verify(previous); return previous; } catch (IOException) { }
                }
            }
            string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
            string pending = Path.Combine(ArchiveDirectory, id + ".pending");
            Directory.CreateDirectory(pending);
            var snapshot = new Snapshot { Id = id, SaveName = name, Reason = reason, CreatedUtc = DateTime.UtcNow, Files = records, Folder = pending };
            foreach (var record in records)
            {
                string destination = Path.Combine(pending, record.Name);
                using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(content[record.Name], 0, content[record.Name].Length); stream.Flush(true); }
                File.SetLastWriteTimeUtc(destination, record.LastWriteUtc);
                if (HashFile(destination) != record.Sha256) throw new LocalizedIOException("快照校验失败，尚未修改游戏存档。");
            }
            WriteJson(Path.Combine(pending, "snapshot.json"), snapshot);
            string completed = Path.Combine(ArchiveDirectory, id);
            Directory.Move(pending, completed); snapshot.Folder = completed;
            return snapshot;
        }

        public List<Snapshot> GetSnapshots()
        {
            lock (gate)
            {
                NoLinks(ArchiveDirectory);
                var result = new List<Snapshot>();
                foreach (var directory in Directory.GetDirectories(ArchiveDirectory))
                {
                    if (directory.EndsWith(".pending", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!File.Exists(Path.Combine(directory, "snapshot.json"))) continue;
                    try { result.Add(Load(Path.GetFileName(directory))); }
                    catch (IOException) { }
                    catch (InvalidOperationException) { }
                    catch (SerializationException) { }
                }
                return result.OrderByDescending(s => s.CreatedUtc).ToList();
            }
        }

        public List<SavePair> GetPairs(Snapshot snapshot)
        {
            lock (gate)
            {
                var loaded = Load(snapshot.Id);
                var result = new List<SavePair>();
                foreach (var record in loaded.Files.Where(f => f.Name.EndsWith(".zxsav", StringComparison.Ordinal)))
                {
                    string stem = Path.GetFileNameWithoutExtension(record.Name);
                    if (!loaded.Files.Any(f => f.Name == stem + ".zxcheck")) continue;
                    result.Add(new SavePair { SaveName = loaded.SaveName, FileStem = stem, Folder = loaded.Folder, SnapshotId = loaded.Id, SavedAtUtc = record.LastWriteUtc, Length = record.Length });
                }
                return result;
            }
        }

        public void Restore(SavePair target, Func<bool> guard)
        {
            lock (gate)
            {
                if (target == null || string.IsNullOrEmpty(target.SnapshotId)) throw new LocalizedInvalidOperationException("恢复目标必须是已校验的历史快照。");
                var source = Load(target.SnapshotId); Verify(source);
                var selected = GetPairs(source).SingleOrDefault(p => p.FileStem == target.FileStem && p.SaveName == target.SaveName);
                if (selected == null) throw new LocalizedInvalidOperationException("目标不在快照清单中。");
                RequireMenu(guard);
                var before = CaptureInternal(selected.SaveName, "before-restore", false, true);
                string recoveryParent = Path.Combine(Directory.GetParent(SaveDirectory).FullName, "TABSaveManager-Recovery");
                NoLinks(recoveryParent);
                string recovery = Path.Combine(recoveryParent, Guid.NewGuid().ToString("N"));
                string held = Path.Combine(recovery, "originals"), staged = Path.Combine(recovery, "staged");
                Directory.CreateDirectory(held); Directory.CreateDirectory(staged);
                LastRecoveryDirectory = recovery;
                string[] selectedNames = { selected.FileStem + ".zxsav", selected.FileStem + ".zxcheck" };
                foreach (string name in selectedNames)
                {
                    string output = Path.Combine(staged, name);
                    NoLinks(Path.Combine(source.Folder,name)); NoLinks(output);
                    File.Copy(Path.Combine(source.Folder, name), output, false);
                    if (HashFile(output) != source.Files.Single(f => f.Name == name).Sha256) throw new LocalizedIOException("恢复暂存文件校验失败。");
                }
                var journal = new RecoveryJournal { State = "Prepared", SaveDirectory = SaveDirectory, BeforeSnapshot = before.Id, TargetSnapshot = source.Id, TargetStem = selected.FileStem };
                string journalPath = Path.Combine(recovery, "recovery.json");
                WriteJson(journalPath, journal);
                var moved = new List<string>(); var placed = new List<string>();
                try
                {
                    // Validate the complete preimage again before the first move.
                    foreach (string name in ExpectedNames(selected.SaveName))
                    {
                        string live = Path.Combine(SaveDirectory, name); NoLinks(live);
                        var expected = before.Files.SingleOrDefault(f => f.Name == name);
                        if (File.Exists(live) != (expected != null) || (expected != null && HashFile(live) != expected.Sha256))
                            throw new LocalizedIOException("主菜单期间存档发生变化，本次回退已停止。");
                    }
                    foreach (var record in before.Files)
                    {
                        RequireMenu(guard); NoLinks(SaveDirectory);
                        string live = Path.Combine(SaveDirectory, record.Name); NoLinks(live);
                        if (HashFile(live) != record.Sha256) throw new LocalizedIOException("即将移动的存档发生变化。");
                        MoveChecked(live, Path.Combine(held, record.Name)); moved.Add(record.Name);
                        if (HashFile(Path.Combine(held, record.Name)) != record.Sha256) throw new LocalizedIOException("移动期间原件发生变化。");
                        journal.State = "MovingOriginals"; WriteJson(journalPath, journal);
                    }
                    foreach (string name in selectedNames)
                    {
                        RequireMenu(guard); NoLinks(SaveDirectory);
                        MoveChecked(Path.Combine(staged, name), Path.Combine(SaveDirectory, name)); placed.Add(name);
                        journal.State = "InstallingTarget"; WriteJson(journalPath, journal);
                    }
                    RequireMenu(guard);
                    foreach (string name in selectedNames)
                        if (HashFile(Path.Combine(SaveDirectory, name)) != source.Files.Single(f => f.Name == name).Sha256) throw new LocalizedIOException("恢复结果校验失败。");
                    VerifyActiveTarget(selected);
                    journal.State = "AwaitingGameLoad"; WriteJson(journalPath, journal);
                }
                catch (Exception error)
                {
                    var problems = new List<object>();
                    foreach (string name in placed)
                    {
                        try
                        {
                            string live = Path.Combine(SaveDirectory, name); NoLinks(live);
                            if (File.Exists(live) && HashFile(live) == source.Files.Single(f => f.Name == name).Sha256)
                                MoveChecked(live, Path.Combine(staged, name));
                        }
                        catch (Exception e) { problems.Add(e); }
                    }
                    foreach (string name in moved)
                    {
                        try
                        {
                            string live = Path.Combine(SaveDirectory, name); NoLinks(live);
                            if (!File.Exists(live)) MoveChecked(Path.Combine(held, name), live);
                            else problems.Add(new LocalizedText("保留了外部新文件，原件仍在恢复目录：{0}", name));
                        }
                        catch (Exception e) { problems.Add(e); }
                    }
                    journal.State = problems.Count == 0 ? "RolledBack" : "NeedsRecovery";
                    try { WriteJson(journalPath, journal); } catch (Exception) { }
                    throw new LocalizedIOException(error, "{0}\n{1}\n{2}", error, new LocalizedText("已保留快照与原件：{0}", recovery), problems.Count > 0 ? (object)problems : new LocalizedText("已撤销文件操作。"));
                }
            }
        }

        public void VerifyActiveTarget(SavePair target)
        {
            lock (gate)
            {
                var snapshot = Load(target.SnapshotId);
                foreach (string name in ExpectedNames(snapshot.SaveName))
                {
                    string path = Path.Combine(SaveDirectory,name); NoLinks(path);
                    bool selected = name == target.FileStem + ".zxsav" || name == target.FileStem + ".zxcheck";
                    if (!selected && File.Exists(path)) throw new LocalizedIOException("较新存档重新出现，可能来自云同步，本次未确认成功。请退出游戏后重试。");
                    if (selected && (!File.Exists(path) || HashFile(path) != snapshot.Files.Single(f => f.Name == name).Sha256))
                        throw new LocalizedIOException("恢复目标发生变化或缺失，本次未确认成功。历史快照仍然保留。");
                }
            }
        }

        private static void MoveChecked(string source, string destination)
        {
            NoLinks(source); NoLinks(destination); File.Move(source,destination); NoLinks(destination);
        }

        private Snapshot Load(string id)
        {
            ValidateName(id); string folder = Path.Combine(ArchiveDirectory, id); NoLinks(folder);
            string manifest = Path.Combine(folder, "snapshot.json"); NoLinks(manifest);
            var snapshot = ReadJson<Snapshot>(manifest);
            if (snapshot == null || snapshot.Id != id || snapshot.Files == null) throw new LocalizedIOException("快照清单不完整。");
            ValidateName(snapshot.SaveName);
            string[] expected = ExpectedNames(snapshot.SaveName);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in snapshot.Files)
            {
                if (record == null || !expected.Contains(record.Name) || !seen.Add(record.Name) || string.IsNullOrEmpty(record.Sha256) || record.Sha256.Length != 64 || record.Length <= 0)
                    throw new LocalizedIOException("快照清单包含无效文件。");
                NoLinks(Path.Combine(folder, record.Name));
            }
            foreach (string name in seen)
            {
                string partner = Path.ChangeExtension(name, name.EndsWith(".zxsav", StringComparison.Ordinal) ? ".zxcheck" : ".zxsav");
                if (!seen.Contains(partner)) throw new LocalizedIOException("快照中的存档配对不完整。");
            }
            snapshot.Folder = folder; return snapshot;
        }

        private static void Verify(Snapshot snapshot)
        {
            foreach (var record in snapshot.Files)
            {
                string file = Path.Combine(snapshot.Folder, record.Name); NoLinks(file);
                if (!File.Exists(file) || new FileInfo(file).Length != record.Length || HashFile(file) != record.Sha256)
                    throw new LocalizedIOException("快照完整性校验失败：{0}", record.Name);
            }
        }

        private sealed class FileContent { public FileRecord Record; public byte[] Bytes; }
        private static FileContent ReadStable(string path)
        {
            NoLinks(path); var before = new FileInfo(path);
            long size = before.Length; DateTime time = before.LastWriteTimeUtc;
            if (size <= 0 || size > 256L * 1024 * 1024) throw new LocalizedIOException("存档文件为空或超过 256 MB：{0}", Path.GetFileName(path));
            byte[] bytes;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var output = new MemoryStream()) { input.CopyTo(output); bytes = output.ToArray(); }
            var after = new FileInfo(path);
            if (bytes.LongLength != size || after.Length != size || after.LastWriteTimeUtc != time) throw new LocalizedIOException("存档正在变化，请稍后重试。");
            return new FileContent { Bytes = bytes, Record = new FileRecord { Name = Path.GetFileName(path), Length = size, LastWriteUtc = time, Sha256 = Hash(bytes) } };
        }

        public static string HashFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static string Signature(IEnumerable<FileRecord> files) { return string.Join("|", files.OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => f.Name + ":" + f.Sha256).ToArray()); }
        private static string[] ExpectedNames(string name) { return new[] { name + ".zxsav", name + ".zxcheck", name + "_Backup.zxsav", name + "_Backup.zxcheck" }; }
        private static void RequireMenu(Func<bool> guard) { if (guard == null || !guard()) throw new LocalizedInvalidOperationException("游戏已离开主菜单或进入了存档列表，回退已停止。请退出游戏后重试。"); }
        private static string Normalize(string path) { if (string.IsNullOrWhiteSpace(path)) throw new LocalizedInvalidOperationException("目录不能为空。"); return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        private static bool Inside(string path, string parent) { return path.Equals(parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal))
                throw new LocalizedInvalidOperationException("无效的存档或快照名称。");
        }
        private static void NoLinks(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new LocalizedInvalidOperationException("为避免操作到其他位置，暂不支持链接目录或文件：{0}", current);
                current = Path.GetDirectoryName(current);
            }
        }

        public static T ReadJson<T>(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        public static void WriteJson<T>(string path, T value)
        {
            NoLinks(path);
            string temporary = path + ".writing-" + Guid.NewGuid().ToString("N");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); stream.Flush(true); }
            NoLinks(path); NoLinks(temporary);
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }

        [DataContract]
        private sealed class RecoveryJournal
        {
            [DataMember] public string State;
            [DataMember] public string SaveDirectory;
            [DataMember] public string BeforeSnapshot;
            [DataMember] public string TargetSnapshot;
            [DataMember] public string TargetStem;
        }
    }
}
