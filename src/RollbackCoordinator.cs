using System;
using System.IO;
using System.Linq;

namespace BillionsSaveManager
{
    public class RollbackCoordinator
    {
        private readonly SaveStore store;
        private readonly IGameHost host;
        private readonly Func<DateTime> now;
        private DateTime beganUtc;
        private DateTime processStartedUtc;
        private int processId;
        private string originalLog;
        private string armedLog;
        private DateTime armedUtc;
        public RollbackState State { get; private set; }
        private string messageTemplate;
        private Exception failureException;
        private object[] messageArguments = new object[0];
        public string Message { get { return failureException != null ? failureException.Message : L.T(messageTemplate, messageArguments); } }
        public SavePair Target { get; private set; }
        public bool Busy { get { return State == RollbackState.WaitingForMenu || State == RollbackState.AwaitingLoad; } }
        public RollbackCoordinator(SaveStore store, IGameHost host, Func<DateTime> now)
        { this.store = store; this.host = host; this.now = now; State = RollbackState.Idle; SetMessage("选择一个恢复点，然后启动游戏并回退。"); }

        public void Begin(SavePair pair)
        {
            if (Busy) throw new LocalizedInvalidOperationException("已有回退正在进行。");
            var observation = host.Observe();
            if (observation.Running) throw new LocalizedInvalidOperationException("请先正常保存并退出游戏，再启动回退。工具不会强制关闭游戏。");
            if (pair == null) throw new LocalizedInvalidOperationException("请先选择一个恢复点。");
            if (string.IsNullOrEmpty(pair.SnapshotId))
            {
                var snapshot = store.Capture(pair.SaveName, "selected", false);
                Target = store.GetPairs(snapshot).Single(p => p.FileStem == pair.FileStem);
            }
            else
            {
                var snapshot = store.GetSnapshots().SingleOrDefault(s => s.Id == pair.SnapshotId);
                if (snapshot == null) throw new LocalizedIOException("找不到该快照，请刷新列表。");
                Target = store.GetPairs(snapshot).Single(p => p.FileStem == pair.FileStem);
            }
            originalLog = observation.LogText; beganUtc = now(); processId = 0;
            State = RollbackState.WaitingForMenu;
            SetMessage("正在通过 Steam 启动游戏。请停在主菜单，暂时不要点击继续。");
            try { host.Launch(); }
            catch (Exception e) { Fail(new LocalizedInvalidOperationException("启动失败：{0}", e)); throw; }
        }

        public void Poll()
        {
            if (!Busy) return;
            try
            {
                var observation = host.Observe();
                if (State == RollbackState.WaitingForMenu)
                {
                    if (now() - beganUtc > TimeSpan.FromMinutes(3)) { Fail("等待主菜单超时，尚未处理存档。请退出游戏后重试。"); return; }
                    if (!observation.Running) return;
                    if (observation.StartedUtc < beganUtc.AddSeconds(-2) || observation.LogLastWriteUtc < beganUtc || string.IsNullOrEmpty(observation.LogText) || observation.LogText == originalLog) return;
                    if (!string.IsNullOrEmpty(originalLog) && observation.LogText.StartsWith(originalLog,StringComparison.Ordinal) && observation.LogText.LastIndexOf("Log Start",StringComparison.Ordinal) < originalLog.Length) return;
                    var parsed = GameLog.Parse(observation.LogText);
                    if (!parsed.HasSessionStart) return;
                    if (parsed.StartedLoading) { Fail("检测到提前点击了继续或其他菜单，尚未处理存档。请退出游戏后重试。"); return; }
                    if (!parsed.MainMenu) return;
                    processId = observation.ProcessId; processStartedUtc = observation.StartedUtc;
                    store.Restore(Target, delegate {
                        var current = host.Observe();
                        var currentLog = GameLog.Parse(current.LogText);
                        return SameSession(current) && current.LogLastWriteUtc >= beganUtc && currentLog.MainMenu && !currentLog.StartedLoading;
                    });
                    armedLog = observation.LogText; armedUtc = now();
                    State = RollbackState.AwaitingLoad;
                    SetMessage("现在可以点击「继续」了！请选择 {0}，直接在当前游戏里加载，暂时不要重启。", Target.SaveName);
                }
                else if (State == RollbackState.AwaitingLoad)
                {
                    if (!SameSession(observation)) { Fail("游戏已退出或重新启动，尚未确认读档。原件和恢复点都已保留，可退出游戏后重新执行回退。"); return; }
                    if (now() - armedUtc > TimeSpan.FromMinutes(10)) { Fail("十分钟内没有确认目标读档，日志验证已停止。准备好的存档与原件均保留，请检查游戏进度。"); return; }
                    if (string.IsNullOrEmpty(observation.LogText)) return;
                    if (!observation.LogText.StartsWith(armedLog,StringComparison.Ordinal)) { Fail("本次会话的日志已被替换，无法确认读档。快照与原件均已保留。"); return; }
                    var parsed = GameLog.Parse(observation.LogText.Substring(armedLog.Length));
                    if (parsed.LoadedPath == null) { store.VerifyActiveTarget(Target); return; }
                    string expected = Path.GetFullPath(Path.Combine(store.SaveDirectory, Target.FileStem + ".zxsav"));
                    if (!string.Equals(expected, Path.GetFullPath(parsed.LoadedPath), StringComparison.OrdinalIgnoreCase))
                    { Fail("日志显示加载了其他存档，本次未确认回退成功。请检查选择的游戏进度。"); return; }
                    store.VerifyActiveTarget(Target);
                    State = RollbackState.Loaded;
                    SetMessage("已确认加载 {0} 的恢复点。可以继续玩；结束时正常保存退出，再等待 Steam 云同步完成。", Target.SavedAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss"));
                }
            }
            catch (Exception e) { Fail(e); }
        }

        private bool SameSession(GameObservation observation)
        { return observation.Running && observation.ProcessId == processId && observation.StartedUtc == processStartedUtc; }
        private void SetMessage(string template, params object[] args) { failureException = null; messageTemplate = template; messageArguments = args; }
        private void Fail(Exception error) { State = RollbackState.Failed; failureException = error; }
        private void Fail(string message, params object[] args) { State = RollbackState.Failed; SetMessage(message, args); }
    }
}
