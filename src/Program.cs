using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BillionsSaveManager
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool first;
            using (var mutex = new Mutex(true, @"Local\TheyAreBillionsSaveManager", out first))
            {
                if (!first) { MessageBox.Show("存档回退助手已经在运行，请打开已有窗口。", "存档回退助手"); return; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"TheyAreBillionsSaveManager\settings.json");
                AppSettings settings = AppSettings.Defaults();
                try { if (File.Exists(path)) settings = SaveStore.ReadJson<AppSettings>(path) ?? settings; }
                catch (Exception e) { MessageBox.Show("原设置无法读取，将使用默认设置。\n" + e.Message,"存档回退助手"); }
                try { Application.Run(new MainForm(settings,path,null)); }
                catch (Exception e) { MessageBox.Show(e.Message,"存档回退助手遇到错误",MessageBoxButtons.OK,MessageBoxIcon.Error); }
                mutex.ReleaseMutex();
            }
        }
    }
}
