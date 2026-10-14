/* Derived from SteamAchievementManager by Rick (gibbed), zlib licensed.
 * Altered: extracted from SAM.Game.Manager into a standalone loader.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ZAchievements.Steam.Types;

namespace ZAchievements.Schema
{
    /// <summary>
    /// The achievement and statistic definitions Steam caches on disk for one game
    /// (<c>appcache/stats/UserGameStatsSchema_&lt;appid&gt;.bin</c>).
    /// </summary>
    internal sealed class GameStatsSchema
    {
        private GameStatsSchema(
            IReadOnlyList<AchievementDefinition> achievements,
            IReadOnlyList<StatDefinition> stats)
        {
            this.Achievements = achievements;
            this.Stats = stats;
        }

        public IReadOnlyList<AchievementDefinition> Achievements { get; }

        public IReadOnlyList<StatDefinition> Stats { get; }

        /// <summary>Returns null when Steam has no cached schema for this game.</summary>
        public static GameStatsSchema Load(uint appId, string language)
        {
            var root = ReadCachedSchema(appId);
            if (root == null)
            {
                return null;
            }

            var stats = root[appId.ToString(CultureInfo.InvariantCulture)]["stats"];
            if (stats.Valid == false || stats.Children == null)
            {
                return null;
            }

            List<AchievementDefinition> achievements = new();
            List<StatDefinition> statistics = new();

            foreach (var stat in stats.Children.Where(s => s.Valid == true))
            {
                switch (ReadStatType(stat))
                {
                    case UserStatType.Integer:
                    {
                        statistics.Add(new IntegerStatDefinition(
                            stat["name"].AsString(""),
                            DisplayName(stat, language),
                            stat["incrementonly"].AsBoolean(false),
                            stat["permission"].AsInteger(0)));
                        break;
                    }

                    case UserStatType.Float:
                    case UserStatType.AverageRate:
                    {
                        statistics.Add(new FloatStatDefinition(
                            stat["name"].AsString(""),
                            DisplayName(stat, language),
                            stat["incrementonly"].AsBoolean(false),
                            stat["permission"].AsInteger(0)));
                        break;
                    }

                    case UserStatType.Achievements:
                    case UserStatType.GroupAchievements:
                    {
                        achievements.AddRange(ReadAchievementBits(stat, language));
                        break;
                    }
                }
            }

            return new GameStatsSchema(achievements, statistics);
        }

        private static KeyValue ReadCachedSchema(uint appId)
        {
            var installPath = Steam.SteamLibrary.GetInstallPath();
            if (string.IsNullOrEmpty(installPath) == true)
            {
                return null;
            }

            var path = Path.Combine(
                installPath,
                "appcache",
                "stats",
                $"UserGameStatsSchema_{appId.ToString(CultureInfo.InvariantCulture)}.bin");

            return File.Exists(path) == false ? null : KeyValue.LoadAsBinary(path);
        }

        private static UserStatType ReadStatType(KeyValue stat)
        {
            var typeNode = stat["type"];

            if (typeNode.Valid == true &&
                typeNode.Type == KeyValueType.String &&
                Enum.TryParse((string)typeNode.Value, true, out UserStatType named) == true &&
                named != UserStatType.Invalid)
            {
                return named;
            }

            var typeIntNode = stat["type_int"];
            var raw = typeIntNode.Valid == true ? typeIntNode.AsInteger(0) : typeNode.AsInteger(0);
            return (UserStatType)raw;
        }

        private static IEnumerable<AchievementDefinition> ReadAchievementBits(KeyValue stat, string language)
        {
            if (stat.Children == null)
            {
                yield break;
            }

            var bitGroups = stat.Children.Where(
                child => string.Equals(child.Name, "bits", StringComparison.OrdinalIgnoreCase) == true);

            foreach (var bits in bitGroups)
            {
                if (bits.Valid == false || bits.Children == null)
                {
                    continue;
                }

                foreach (var bit in bits.Children)
                {
                    var id = bit["name"].AsString("");
                    var display = bit["display"];

                    yield return new AchievementDefinition(
                        id,
                        Localized(display["name"], language, id),
                        Localized(display["desc"], language, ""),
                        display["icon"].AsString(""),
                        display["icon_gray"].AsString(""),
                        display["hidden"].AsBoolean(false),
                        bit["permission"].AsInteger(0));
                }
            }
        }

        private static string DisplayName(KeyValue stat, string language)
        {
            var id = stat["name"].AsString("");
            return Localized(stat["display"]["name"], language, id);
        }

        private static string Localized(KeyValue node, string language, string fallback)
        {
            var localized = node[language].AsString("");
            if (string.IsNullOrEmpty(localized) == false)
            {
                return localized;
            }

            if (language != "english")
            {
                var english = node["english"].AsString("");
                if (string.IsNullOrEmpty(english) == false)
                {
                    return english;
                }
            }

            var plain = node.AsString("");
            return string.IsNullOrEmpty(plain) == false ? plain : fallback;
        }
    }
}
