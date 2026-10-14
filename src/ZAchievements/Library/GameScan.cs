using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZAchievements.Steam;

namespace ZAchievements.Library
{
    internal readonly record struct ScanProgress(int Checked, int Total, int Found);

    /// <summary>
    /// Turns the shared catalogue into library entries. Steam answers ownership
    /// from its local cache, so the scan stays on the UI thread (where the client
    /// lives) and simply breathes every few thousand entries.
    /// </summary>
    internal sealed class GameScan
    {
        private const int EntriesPerBreath = 2048;

        private readonly SteamConnection _steam;

        public GameScan(SteamConnection steam)
        {
            this._steam = steam;
        }

        public async Task<List<GameEntry>> RunAsync(
            IReadOnlyList<(uint Id, string Kind)> catalogue,
            bool includeUnowned,
            IReadOnlyDictionary<uint, string> spareNames,
            IProgress<ScanProgress> progress)
        {
            var apps = this._steam.Client.SteamApps008;
            var appData = this._steam.Client.SteamApps001;

            List<GameEntry> found = new();
            HashSet<uint> seen = new();

            for (int i = 0; i < catalogue.Count; i++)
            {
                var (id, kind) = catalogue[i];

                if (seen.Add(id) == true)
                {
                    var owned = apps.IsSubscribedApp(id);
                    if (owned == true || includeUnowned == true)
                    {
                        found.Add(new GameEntry(id, kind, Name(id, owned), owned));
                    }
                }

                if (i % EntriesPerBreath == EntriesPerBreath - 1)
                {
                    progress?.Report(new ScanProgress(i + 1, catalogue.Count, found.Count));
                    await Task.Yield();
                }
            }

            progress?.Report(new ScanProgress(catalogue.Count, catalogue.Count, found.Count));
            return found;

            // Steam only holds app data for what the account touches, so for the rest
            // the public app list answers faster than asking the client and failing.
            string Name(uint id, bool owned)
            {
                if (owned == false && spareNames != null && spareNames.TryGetValue(id, out var spare) == true)
                {
                    return spare;
                }

                return appData.GetAppData(id, "name");
            }
        }

        /// <summary>One app by id, owned or not — the manual "add by app id" path.</summary>
        public GameEntry Single(uint id)
        {
            return new GameEntry(
                id,
                "normal",
                this._steam.Client.SteamApps001.GetAppData(id, "name"),
                this._steam.Client.SteamApps008.IsSubscribedApp(id));
        }
    }
}
