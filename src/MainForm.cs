using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BillionsSaveManager
{
    public sealed class MainForm : Form
    {
        private readonly AppSettings settings;
        private readonly string settingsPath;
        private readonly IGameHost injectedHost;
        private SaveStore store;
        private IGameHost host;
        private RollbackCoordinator coordinator;
        private readonly Label pathLabel = new Label(), statusTitle = new Label(), statusDetail = new Label(), selectionDetail = new Label();
        private readonly Label gameLabel = new Label();
        private readonly Button restoreButton = new Button(), captureButton = new Button(), refreshButton = new Button(), folderButton = new Button();
        private readonly CheckBox automaticBox = new CheckBox();
        private readonly DataGridView localGrid = MakeGrid(), historyGrid = MakeGrid();
        private readonly TabControl tabs = new TabControl();
        private readonly TextBox activity = new TextBox();
        private readonly Timer timer = new Timer();
        private readonly ComboBox languageBox = new ComboBox();
        private readonly Dictionary<Control,string> textBindings = new Dictionary<Control,string>();
        private readonly Dictionary<string,string> observedVersions = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        private bool working, polling, gameRunning, populating;
        private int ticks;
        private string lastCoordinatorMessage = "";
        private string lastAutoError = "";
        private readonly Color accent = Color.FromArgb(25, 104, 86);

        public MainForm(AppSettings settings, string settingsPath, IGameHost injectedHost)
        {
            this.settings = settings; this.settingsPath = settingsPath; this.injectedHost = injectedHost;
            L.SetLanguage(settings.Language);
            Text = "They Are Billions · 存档回退助手";
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(245, 247, 249);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1040, 780); MinimumSize = new Size(900, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildLayout(); RememberText(this); ApplyLanguage(); InitializeStore();
            languageBox.SelectedIndexChanged += ChangeLanguage;
            timer.Interval = 1000; timer.Tick += TickAsync;
            Shown += delegate {
                BeginInvoke(new Action(delegate {
                    var preferred = localGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Tag is SavePair && ((SavePair)r.Tag).IsBackup);
                    if (preferred != null) { localGrid.ClearSelection(); localGrid.CurrentCell = preferred.Cells[0]; preferred.Selected = true; }
                    UpdateSelection(); timer.Start();
                }));
            };
            FormClosing += ClosingForm;
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 8 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            var heading = new Panel { Dock = DockStyle.Fill };
            heading.Controls.Add(new Label { Text = "存档回退助手", Font = new Font(Font.FontFamily, 22F, FontStyle.Bold), AutoSize = true, Location = new Point(0, 0), ForeColor = Color.FromArgb(30, 40, 51) });
            heading.Controls.Add(new Label { Text = "THEY ARE BILLIONS  /  保存一个重新来过的机会", AutoSize = true, Location = new Point(2, 43), ForeColor = Color.DimGray });
            var languageRow = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 250, FlowDirection = FlowDirection.LeftToRight };
            languageRow.Controls.Add(new Label { Text = "语言 / Language", AutoSize = true, Margin = new Padding(0,7,8,0) });
            languageBox.Name = "languageBox"; languageBox.DropDownStyle = ComboBoxStyle.DropDownList; languageBox.Width = 125;
            languageBox.Items.AddRange(new object[] { "简体中文", "English" });
            languageBox.SelectedIndex = L.Language == "zh-CN" ? 0 : 1;
            languageRow.Controls.Add(languageBox); heading.Controls.Add(languageRow);
            root.Controls.Add(heading, 0, 0);

            var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,125));
            pathLabel.Dock = DockStyle.Fill; pathLabel.TextAlign = ContentAlignment.MiddleLeft; pathLabel.AutoEllipsis = true;
            StyleButton(folderButton, "选择存档目录", false); folderButton.Click += ChangeFolder;
            pathRow.Controls.Add(pathLabel,0,0); pathRow.Controls.Add(folderButton,1,0); root.Controls.Add(pathRow,0,1);

            var card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16,10,16,10), RowCount = 2, ColumnCount = 1, Margin = new Padding(0,4,0,8) };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); card.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            statusTitle.Dock = DockStyle.Fill; statusTitle.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold); statusTitle.ForeColor = accent;
            statusDetail.Dock = DockStyle.Fill; statusDetail.ForeColor = Color.FromArgb(62,72,83);
            card.Controls.Add(statusTitle,0,0); card.Controls.Add(statusDetail,0,1); root.Controls.Add(card,0,2);

            var tools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,88)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,125)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));
            StyleButton(refreshButton,"刷新列表",false); refreshButton.Click += async delegate { await RefreshAsync(); };
            StyleButton(captureButton,"备份选中存档",false); captureButton.Click += CaptureAsync;
            var openButton = new Button(); StyleButton(openButton,"打开快照目录",false); openButton.Click += delegate { OpenArchive(); };
            automaticBox.Text = "自动保留新存档（每 15 秒检查）"; automaticBox.AutoSize = true; automaticBox.Anchor = AnchorStyles.Left; automaticBox.Checked = settings.AutomaticSnapshots;
            automaticBox.CheckedChanged += delegate { settings.AutomaticSnapshots = automaticBox.Checked; SaveSettings(); };
            gameLabel.Dock = DockStyle.Fill; gameLabel.TextAlign = ContentAlignment.MiddleRight;
            tools.Controls.Add(refreshButton,0,0); tools.Controls.Add(captureButton,1,0); tools.Controls.Add(openButton,2,0); tools.Controls.Add(automaticBox,3,0); tools.Controls.Add(gameLabel,4,0); root.Controls.Add(tools,0,3);

            tabs.Dock = DockStyle.Fill;
            var localPage = new TabPage("当前存档与自动备份") { Padding = new Padding(4), BackColor = Color.White };
            var historyPage = new TabPage("工具保留的历史快照") { Padding = new Padding(4), BackColor = Color.White };
            localPage.Controls.Add(localGrid); historyPage.Controls.Add(historyGrid); tabs.TabPages.Add(localPage); tabs.TabPages.Add(historyPage);
            tabs.SelectedIndexChanged += delegate { UpdateSelection(); };
            foreach (var grid in new[] { localGrid, historyGrid })
            {
                grid.Columns.Add("name", "存档名称"); grid.Columns.Add("kind", "类型"); grid.Columns.Add("time", "游戏保存时间"); grid.Columns.Add("size", "大小"); grid.Columns.Add("detail", grid == localGrid ? "回退时间" : "快照创建时间 / 原因");
                grid.Columns[0].FillWeight = 115; grid.Columns[1].FillWeight = 80; grid.Columns[2].FillWeight = 145; grid.Columns[3].FillWeight = 70; grid.Columns[4].FillWeight = 190;
                grid.SelectionChanged += delegate { UpdateSelection(); };
            }
            root.Controls.Add(tabs,0,4);

            var actionRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0,8,0,4) };
            actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,195));
            selectionDetail.Name = "selectionDetail"; selectionDetail.Dock = DockStyle.Fill; selectionDetail.TextAlign = ContentAlignment.MiddleLeft; selectionDetail.AutoEllipsis = true;
            StyleButton(restoreButton,"启动游戏并回退",true); restoreButton.Click += RestoreAsync;
            actionRow.Controls.Add(selectionDetail,0,0); actionRow.Controls.Add(restoreButton,1,0); root.Controls.Add(actionRow,0,5);
            activity.Dock = DockStyle.Fill; activity.Multiline = true; activity.ReadOnly = true; activity.ScrollBars = ScrollBars.Vertical; activity.BackColor = Color.White; activity.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(activity,0,6);
            root.Controls.Add(new Label { Text = "仅保留游戏已经写入磁盘的保存点，无法保证精确回退 5 分钟。历史快照保存在本机，不会自动上传。", Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.BottomLeft },0,7);
        }

        private static DataGridView MakeGrid()
        {
            return new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, RowTemplate = { Height = 32 }, ColumnHeadersHeight = 36, EnableHeadersVisualStyles = false, ColumnHeadersDefaultCellStyle = { BackColor = Color.FromArgb(235,240,243), ForeColor = Color.FromArgb(38,49,61) }, DefaultCellStyle = { SelectionBackColor = Color.FromArgb(219,239,232), SelectionForeColor = Color.FromArgb(16,63,50) } };
        }

        private void RememberText(Control control)
        {
            if (L.HasKey(control.Text)) textBindings.Add(control,control.Text);
            foreach (Control child in control.Controls) RememberText(child);
        }
        private void ApplyLanguage()
        {
            foreach (var binding in textBindings) binding.Key.Text = L.T(binding.Value);
            foreach (var grid in new[] { localGrid, historyGrid })
            {
                string[] headers = { "存档名称", "类型", "游戏保存时间", "大小", grid == localGrid ? "回退时间" : "快照创建时间 / 原因" };
                for (int i = 0; i < headers.Length; i++) grid.Columns[i].HeaderText = L.T(headers[i]);
            }
        }
        private void ChangeLanguage(object sender, EventArgs e)
        {
            if (working || polling || (coordinator != null && coordinator.Busy)) return;
            settings.Language = languageBox.SelectedIndex == 0 ? "zh-CN" : "en";
            L.SetLanguage(settings.Language); ApplyLanguage(); SaveSettings();
            if (store != null)
            {
                pathLabel.Text = L.T("存档目录：{0}", store.SaveDirectory);
                try { Populate(); CheckGame(); }
                catch (Exception error) { Log(error.Message); }
                if (coordinator.State == RollbackState.Idle)
                    SetStatus(L.T("先选择想回到的保存点"), L.T("回退前请正常退出游戏。工具会先备份、启动游戏并等待主菜单；看到“可以继续”后再点击游戏中的继续。"),false);
                else { lastCoordinatorMessage = ""; ShowCoordinator(); }
            }
            else InitializeStore();
            Log(L.T("语言已切换；已记录的活动消息保留原来的语言。"));
        }

        private void StyleButton(Button button, string text, bool primary)
        {
            button.UseMnemonic = false;
            button.Text = text; button.Dock = DockStyle.Fill; button.Margin = new Padding(0,4,8,4); button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(208,216,221);
            button.BackColor = primary ? accent : Color.White; button.ForeColor = primary ? Color.White : Color.FromArgb(38,49,61);
            if (primary) { button.Font = new Font(Font, FontStyle.Bold); button.Margin = new Padding(8,0,0,0); }
        }

        private void InitializeStore()
        {
            try
            {
                store = new SaveStore(settings.SaveDirectory,settings.ArchiveDirectory);
                host = injectedHost ?? new WindowsGameHost(store.SaveDirectory);
                coordinator = new RollbackCoordinator(store,host,delegate { return DateTime.UtcNow; });
                observedVersions.Clear(); pathLabel.Text = L.T("存档目录：{0}", store.SaveDirectory);
                SetStatus(L.T("先选择想回到的保存点"), L.T("回退前请正常退出游戏。工具会先备份、启动游戏并等待主菜单；看到“可以继续”后再点击游戏中的继续。"), false);
                Populate(); CheckGame();
            }
            catch (Exception e) { store = null; host = null; coordinator = null; pathLabel.Text = settings.SaveDirectory; SetStatus(L.T("需要设置存档目录"),e.Message,true); }
            UpdateButtons();
        }

        private void Populate()
        {
            if (store == null) return;
            SavePair previous = SelectedPair(); string previousKey = Key(previous);
            populating = true;
            try
            {
            var locals = store.ScanLocalPairs(); var snapshots = store.GetSnapshots();
            localGrid.Rows.Clear(); historyGrid.Rows.Clear();
            foreach (var pair in locals)
            {
                var main = locals.FirstOrDefault(p => p.SaveName == pair.SaveName && !p.IsBackup);
                string delta = pair.IsBackup && main != null ? Difference(main.SavedAtUtc - pair.SavedAtUtc) : pair.IsBackup ? L.T("仅剩自动备份") : L.T("当前进度");
                AddRow(localGrid,pair,delta);
            }
            foreach (var snapshot in snapshots)
                foreach (var pair in store.GetPairs(snapshot)) AddRow(historyGrid,pair,snapshot.CreatedUtc.ToLocalTime().ToString("MM-dd HH:mm:ss") + " / " + L.Reason(snapshot.Reason));
            var selectedGrid = tabs.SelectedIndex == 0 ? localGrid : historyGrid;
            DataGridViewRow found = selectedGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => Key(r.Tag as SavePair) == previousKey);
            if (found == null && tabs.SelectedIndex == 0) found = selectedGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => ((SavePair)r.Tag).IsBackup);
            if (found == null && selectedGrid.Rows.Count > 0) found = selectedGrid.Rows[0];
            if (found != null) { selectedGrid.ClearSelection(); selectedGrid.CurrentCell = found.Cells[0]; found.Selected = true; }
            }
            finally { populating = false; }
            UpdateSelection();
        }

        private static string Difference(TimeSpan span)
        {
            if (span.TotalSeconds < 0) return L.T("备份时间较新，请核对");
            return string.Format(L.T("约回退 {0} 分 {1} 秒"), (int)span.TotalMinutes, span.Seconds);
        }
        private static string Key(SavePair pair) { return pair == null ? "" : pair.SnapshotId + "/" + pair.FileStem; }
        private static void AddRow(DataGridView grid, SavePair pair, string detail)
        {
            int index = grid.Rows.Add(pair.SaveName, pair.IsBackup ? L.T("自动备份") : L.T("主存档"), pair.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), (pair.Length / 1024.0).ToString("N0") + " KB", detail);
            grid.Rows[index].Tag = pair;
        }
        private SavePair SelectedPair()
        {
            var grid = tabs.SelectedIndex == 0 ? localGrid : historyGrid;
            return grid.SelectedRows.Count == 1 ? grid.SelectedRows[0].Tag as SavePair : null;
        }
        private void UpdateSelection()
        {
            if (populating) return;
            var pair = SelectedPair();
            selectionDetail.Text = pair == null ? L.T("选择一行存档或历史快照。") : L.T("恢复目标：{0} · {1} · {2}", pair.SaveName, pair.SavedAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss"), (pair.IsBackup ? L.T("自动备份") : L.T("主存档快照")));
            UpdateButtons();
        }
        private void UpdateButtons()
        {
            // The timer's re-entry guard is not a user-visible operation.
            bool blocked = working || (coordinator != null && coordinator.Busy);
            restoreButton.Enabled = store != null && SelectedPair() != null && !blocked && !gameRunning;
            captureButton.Enabled = store != null && SelectedPair() != null && !blocked;
            refreshButton.Enabled = store != null && !blocked; folderButton.Enabled = !blocked;
            tabs.Enabled = !blocked; automaticBox.Enabled = !blocked; languageBox.Enabled = !blocked;
        }
        private void CheckGame()
        {
            try { gameRunning = host != null && host.Observe().Running; gameLabel.Text = gameRunning ? L.T("游戏正在运行") : L.T("游戏已退出"); gameLabel.ForeColor = gameRunning ? Color.FromArgb(160,96,24) : accent; }
            catch (Exception e) { gameRunning = true; gameLabel.Text = L.T("游戏状态未知"); Log(e.Message); }
            UpdateButtons();
        }

        private async void RestoreAsync(object sender, EventArgs e)
        {
            var pair = SelectedPair(); if (pair == null || working || coordinator == null) return;
            working = true; UpdateButtons();
            SetStatus(L.T("请暂时不要点击游戏里的继续"), L.T("工具正在保留原件。接下来会启动游戏，等到主菜单后处理回退文件。"), false);
            try { await Task.Run(delegate { coordinator.Begin(pair); }); ShowCoordinator(); }
            catch (Exception error) { SetStatus(L.T("尚未开始回退"),error.Message,true); Log(error.Message); }
            finally { working = false; UpdateButtons(); }
        }

        private async void CaptureAsync(object sender, EventArgs e)
        {
            var pair = SelectedPair(); if (pair == null || store == null || working) return;
            working = true; UpdateButtons();
            try { await Task.Run(delegate { store.Capture(pair.SaveName,"manual",false); }); Log(L.T("已备份当前磁盘上的「{0}」，完整性校验通过。", pair.SaveName)); Populate(); }
            catch (Exception error) { Log(error.Message); MessageBox.Show(this,error.Message,L.T("备份未完成"),MessageBoxButtons.OK,MessageBoxIcon.Information); }
            finally { working = false; UpdateButtons(); }
        }

        private async Task RefreshAsync()
        {
            if (store == null || working) return;
            working = true; UpdateButtons();
            try { await Task.Run(delegate { store.ScanLocalPairs(); }); Populate(); CheckGame(); }
            catch (Exception e) { Log(e.Message); }
            finally { working = false; UpdateButtons(); }
        }

        private async void TickAsync(object sender, EventArgs e)
        {
            if (working || polling || store == null) return;
            polling = true;
            try
            {
                if (coordinator != null && coordinator.Busy)
                {
                    await Task.Run(delegate { coordinator.Poll(); }); ShowCoordinator();
                }
                CheckGame();
                if ((coordinator == null || !coordinator.Busy) && automaticBox.Checked && ++ticks >= 15)
                {
                    ticks = 0; working = true; UpdateButtons();
                    try
                    {
                        var messages = await Task.Run(delegate { return AutoCapture(); });
                        foreach (string message in messages) Log(message);
                        if (messages.Count > 0) Populate();
                    }
                    finally { working = false; }
                }
            }
            catch (Exception error) { Log(error.Message); }
            finally { polling = false; UpdateButtons(); }
        }

        private List<string> AutoCapture()
        {
            var messages = new List<string>();
            foreach (var slot in store.ScanLocalPairs().GroupBy(p => p.SaveName))
            {
                try
                {
                    string signature = string.Join("|",slot.OrderBy(p => p.FileStem).Select(p => p.FileStem + ":" + SaveStore.HashFile(Path.Combine(p.Folder,p.FileStem + ".zxsav")) + ":" + SaveStore.HashFile(Path.Combine(p.Folder,p.FileStem + ".zxcheck"))).ToArray());
                    string previous;
                    if (observedVersions.TryGetValue(slot.Key,out previous) && previous == signature) continue;
                    store.Capture(slot.Key,"automatic",true); observedVersions[slot.Key] = signature;
                    messages.Add(L.T("已检查并保留「{0}」的完整磁盘存档。", slot.Key));
                }
                catch (IOException error) { if (error.Message != lastAutoError) { messages.Add(L.T("稍后重试备份：{0}", error.Message)); lastAutoError = error.Message; } }
            }
            return messages;
        }

        private void ShowCoordinator()
        {
            if (coordinator == null || coordinator.Message == lastCoordinatorMessage) return;
            lastCoordinatorMessage = coordinator.Message;
            string title = coordinator.State == RollbackState.WaitingForMenu ? L.T("正在等待游戏主菜单") : coordinator.State == RollbackState.AwaitingLoad ? L.T("文件已准备好，现在可以点继续") : coordinator.State == RollbackState.Loaded ? L.T("已确认游戏加载了恢复点") : L.T("本次回退已停止");
            SetStatus(title,coordinator.Message,coordinator.State == RollbackState.Failed); Log(coordinator.Message);
            if (coordinator.State == RollbackState.AwaitingLoad) { System.Media.SystemSounds.Asterisk.Play(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); }
            if (!coordinator.Busy) Populate();
        }
        private void SetStatus(string title, string detail, bool error) { statusTitle.Text = title; statusTitle.ForeColor = error ? Color.FromArgb(175,61,48) : accent; statusDetail.Text = detail; }
        private void Log(string message)
        {
            if (activity.TextLength > 24000) activity.Text = activity.Text.Substring(activity.TextLength - 12000);
            activity.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message.Replace("\n","  ") + Environment.NewLine);
        }
        private void ChangeFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = L.T("选择游戏的 Saves 目录"), SelectedPath = settings.SaveDirectory, ShowNewFolderButton = false })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings.SaveDirectory = dialog.SelectedPath; SaveSettings(); InitializeStore();
            }
        }
        private void SaveSettings()
        {
            if (string.IsNullOrEmpty(settingsPath)) return;
            try { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)); SaveStore.WriteJson(settingsPath,settings); }
            catch (Exception e) { Log(L.T("设置保存失败：{0}", e.Message)); }
        }
        private void OpenArchive()
        {
            try { Directory.CreateDirectory(settings.ArchiveDirectory); Process.Start(new ProcessStartInfo(settings.ArchiveDirectory) { UseShellExecute = true }); }
            catch (Exception e) { Log(e.Message); }
        }
        private void ClosingForm(object sender, FormClosingEventArgs e)
        {
            if (working || polling) { e.Cancel = true; return; }
            if (coordinator != null && coordinator.Busy && MessageBox.Show(this,L.T("回退流程尚未完成。关闭后将停止日志验证；已保留的快照和原件仍在。确定关闭工具吗？"),L.T("回退尚未完成"),MessageBoxButtons.YesNo,MessageBoxIcon.Information) != DialogResult.Yes)
            { e.Cancel = true; return; }
            timer.Stop(); SaveSettings();
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
