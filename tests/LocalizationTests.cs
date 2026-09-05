using System;
using System.IO;
using System.Linq;
using BillionsSaveManager;

internal static class LocalizationTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class IdleHost : IGameHost
    {
        public GameObservation Observe() { return new GameObservation(); }
        public void Launch() { }
    }
    internal static void Run(Action<string,Action> run)
    {
        run("automatic language follows current Windows display culture", delegate {
            var previous = System.Threading.Thread.CurrentThread.CurrentUICulture;
            try {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US"); L.SetLanguage(null);
                Check(L.Language == "en", "Automatic language ignored current English display culture.");
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN"); L.SetLanguage(null);
                Check(L.Language == "zh-CN", "Automatic language ignored current Chinese display culture.");
            }
            finally { System.Threading.Thread.CurrentThread.CurrentUICulture = previous; }
        });
        run("language preference survives settings roundtrip and legacy settings", delegate {
            using (var f = new CoreTests.Fixture()) {
                string file = Path.Combine(f.Root,"settings.json");
                File.WriteAllText(file,"{\"AutomaticSnapshots\":true}");
                var legacy = SaveStore.ReadJson<AppSettings>(file);
                Check(legacy.AutomaticSnapshots && L.ResolveLanguage(legacy.Language,"zh-TW") == "zh-CN", "Legacy settings did not use the system language.");
                Check(L.ResolveLanguage(null,"de-DE") == "en", "Unsupported system language has no English fallback.");
                legacy.Language = "en"; SaveStore.WriteJson(file,legacy);
                var saved = SaveStore.ReadJson<AppSettings>(file);
                Check(L.ResolveLanguage(saved.Language,"zh-CN") == "en", "Explicit language was not persisted or was overridden.");
            }
        });
        run("existing coordinator messages follow the selected language", delegate {
            using (var f = new CoreTests.Fixture()) {
                L.SetLanguage("zh-CN");
                var coordinator = new RollbackCoordinator(f.Store,new IdleHost(),delegate { return DateTime.UtcNow; });
                string before = coordinator.Message; L.SetLanguage("en");
                Check(coordinator.Message != before && !coordinator.Message.Any(c => c >= '\u4e00' && c <= '\u9fff'), "Coordinator retained its old language.");
            }
        });
        run("localization preserves Unicode filenames, braces and archived reason values", delegate {
            using (var f = new CoreTests.Fixture()) {
                const string name = "生存 {0}";
                f.Pair(name,"original"); var snap = f.Store.Capture(name,"手动备份",false);
                L.SetLanguage("en"); string english = L.Reason(snap.Reason);
                Check(english != snap.Reason && L.T("已备份当前磁盘上的「{0}」，完整性校验通过。",name).Contains(name), "Translation changed the save name or failed to translate a legacy reason.");
                L.SetLanguage("zh-CN");
                Check(L.Reason("manual") == "手动备份" && f.Store.GetSnapshots().Single().Reason == "手动备份", "Language switching rewrote stored metadata.");
                f.Pair(name,"new"); f.Store.Restore(f.Store.GetPairs(snap).Single(),delegate { return true; });
                Check(f.Read(name) == "original", "Localization changed the restored filename or data.");
            }
        });
        run("English validation errors keep dynamic paths intact", delegate {
            using (var f = new CoreTests.Fixture()) {
                L.SetLanguage("en"); string missing = Path.Combine(f.Root,"missing");
                try { new SaveStore(missing,Path.Combine(f.Root,"Archive")); throw new Exception("Missing directory was accepted."); }
                catch (DirectoryNotFoundException e) {
                    Check(e.Message.Contains(missing) && !e.Message.Any(c => c >= '\u4e00' && c <= '\u9fff'), "Validation error was not localized or lost its path.");
                }
            }
        });
    }
}
