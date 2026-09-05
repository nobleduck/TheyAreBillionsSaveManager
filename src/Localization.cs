using System;
using System.Collections.Generic;
using System.Globalization;

namespace BillionsSaveManager
{
    // Source-language keys follow the gettext convention. Keep format placeholders aligned.
    // Do not localize filenames, game-log markers, snapshot identifiers or journal state values.
    public static class L
    {
        private static volatile string language = ResolveLanguage(null, CultureInfo.CurrentUICulture.Name);
        public static string Language { get { return language; } }
        public static string ResolveLanguage(string preference, string systemLanguage)
        {
            string value = string.IsNullOrWhiteSpace(preference) ? systemLanguage : preference;
            return value != null && value.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en";
        }
        public static void SetLanguage(string preference)
        { language = ResolveLanguage(preference, CultureInfo.CurrentUICulture.Name); }
        public static bool HasKey(string source) { return source != null && english.ContainsKey(source); }
        public static string T(string source, params object[] args)
        {
            string translated;
            if (language != "zh-CN" && english.TryGetValue(source, out translated)) source = translated;
            return args.Length == 0 ? source : string.Format(CultureInfo.InvariantCulture, source, args);
        }
        public static string Reason(string reason)
        {
            switch (reason)
            {
                case "manual": case "手动备份": return T("手动备份");
                case "automatic": case "自动保留": return T("自动保留");
                case "selected": case "选定恢复点": return T("选定恢复点");
                case "before-restore": case "回退前安全备份": return T("回退前安全备份");
                default: return reason ?? "";
            }
        }
        private static readonly Dictionary<string,string> english = new Dictionary<string,string>(StringComparer.Ordinal)
        {
            { "They Are Billions · 存档回退助手", "They Are Billions · Save Manager" },
            { "存档回退助手", "Save Manager" },
            { "THEY ARE BILLIONS  /  保存一个重新来过的机会", "THEY ARE BILLIONS  /  Keep a chance to try again" },
            { "选择存档目录", "Save folder" },
            { "刷新列表", "Refresh" },
            { "备份选中存档", "Back up selected" },
            { "打开快照目录", "Open snapshots" },
            { "自动保留新存档（每 15 秒检查）", "Keep new saves (every 15 s)" },
            { "当前存档与自动备份", "Current saves & game backups" },
            { "工具保留的历史快照", "Snapshot history" },
            { "存档名称", "Save name" },
            { "类型", "Type" },
            { "游戏保存时间", "Game save time" },
            { "大小", "Size" },
            { "回退时间", "Rollback time" },
            { "快照创建时间 / 原因", "Snapshot time / reason" },
            { "启动游戏并回退", "Launch & restore" },
            { "仅保留游戏已经写入磁盘的保存点，无法保证精确回退 5 分钟。历史快照保存在本机，不会自动上传。", "Only existing disk saves can be restored. Exact rollback times aren't guaranteed. Snapshots stay on this PC." },
            { "存档目录：{0}", "Save folder: {0}" },
            { "先选择想回到的保存点", "Choose a restore point" },
            { "回退前请正常退出游戏。工具会先备份、启动游戏并等待主菜单；看到“可以继续”后再点击游戏中的继续。", "Exit the game normally first. This tool backs up your saves, launches the game and waits for its main menu. Click Continue only after the tool says it is ready." },
            { "需要设置存档目录", "Choose a valid save folder" },
            { "仅剩自动备份", "Only game backup remains" },
            { "当前进度", "Current progress" },
            { "备份时间较新，请核对", "Backup is newer; check the times" },
            { "约回退 {0} 分 {1} 秒", "About {0} min {1} sec earlier" },
            { "自动备份", "Game backup" },
            { "主存档", "Main save" },
            { "选择一行存档或历史快照。", "Select a save or snapshot row." },
            { "恢复目标：{0} · {1} · {2}", "Restore: {0} · {1} · {2}" },
            { "主存档快照", "Main save snapshot" },
            { "游戏正在运行", "Game running" },
            { "游戏已退出", "Game closed" },
            { "游戏状态未知", "Game status unknown" },
            { "请暂时不要点击游戏里的继续", "Do not click Continue yet" },
            { "工具正在保留原件。接下来会启动游戏，等到主菜单后处理回退文件。", "Preserving the originals. The game will launch next; restore files will be prepared once its main menu is confirmed." },
            { "尚未开始回退", "Restore has not started" },
            { "手动备份", "Manual backup" },
            { "已备份当前磁盘上的「{0}」，完整性校验通过。", "Backed up the current disk save for \"{0}\" and verified its contents." },
            { "备份未完成", "Backup not completed" },
            { "自动保留", "Automatic snapshot" },
            { "已检查并保留「{0}」的完整磁盘存档。", "Checked and preserved the complete disk save for \"{0}\"." },
            { "稍后重试备份：{0}", "Backup will retry later: {0}" },
            { "正在等待游戏主菜单", "Waiting for the main menu" },
            { "文件已准备好，现在可以点继续", "Ready! You can click Continue now" },
            { "已确认游戏加载了恢复点", "Restore point loaded and confirmed" },
            { "本次回退已停止", "Restore stopped" },
            { "选择游戏的 Saves 目录", "Select the game's Saves folder" },
            { "设置保存失败：{0}", "Could not save settings: {0}" },
            { "回退流程尚未完成。关闭后将停止日志验证；已保留的快照和原件仍在。确定关闭工具吗？", "The restore is not finished. Closing stops log verification; snapshots and preserved originals will remain. Close the tool?" },
            { "回退尚未完成", "Restore not finished" },
            { "检测到多个游戏进程，请先退出多余的游戏窗口。", "Multiple game processes were found. Close the extra game windows first." },
            { "未找到 Steam，请安装并登录 Steam 后重试。", "Steam was not found. Install Steam and sign in, then retry." },
            { "存档目录与快照目录不能相同或互相包含。", "Save and snapshot folders must be separate; neither may contain the other." },
            { "找不到游戏存档目录：{0}", "Game save folder not found: {0}" },
            { "存档配对不完整，等待游戏完成保存后重试：{0}", "Save pair is incomplete. Wait for the game to finish saving, then retry: {0}" },
            { "存档仍在保存，或校验文件时间不匹配，请稍后重试：{0}", "The save is still being written or the pair timestamps differ. Retry later: {0}" },
            { "复制期间存档发生变化，本次快照已取消。", "The save changed while being copied. This snapshot was canceled." },
            { "没有可备份的完整存档。", "No complete save pair is available to back up." },
            { "快照校验失败，尚未修改游戏存档。", "Snapshot verification failed. Game saves have not been modified." },
            { "恢复目标必须是已校验的历史快照。", "The restore target must be a verified archived snapshot." },
            { "目标不在快照清单中。", "The target is not in the snapshot manifest." },
            { "回退前安全备份", "Safety backup before restore" },
            { "恢复暂存文件校验失败。", "Verification of the staged restore files failed." },
            { "主菜单期间存档发生变化，本次回退已停止。", "The saves changed while the game was at its main menu. Restore stopped." },
            { "即将移动的存档发生变化。", "A save changed before it could be moved." },
            { "移动期间原件发生变化。", "An original file changed while being moved." },
            { "恢复结果校验失败。", "Verification of the restored files failed." },
            { "保留了外部新文件，原件仍在恢复目录：{0}", "Kept an externally created file; the original remains in the recovery folder: {0}" },
            { "已保留快照与原件：{0}", "Snapshots and originals have been preserved: {0}" },
            { "已撤销文件操作。", "File operations have been rolled back." },
            { "较新存档重新出现，可能来自云同步，本次未确认成功。请退出游戏后重试。", "Another save pair reappeared, possibly from cloud sync. Success is not confirmed. Exit the game and retry." },
            { "恢复目标发生变化或缺失，本次未确认成功。历史快照仍然保留。", "The restore target changed or is missing. Success is not confirmed. Archived snapshots remain available." },
            { "快照清单不完整。", "The snapshot manifest is incomplete." },
            { "快照清单包含无效文件。", "The snapshot manifest contains invalid files." },
            { "快照中的存档配对不完整。", "A save pair in the snapshot is incomplete." },
            { "快照完整性校验失败：{0}", "Snapshot integrity verification failed: {0}" },
            { "存档文件为空或超过 256 MB：{0}", "The save file is empty or exceeds 256 MB: {0}" },
            { "存档正在变化，请稍后重试。", "The save is changing. Retry later." },
            { "游戏已离开主菜单或进入了存档列表，回退已停止。请退出游戏后重试。", "The game left its main menu or opened the save list. Restore stopped. Exit the game and retry." },
            { "目录不能为空。", "The folder path cannot be empty." },
            { "无效的存档或快照名称。", "Invalid save or snapshot name." },
            { "为避免操作到其他位置，暂不支持链接目录或文件：{0}", "Linked folders and files are not supported, to avoid modifying another location: {0}" },
            { "存档回退助手已经在运行，请打开已有窗口。", "Save Manager is already running. Open its existing window." },
            { "原设置无法读取，将使用默认设置。", "The previous settings could not be read. Default settings will be used." },
            { "存档回退助手遇到错误", "Save Manager error" },
            { "选择一个恢复点，然后启动游戏并回退。", "Choose a restore point, then launch the game to restore it." },
            { "已有回退正在进行。", "A restore is already in progress." },
            { "请先正常保存并退出游戏，再启动回退。工具不会强制关闭游戏。", "Save and exit the game normally before starting a restore. This tool does not force the game to close." },
            { "请先选择一个恢复点。", "Select a restore point first." },
            { "选定恢复点", "Selected restore point" },
            { "找不到该快照，请刷新列表。", "The snapshot could not be found. Refresh the list." },
            { "正在通过 Steam 启动游戏。请停在主菜单，暂时不要点击继续。", "Launching through Steam. Stay at the main menu and do not click Continue yet." },
            { "启动失败：{0}", "Launch failed: {0}" },
            { "等待主菜单超时，尚未处理存档。请退出游戏后重试。", "Timed out waiting for the main menu. Saves have not been modified. Exit the game and retry." },
            { "检测到提前点击了继续或其他菜单，尚未处理存档。请退出游戏后重试。", "Continue or another menu was opened too early. Saves have not been modified. Exit the game and retry." },
            { "现在可以点击「继续」了！请选择 {0}，直接在当前游戏里加载，暂时不要重启。", "You can click Continue now! Load {0} in the current game session. Do not restart the game yet." },
            { "游戏已退出或重新启动，尚未确认读档。原件和恢复点都已保留，可退出游戏后重新执行回退。", "The game exited or restarted before loading was confirmed. Originals and restore points remain available. Exit the game and run the restore again." },
            { "十分钟内没有确认目标读档，日志验证已停止。准备好的存档与原件均保留，请检查游戏进度。", "Loading was not confirmed within 10 minutes. Log verification stopped. Prepared saves and originals remain available; check your progress in the game." },
            { "本次会话的日志已被替换，无法确认读档。快照与原件均已保留。", "This session's log was replaced, so loading cannot be confirmed. Snapshots and originals have been preserved." },
            { "日志显示加载了其他存档，本次未确认回退成功。请检查选择的游戏进度。", "The log shows a different save was loaded. Restore success is not confirmed. Check the selected game progress." },
            { "已确认加载 {0} 的恢复点。可以继续玩；结束时正常保存退出，再等待 Steam 云同步完成。", "Confirmed loading of the restore point from {0}. You can keep playing. Save and exit normally when finished, then wait for Steam cloud sync." },
            { "语言已切换；已记录的活动消息保留原来的语言。", "Language changed. Previously recorded activity messages keep their original language." },
        };
    }
}
