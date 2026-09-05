using System;

namespace BillionsSaveManager
{
    public class GameLogState
    {
        public bool MainMenu;
        public bool StartedLoading;
        public bool HasSessionStart;
        public string LoadedPath;
    }
    public static class GameLog
    {
        public static GameLogState Parse(string text)
        {
            text = text ?? "";
            int session = text.LastIndexOf("Log Start", StringComparison.Ordinal);
            if (session >= 0) text = text.Substring(session);
            int menu = text.LastIndexOf("ZXSystem_StartScreen - ShowSceneSuccess", StringComparison.Ordinal);
            int chooser = text.LastIndexOf("Window Show:", StringComparison.Ordinal);
            int randomMap = text.LastIndexOf("Random Map Creation", StringComparison.Ordinal);
            const string marker = "Loading game: ";
            int load = text.LastIndexOf(marker, StringComparison.Ordinal);
            int finished = text.LastIndexOf("LoadLevel - Gamestate Loaded", StringComparison.Ordinal);
            var result = new GameLogState
            {
                MainMenu = menu >= 0 && chooser < menu && load < menu && randomMap < menu,
                StartedLoading = chooser >= 0 || load >= 0 || randomMap >= 0,
                HasSessionStart = session >= 0
            };
            if (load >= 0 && finished > load && randomMap < load && load > menu)
            {
                int start = load + marker.Length;
                int end = text.IndexOf('\n', start);
                result.LoadedPath = (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).Trim();
            }
            return result;
        }
    }
}
