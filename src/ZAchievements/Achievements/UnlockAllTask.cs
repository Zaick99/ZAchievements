using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using ZAchievements.Steam;

namespace ZAchievements.Achievements
{
    /// <summary>
    /// Headless "unlock everything in this game", the unit of work the library's
    /// bulk pass spawns once per game. Reports a single line on stdout:
    /// <c>OK &lt;unlocked&gt; &lt;refused&gt;</c> or <c>ERR &lt;reason&gt;</c>.
    /// </summary>
    internal static class UnlockAllTask
    {
        private static readonly TimeSpan StatsTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(40);

        public static int Run(uint appId)
        {
            if (SteamConnection.TryOpen(appId, out var steam, out var problem) == false)
            {
                return Fail(problem);
            }

            using (steam)
            {
                var session = new AchievementSession(steam, appId);
                var request = session.RequestAsync();

                var clock = Stopwatch.StartNew();
                while (request.IsCompleted == false)
                {
                    if (clock.Elapsed > StatsTimeout)
                    {
                        return Fail("Steam never answered with this game's stats");
                    }

                    steam.Pump();
                    Thread.Sleep(PumpInterval);
                }

                if (request.Result != 1)
                {
                    return Fail($"Steam refused the stats request (code {request.Result})");
                }

                var schema = session.LoadSchema();
                if (schema == null)
                {
                    return Fail("Steam has no cached achievement schema for this game");
                }

                var rows = session.ReadAchievements(schema);
                foreach (var row in rows)
                {
                    row.IsUnlocked = true;
                }

                var result = session.Commit(rows, Array.Empty<StatRow>());
                Console.Out.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "OK {0} {1}",
                    result.Achievements,
                    result.Refused));
                return 0;
            }
        }

        private static int Fail(string reason)
        {
            Console.Out.WriteLine("ERR " + reason.Replace('\n', ' '));
            return 1;
        }
    }
}
