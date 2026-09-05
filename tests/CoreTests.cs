using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using BillionsSaveManager;

internal static class CoreTests
{
    private static int passed, failed;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected refusal."); }
    private static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); passed++; }
        catch (Exception e) { Console.WriteLine("FAIL " + name + ": " + e.Message); failed++; }
    }

    internal sealed class Fixture : IDisposable
    {
        public string Root = Path.Combine(Path.GetTempPath(), "TABSaveTests-" + Guid.NewGuid().ToString("N"));
        public SaveStore Store;
        public Fixture() { Directory.CreateDirectory(Path.Combine(Root, "Saves")); Store = new SaveStore(Path.Combine(Root,"Saves"), Path.Combine(Root,"Snapshots")); }
        public void Pair(string stem, string value)
        {
            File.WriteAllText(Path.Combine(Store.SaveDirectory, stem + ".zxsav"), value);
            File.WriteAllText(Path.Combine(Store.SaveDirectory, stem + ".zxcheck"), "check:" + value);
            DateTime time = new DateTime(2026, 9, 5, 12, 23, 41, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(Path.Combine(Store.SaveDirectory, stem + ".zxsav"), time);
            File.SetLastWriteTimeUtc(Path.Combine(Store.SaveDirectory, stem + ".zxcheck"), time);
        }
        public string Read(string stem) { return File.ReadAllText(Path.Combine(Store.SaveDirectory, stem + ".zxsav")); }
        public void Dispose()
        {
            string resolved = Path.GetFullPath(Root);
            if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("TABSaveTests-")) Directory.Delete(resolved, true);
        }
    }

    private static int Main()
    {
        Run("find complete pairs only", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony", "latest"); f.Pair("Colony_Backup", "older");
                File.WriteAllText(Path.Combine(f.Store.SaveDirectory,"Broken.zxsav"),"incomplete");
                var pairs = f.Store.ScanLocalPairs();
                Check(pairs.Count == 2 && pairs.Count(p => p.IsBackup) == 1, "Orphan was offered or backup classification is wrong.");
            }
        });
        Run("snapshot preserves previous bytes after game overwrites", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony", "before"); var snapshot = f.Store.Capture("Colony", "manual", false);
                f.Pair("Colony", "after");
                Check(File.ReadAllText(Path.Combine(snapshot.Folder,"Colony.zxsav")) == "before", "Snapshot changed with live file.");
                Check(f.Store.GetPairs(snapshot).Count == 1, "Missing snapshot pair.");
            }
        });
        Run("automatic capture deduplicates by content", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony", "same"); var first = f.Store.Capture("Colony","auto",true);
                var second = f.Store.Capture("Colony","auto",true);
                Check(first.Id == second.Id && f.Store.GetSnapshots().Count == 1, "Unchanged content created duplicate snapshots.");
                f.Pair("Colony","changed"); f.Store.Capture("Colony","auto",true);
                Check(f.Store.GetSnapshots().Count == 2,"Changed content was not captured.");
            }
        });
        Run("unsafe slot name is refused", delegate {
            using (var f = new Fixture()) { Throws(delegate { f.Store.Capture("../escape", "manual", false); }); Check(!File.Exists(Path.Combine(f.Root,"escape.zxsav")),"Escaped root."); }
        });
        Run("archive under live saves is refused", delegate {
            using (var f = new Fixture()) { Throws(delegate { new SaveStore(f.Store.SaveDirectory, Path.Combine(f.Store.SaveDirectory,"History")); }); }
        });
        Run("mismatched pair timestamps are refused", delegate {
            using (var f = new Fixture()) { f.Pair("Colony","value"); File.SetLastWriteTimeUtc(Path.Combine(f.Store.SaveDirectory,"Colony.zxcheck"),DateTime.UtcNow); Throws(delegate { f.Store.Capture("Colony","manual",false); }); }
        });
        Run("exclusively locked pair is refused", delegate {
            using (var f = new Fixture()) { f.Pair("Colony","value"); using (var writer = new FileStream(Path.Combine(f.Store.SaveDirectory,"Colony.zxsav"), FileMode.Open, FileAccess.Write, FileShare.None)) { Throws(delegate { f.Store.Capture("Colony","manual",false); }); } }
        });
        Run("restore keeps backup filename and unrelated slots", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); f.Pair("Other","untouched");
                var target = f.Store.GetPairs(f.Store.Capture("Colony","manual",false)).Single(p => p.IsBackup);
                f.Store.Restore(target, delegate { return true; });
                Check(!File.Exists(Path.Combine(f.Store.SaveDirectory,"Colony.zxsav")),"New main save still shadows backup.");
                Check(f.Read("Colony_Backup") == "old" && f.Read("Other") == "untouched","Wrong save was restored.");
                Check(f.Store.GetSnapshots().Count >= 2,"Pre-restore state was not captured.");
            }
        });
        Run("corrupt snapshot never alters live data", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); var snapshot = f.Store.Capture("Colony","manual",false);
                var target = f.Store.GetPairs(snapshot).Single(p => p.IsBackup);
                File.WriteAllText(Path.Combine(snapshot.Folder,"Colony_Backup.zxsav"),"damaged");
                Throws(delegate { f.Store.Restore(target, delegate { return true; }); });
                Check(f.Read("Colony") == "new" && f.Read("Colony_Backup") == "old","Live data changed despite failed verification.");
            }
        });
        Run("guard refusal before mutation leaves originals", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); var target = f.Store.GetPairs(f.Store.Capture("Colony","manual",false)).Single(p => p.IsBackup);
                Throws(delegate { f.Store.Restore(target, delegate { return false; }); });
                Check(f.Read("Colony") == "new" && f.Read("Colony_Backup") == "old","Guard refusal mutated saves.");
            }
        });
        Run("interrupted transaction restores removed originals", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); var target = f.Store.GetPairs(f.Store.Capture("Colony","manual",false)).Single(p => p.IsBackup);
                Throws(delegate { f.Store.Restore(target, delegate { return File.Exists(Path.Combine(f.Store.SaveDirectory,"Colony.zxsav")); }); });
                Check(f.Read("Colony") == "new" && f.Read("Colony_Backup") == "old","Partial transaction lost originals.");
            }
        });
        Run("cloud recreation during transaction cannot shadow target", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); var target = f.Store.GetPairs(f.Store.Capture("Colony","manual",false)).Single(p => p.IsBackup);
                bool recreated = false;
                Throws(delegate { f.Store.Restore(target, delegate {
                    if (!recreated && !Directory.GetFiles(f.Store.SaveDirectory,"Colony*").Any()) { f.Pair("Colony","external-new"); recreated = true; }
                    return true;
                }); });
                Check(f.Read("Colony") == "external-new","External new file was overwritten during recovery.");
            }
        });
        Run("traversal in stored manifest is refused", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); var snapshot = f.Store.Capture("Colony","manual",false); var target = f.Store.GetPairs(snapshot).Single();
                snapshot.Files[0].Name = "../escape.zxsav"; SaveStore.WriteJson(Path.Combine(snapshot.Folder,"snapshot.json"),snapshot);
                Throws(delegate { f.Store.Restore(target,delegate { return true; }); }); Check(f.Read("Colony") == "new","Malformed manifest changed live data.");
            }
        });
        Run("restores historical pair after slot disappeared", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","saved"); var target = f.Store.GetPairs(f.Store.Capture("Colony","manual",false)).Single();
                File.Delete(Path.Combine(f.Store.SaveDirectory,"Colony.zxsav")); File.Delete(Path.Combine(f.Store.SaveDirectory,"Colony.zxcheck"));
                f.Store.Restore(target,delegate { return true; }); Check(f.Read("Colony") == "saved","Lost slot could not be recovered.");
            }
        });
        Run("recovery destination junction cannot redirect originals", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); var target = f.Store.GetPairs(f.Store.Capture("Colony","manual",false)).Single(p => p.IsBackup);
                string redirected = Path.Combine(f.Root,"UnexpectedDestination"); Directory.CreateDirectory(redirected);
                string junction = null;
                try {
                    Throws(delegate { f.Store.Restore(target,delegate {
                        if (junction == null && f.Store.LastRecoveryDirectory != null) {
                            junction = Path.Combine(f.Store.LastRecoveryDirectory,"originals");
                            Check(Path.GetFullPath(junction).StartsWith(f.Root + Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Fixture recovery escaped temp root.");
                            Directory.Delete(junction);
                            string command = "New-Item -ItemType Junction -Path '" + junction.Replace("'","''") + "' -Target '" + redirected.Replace("'","''") + "' | Out-Null";
                            using (var process = Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -Command \"" + command + "\"") { UseShellExecute = false, CreateNoWindow = true })) { process.WaitForExit(); Check(process.ExitCode == 0,"Could not create fixture junction."); }
                        }
                        return true;
                    }); });
                    Check(!Directory.GetFiles(redirected).Any() && f.Read("Colony") == "new","Originals were redirected through a junction.");
                }
                finally { if (junction != null && Directory.Exists(junction) && (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0) Directory.Delete(junction); }
            }
        });
        CoordinatorTests();
        Console.WriteLine("RESULT: " + passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
    }

    private const string Menu = "20:00 - Log Start\n20:00 - ZXSystem_StartScreen - ShowSceneSuccess\n20:00 - ZXGame - ChangeScene -End\n";
    private sealed class TestHost : IGameHost
    {
        public GameObservation Current = new GameObservation();
        public Action OnLaunch;
        public GameObservation Observe() { return Current; }
        public void Launch() { if (OnLaunch != null) OnLaunch(); }
        public void MenuAt(DateTime now) { Current = new GameObservation { Running = true, ProcessId = 17, StartedUtc = now, LogLastWriteUtc = now, LogText = Menu }; }
    }

    private static void CoordinatorTests()
    {
        Run("main menu parser refuses early continue click", delegate {
            Check(GameLog.Parse(Menu).MainMenu,"Fresh menu not recognized.");
            Check(!GameLog.Parse(Menu + "20:00 - Window Show: 选择游戏进度\n").MainMenu,"Save chooser was considered safe.");
        });
        Run("loading alone is not success", delegate {
            Check(GameLog.Parse(Menu + "20:00 - Loading game: C:\\Saves\\Colony_Backup.zxsav\n").LoadedPath == null,"Load started but not finished.");
        });
        Run("cloud download happens before guarded restore", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old");
                DateTime now = DateTime.UtcNow; var host = new TestHost();
                host.OnLaunch = delegate { f.Pair("Colony","cloud-new"); host.MenuAt(now); };
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup));
                Check(f.Read("Colony") == "cloud-new","Files were moved before Steam launch.");
                coordinator.Poll();
                Check(coordinator.State == RollbackState.AwaitingLoad,"Restore was not armed after main menu.");
                Check(!File.Exists(Path.Combine(f.Store.SaveDirectory,"Colony.zxsav")) && f.Read("Colony_Backup") == "old","Cloud current file still shadows intended backup.");
                host.Current.LogText += "20:01 - Loading game: " + Path.Combine(f.Store.SaveDirectory,"Colony_Backup.zxsav") + "\n20:01 - ZXSystem_GameLevel - LoadLevel - Gamestate Loaded\n";
                coordinator.Poll(); Check(coordinator.State == RollbackState.Loaded,"Correct complete load not verified.");
            }
        });
        Run("old main-menu log cannot authorize mutation", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow;
                var host = new TestHost(); host.OnLaunch = delegate { host.MenuAt(now); host.Current.LogLastWriteUtc = now.AddMinutes(-10); };
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); coordinator.Poll();
                Check(coordinator.State != RollbackState.AwaitingLoad && f.Read("Colony") == "new","Stale log authorized moving current save.");
            }
        });
        Run("early continue click is refused without mutation", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow;
                var host = new TestHost(); host.OnLaunch = delegate { host.MenuAt(now); host.Current.LogText += "20:00 - Window Show: 选择游戏进度\n"; };
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); coordinator.Poll();
                Check(coordinator.State == RollbackState.Failed && f.Read("Colony") == "new","Early click was not refused.");
            }
        });
        Run("same filename in another directory is not accepted", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow;
                var host = new TestHost(); host.OnLaunch = delegate { host.MenuAt(now); };
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); coordinator.Poll();
                host.Current.LogText += "20:01 - Loading game: C:\\Unrelated\\Colony_Backup.zxsav\n20:01 - ZXSystem_GameLevel - LoadLevel - Gamestate Loaded\n";
                coordinator.Poll(); Check(coordinator.State != RollbackState.Loaded,"Wrong full path counted as restored.");
            }
        });
        Run("running game blocks new rollback", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow;
                var host = new TestHost(); host.MenuAt(now);
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                Throws(delegate { coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); });
                Check(f.Read("Colony") == "new","Existing session was modified.");
            }
        });
        Run("startup timeout does not alter saves", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow; var host = new TestHost();
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); now = now.AddMinutes(4); coordinator.Poll();
                Check(coordinator.State == RollbackState.Failed && f.Read("Colony") == "new","Timeout changed saves or remained busy.");
            }
        });
        Run("process replacement cannot verify target load", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow;
                var host = new TestHost(); host.OnLaunch = delegate { host.MenuAt(now); };
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); coordinator.Poll();
                host.Current.ProcessId = 33; host.Current.LogText += "20:01 - Loading game: " + Path.Combine(f.Store.SaveDirectory,"Colony_Backup.zxsav") + "\n20:01 - ZXSystem_GameLevel - LoadLevel - Gamestate Loaded\n";
                coordinator.Poll(); Check(coordinator.State == RollbackState.Failed,"Different process verified old recovery.");
            }
        });
        Run("old menu plus fresh append is not a new log session", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow;
                var host = new TestHost(); host.Current.LogText = Menu;
                host.OnLaunch = delegate { host.MenuAt(now); host.Current.LogText += "20:05 - engine initializing\n"; };
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); coordinator.Poll();
                Check(coordinator.State != RollbackState.AwaitingLoad && f.Read("Colony") == "new","Old menu accepted after unrelated append.");
            }
        });
        Run("a load before restore never produces verified success", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow;
                var host = new TestHost(); host.OnLaunch = delegate {
                    host.MenuAt(now); host.Current.LogText = "20:00 - Log Start\n20:00 - Loading game: " + Path.Combine(f.Store.SaveDirectory,"Colony_Backup.zxsav") + "\n20:00 - LoadLevel - Gamestate Loaded\n20:01 - ZXSystem_StartScreen - ShowSceneSuccess\n";
                };
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); coordinator.Poll(); coordinator.Poll();
                Check(coordinator.State == RollbackState.Failed && f.Read("Colony") == "new","A prior load was accepted and files moved after early loading.");
            }
        });
        Run("waiting for load eventually times out without overwriting files", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow;
                var host = new TestHost(); host.OnLaunch = delegate { host.MenuAt(now); };
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); coordinator.Poll(); now = now.AddMinutes(11); coordinator.Poll();
                Check(coordinator.State == RollbackState.Failed && f.Read("Colony_Backup") == "old","Load wait never terminates.");
            }
        });
        Run("modified target cannot be verified from filename alone", delegate {
            using (var f = new Fixture()) {
                f.Pair("Colony","new"); f.Pair("Colony_Backup","old"); DateTime now = DateTime.UtcNow;
                var host = new TestHost(); host.OnLaunch = delegate { host.MenuAt(now); };
                var coordinator = new RollbackCoordinator(f.Store,host,delegate { return now; });
                coordinator.Begin(f.Store.ScanLocalPairs().Single(p => p.IsBackup)); coordinator.Poll();
                f.Pair("Colony_Backup","externally-replaced");
                host.Current.LogText += "20:01 - Loading game: " + Path.Combine(f.Store.SaveDirectory,"Colony_Backup.zxsav") + "\n20:01 - LoadLevel - Gamestate Loaded\n";
                coordinator.Poll(); Check(coordinator.State != RollbackState.Loaded,"Changed target content was incorrectly verified.");
            }
        });
    }
}
