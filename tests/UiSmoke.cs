using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BillionsSaveManager;

internal static class UiSmoke
{
    private sealed class IdleHost : IGameHost
    {
        public GameObservation Observe() { return new GameObservation(); }
        public void Launch() { throw new InvalidOperationException("UI fixture cannot launch a real game."); }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        string fixture = Path.Combine(Path.GetTempPath(),"TABUiSmoke-" + Guid.NewGuid().ToString("N"));
        string saves = Path.Combine(fixture,"Saves"), snapshots = Path.Combine(fixture,"Snapshots");
        Directory.CreateDirectory(saves);
        bool passed = true;
        try
        {
            foreach (string stem in new[] { "我的生存挑战", "我的生存挑战_Backup", "Frozen Highlands" })
            {
                File.WriteAllText(Path.Combine(saves,stem + ".zxsav"),"fixture data"); File.WriteAllText(Path.Combine(saves,stem + ".zxcheck"),"fixture check");
                DateTime time = new DateTime(2026,9,5,12,31,0,DateTimeKind.Utc).AddSeconds(stem.EndsWith("_Backup") ? -439 : 0);
                File.SetLastWriteTimeUtc(Path.Combine(saves,stem + ".zxsav"),time); File.SetLastWriteTimeUtc(Path.Combine(saves,stem + ".zxcheck"),time);
            }
            var store = new SaveStore(saves,snapshots); store.Capture("我的生存挑战","手动备份",false);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new MainForm(new AppSettings { SaveDirectory = saves, ArchiveDirectory = snapshots, AutomaticSnapshots = false },null,new IdleHost()))
            using (var timer = new Timer { Interval = 800 })
            {
                int stage = 0;
                timer.Tick += delegate {
                    var tab = FindControls<TabControl>(form).Single();
                    var grid = FindControls<DataGridView>(tab.SelectedTab).Single();
                    var selected = grid.SelectedRows.Count == 1 ? grid.SelectedRows[0].Tag as SavePair : null;
                    var detail = FindControls<Label>(form).Single(label => label.Text.StartsWith("恢复目标："));
                    if (selected == null || !selected.IsBackup || !detail.Text.Contains(selected.SaveName) || !detail.Text.Contains("自动备份"))
                    { Console.WriteLine("FAIL default selection / target detail: " + (selected == null ? "none" : selected.FileStem) + " | " + detail.Text); passed = false; }
                    using (var bitmap = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height)); bitmap.Save(Path.Combine(args[0],stage == 0 ? "ui-preview.png" : "ui-preview-compact.png")); }
                    if (stage++ == 0) { form.Size = form.MinimumSize; } else { timer.Stop(); form.Close(); }
                };
                form.Shown += delegate { timer.Start(); }; Application.Run(form);
            }
            Console.WriteLine("UI smoke " + (passed ? "passed" : "failed") + "; rendered normal and minimum-size windows."); return passed ? 0 : 1;
        }
        finally
        {
            string resolved = Path.GetFullPath(fixture);
            if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("TABUiSmoke-")) Directory.Delete(resolved,true);
        }
    }
    private static System.Collections.Generic.IEnumerable<T> FindControls<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls) { if (child is T) yield return (T)child; foreach (var nested in FindControls<T>(child)) yield return nested; }
    }
}
