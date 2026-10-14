using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZAchievements.Schema;
using ZAchievements.Steam;
using ZAchievements.Steam.Callbacks;
using APITypes = ZAchievements.Steam.Types;

namespace ZAchievements.Achievements
{
    internal readonly record struct CommitResult(int Achievements, int Statistics, int Refused);

    /// <summary>Reads and writes one game's achievements and statistics.</summary>
    internal sealed class AchievementSession
    {
        private readonly SteamConnection _steam;
        private readonly uint _appId;
        private readonly UserStatsReceived _statsReceived;

        private TaskCompletionSource<int> _pending;

        public AchievementSession(SteamConnection steam, uint appId)
        {
            this._steam = steam;
            this._appId = appId;

            this._statsReceived = steam.Client.CreateAndRegisterCallback<UserStatsReceived>();
            this._statsReceived.OnRun += this.OnStatsReceived;
        }

        public string GameName
        {
            get
            {
                var name = this._steam.Client.SteamApps001.GetAppData(this._appId, "name");
                return string.IsNullOrEmpty(name) == true ? this._appId.ToString() : name;
            }
        }

        /// <summary>Asks Steam for the signed-in user's stats. Returns Steam's result code, 1 on success.</summary>
        public Task<int> RequestAsync()
        {
            var steamId = this._steam.Client.SteamUser.GetSteamId();
            if (this._steam.Client.SteamUserStats.RequestUserStats(steamId) == CallHandle.Invalid)
            {
                return Task.FromResult(0);
            }

            this._pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            return this._pending.Task;
        }

        public GameStatsSchema LoadSchema()
        {
            return GameStatsSchema.Load(this._appId, this._steam.Client.SteamApps008.GetCurrentGameLanguage());
        }

        public List<AchievementRow> ReadAchievements(GameStatsSchema schema)
        {
            var stats = this._steam.Client.SteamUserStats;
            List<AchievementRow> rows = new(schema.Achievements.Count);

            foreach (var definition in schema.Achievements)
            {
                if (string.IsNullOrEmpty(definition.Id) == true)
                {
                    continue;
                }

                if (stats.GetAchievementAndUnlockTime(definition.Id, out var isUnlocked, out var unlockTime) == false)
                {
                    continue;
                }

                var unlockedAt = isUnlocked == true && unlockTime > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime
                    : (DateTime?)null;

                rows.Add(new AchievementRow(definition, isUnlocked, unlockedAt));
            }

            return rows;
        }

        public List<StatRow> ReadStatistics(GameStatsSchema schema)
        {
            var stats = this._steam.Client.SteamUserStats;
            List<StatRow> rows = new(schema.Stats.Count);

            foreach (var definition in schema.Stats)
            {
                if (string.IsNullOrEmpty(definition.Id) == true)
                {
                    continue;
                }

                if (definition is IntegerStatDefinition &&
                    stats.GetStatValue(definition.Id, out int intValue) == true)
                {
                    rows.Add(StatRow.Integer(definition, intValue));
                }
                else if (definition is FloatStatDefinition &&
                         stats.GetStatValue(definition.Id, out float floatValue) == true)
                {
                    rows.Add(StatRow.Float(definition, floatValue));
                }
            }

            return rows;
        }

        /// <summary>
        /// Writes every changed row and commits. Achievements Steam refuses are counted,
        /// not fatal: protected ones are worth attempting and losing is the normal outcome.
        /// </summary>
        public CommitResult Commit(IEnumerable<AchievementRow> achievements, IEnumerable<StatRow> statistics)
        {
            var stats = this._steam.Client.SteamUserStats;
            int written = 0;
            int refused = 0;

            foreach (var row in achievements)
            {
                if (row.IsModified == false)
                {
                    continue;
                }

                if (stats.SetAchievement(row.Id, row.IsUnlocked) == true)
                {
                    written++;
                }
                else
                {
                    refused++;
                }
            }

            int statsWritten = 0;
            foreach (var row in statistics)
            {
                if (row.IsModified == false)
                {
                    continue;
                }

                var ok = row.Kind == StatKind.Integer
                    ? stats.SetStatValue(row.Id, row.AsInteger)
                    : stats.SetStatValue(row.Id, row.AsFloat);

                if (ok == true)
                {
                    statsWritten++;
                }
                else
                {
                    refused++;
                }
            }

            if (written + statsWritten > 0 && stats.StoreStats() == false)
            {
                return new CommitResult(0, 0, written + statsWritten + refused);
            }

            return new CommitResult(written, statsWritten, refused);
        }

        public bool ResetEverything(bool achievementsToo)
        {
            return this._steam.Client.SteamUserStats.ResetAllStats(achievementsToo);
        }

        private void OnStatsReceived(APITypes.UserStatsReceived param)
        {
            var pending = this._pending;
            if (pending == null || param.GameId != this._appId)
            {
                return;
            }

            this._pending = null;
            pending.TrySetResult(param.Result);
        }
    }
}
