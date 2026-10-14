using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ZAchievements.Infra;

namespace ZAchievements.Library
{
    /// <summary>
    /// Every app the Steam store lists at no cost. There is no public API for this,
    /// so the store's own search is paged through once and kept on disk for a week.
    /// </summary>
    internal static partial class FreeApps
    {
        private const int PerPage = 100;
        private const int ParallelPages = 4;

        private static readonly TimeSpan Freshness = TimeSpan.FromDays(7);

        public static async Task<HashSet<uint>> LoadAsync(IProgress<(int Done, int Total)> progress)
        {
            var cached = ReadCache();
            if (cached != null)
            {
                return cached;
            }

            using var client = CreateClient();

            var first = await PageAsync(client, 0).ConfigureAwait(true);
            if (first.Total == 0)
            {
                return new HashSet<uint>();
            }

            HashSet<uint> free = new(first.Ids);
            var pages = Enumerable.Range(1, (first.Total - 1) / PerPage).ToArray();
            int done = 1;

            SemaphoreSlim gate = new(ParallelPages);
            var work = pages.Select(async page =>
            {
                await gate.WaitAsync().ConfigureAwait(true);
                try
                {
                    var result = await PageAsync(client, page * PerPage).ConfigureAwait(true);
                    lock (free)
                    {
                        foreach (var id in result.Ids)
                        {
                            free.Add(id);
                        }
                    }
                }
                finally
                {
                    gate.Release();
                    progress?.Report((++done, 1 + pages.Length));
                }
            });

            await Task.WhenAll(work).ConfigureAwait(true);

            WriteCache(free);
            return free;
        }

        private static async Task<(IReadOnlyList<uint> Ids, int Total)> PageAsync(HttpClient client, int start)
        {
            var url = "https://store.steampowered.com/search/results/" +
                      $"?maxprice=free&category1=998&infinite=1&start={start}&count={PerPage}";

            try
            {
                var json = await client.GetByteArrayAsync(url).ConfigureAwait(true);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var total = root.TryGetProperty("total_count", out var count) == true ? count.GetInt32() : 0;
                var html = root.TryGetProperty("results_html", out var markup) == true ? markup.GetString() : "";

                return (Ids(html), total);
            }
            catch (Exception)
            {
                return (Array.Empty<uint>(), 0);
            }
        }

        private static IReadOnlyList<uint> Ids(string html)
        {
            List<uint> ids = new(PerPage);
            if (string.IsNullOrEmpty(html) == true)
            {
                return ids;
            }

            foreach (Match match in AppIdPattern().Matches(html))
            {
                // Bundle rows carry several ids in one attribute.
                foreach (var part in match.Groups[1].Value.Split(','))
                {
                    if (uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) == true)
                    {
                        ids.Add(id);
                    }
                }
            }

            return ids;
        }

        private static HashSet<uint> ReadCache()
        {
            var path = CachePath;

            try
            {
                if (File.Exists(path) == false ||
                    DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > Freshness)
                {
                    return null;
                }

                HashSet<uint> free = new();
                foreach (var line in File.ReadLines(path))
                {
                    if (uint.TryParse(line, NumberStyles.None, CultureInfo.InvariantCulture, out var id) == true)
                    {
                        free.Add(id);
                    }
                }

                return free.Count > 0 ? free : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static void WriteCache(HashSet<uint> free)
        {
            try
            {
                File.WriteAllLines(CachePath, free.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            }
            catch (IOException)
            {
                // Losing the cache only costs another pass next week.
            }
        }

        private static string CachePath => Path.Combine(AppPaths.Data, "freeapps.txt");

        private static HttpClient CreateClient()
        {
            HttpClient client = new(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                MaxConnectionsPerServer = ParallelPages,
            })
            {
                Timeout = TimeSpan.FromSeconds(30),
            };

            client.DefaultRequestHeaders.Add("User-Agent", AppPaths.UserAgent);
            return client;
        }

        [GeneratedRegex("data-ds-appid=\"([0-9,]+)\"")]
        private static partial Regex AppIdPattern();
    }
}
