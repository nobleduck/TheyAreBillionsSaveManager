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
            string settingsPath = Path.Combine(fixture,"settings.json");
            using (var form = new MainForm(new AppSettings { SaveDirectory = saves, ArchiveDirectory = snapshots, AutomaticSnapshots = false, Language = "zh-CN" },settingsPath,new IdleHost()))
            using (var timer = new Timer { Interval = 800 })
            {
                int stage = 0;
                string selectedKey = null;
                timer.Tick += delegate {
                    try {
                    var language = FindControls<ComboBox>(form).Single(box => box.Name == "languageBox");
                    bool english = stage == 2 || stage == 3;
                    var tab = FindControls<TabControl>(form).Single();
                    var grid = FindControls<DataGridView>(tab.SelectedTab).Single();
                    var selected = grid.SelectedRows.Count == 1 ? grid.SelectedRows[0].Tag as SavePair : null;
                    var detail = FindControls<Label>(form).Single(label => label.Name == "selectionDetail");
                    if (selected == null || !selected.IsBackup || !detail.Text.Contains(selected.SaveName) || !detail.Text.Contains(english ? "Game backup" : "自动备份"))
                    { Console.WriteLine("FAIL default selection / target detail: " + (selected == null ? "none" : selected.FileStem) + " | " + detail.Text); passed = false; }
                    if (stage == 0) selectedKey = selected == null ? null : selected.FileStem;
                    if (selected == null || selected.FileStem != selectedKey) throw new Exception("Switching language changed the selected restore point.");
                    if (!form.Text.Contains(english ? "Save Manager" : "存档回退助手") || grid.Columns[0].HeaderText != (english ? "Save name" : "存档名称"))
                        throw new Exception("Window or grid headings kept their old language.");
                    string[] names = { "ui-preview-zh.png", "ui-preview-zh-compact.png", "ui-preview-en.png", "ui-preview-en-compact.png", "ui-preview-switch-back.png" };
                    using (var bitmap = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height)); bitmap.Save(Path.Combine(args[0],names[stage])); }
                    if (stage == 0 || stage == 2) form.Size = form.MinimumSize;
                    else if (stage == 1) { language.SelectedIndex = 1; form.ClientSize = new Size(1040,780); }
                    else if (stage == 3) {
                        var persisted = SaveStore.ReadJson<AppSettings>(settingsPath);
                        if (persisted.Language != "en" || persisted.AutomaticSnapshots) throw new Exception("Language was not saved or another preference changed.");
                        using (var reopened = new MainForm(persisted,null,new IdleHost()))
                            if (!reopened.Text.Contains("Save Manager")) throw new Exception("Reopened window ignored saved English preference.");
                        language.SelectedIndex = 0;
                    }
                    else { timer.Stop(); form.Close(); }
                    stage++;
                    }
                    catch (Exception error) { passed = false; Console.WriteLine("FAIL UI: " + error.Message); timer.Stop(); form.Close(); }
                };
                form.Shown += delegate { timer.Start(); }; Application.Run(form);
            }
            Console.WriteLine("UI smoke " + (passed ? "passed" : "failed") + "; rendered both languages at normal/minimum size and verified switching, selection and persistence."); return passed ? 0 : 1;
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
