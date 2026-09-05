using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace BillionsSaveManager
{
    public class WindowsGameHost : IGameHost
    {
        private readonly string logPath;
        public WindowsGameHost(string saveDirectory) { logPath = Path.Combine(Directory.GetParent(Path.GetFullPath(saveDirectory)).FullName, "ZXLog.txt"); }

        public GameObservation Observe()
        {
            var result = new GameObservation();
            var processes = new[] { "TheyAreBillions", "TheyAreBillions_x86", "ZXGame" }.SelectMany(Process.GetProcessesByName).ToArray();
            try
            {
                if (processes.Length > 1) throw new LocalizedInvalidOperationException("检测到多个游戏进程，请先退出多余的游戏窗口。");
                if (processes.Length == 1)
                {
                    result.Running = true; result.ProcessId = processes[0].Id; result.StartedUtc = processes[0].StartTime.ToUniversalTime();
                }
            }
            finally { foreach (var process in processes) process.Dispose(); }
            if (File.Exists(logPath))
            {
                try
                {
                    using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        if (stream.Length > 4 * 1024 * 1024) stream.Seek(-4 * 1024 * 1024, SeekOrigin.End);
                        using (var reader = new StreamReader(stream, Encoding.UTF8, true)) result.LogText = reader.ReadToEnd();
                    }
                    result.LogLastWriteUtc = File.GetLastWriteTimeUtc(logPath);
                }
                catch (IOException) { result.LogText = ""; }
            }
            return result;
        }

        public void Launch()
        {
            string steamDirectory = null;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                if (key != null) steamDirectory = key.GetValue("SteamPath") as string;
            if (string.IsNullOrEmpty(steamDirectory)) steamDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            string executable = Path.Combine(steamDirectory, "steam.exe");
            if (!File.Exists(executable)) throw new LocalizedFileNotFoundException("未找到 Steam，请安装并登录 Steam 后重试。", executable);
            using (var process = Process.Start(new ProcessStartInfo(executable, "-applaunch 644930") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden })) { }
        }
    }
}
