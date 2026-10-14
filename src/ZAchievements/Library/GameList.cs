using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.XPath;
using ZAchievements.Infra;

namespace ZAchievements.Library
{
    /// <summary>
    /// The shared catalogue of Steam app ids and their kind (normal, demo, mod, junk).
    /// Kept on disk between runs: two megabytes of XML is not worth re-downloading
    /// every launch, and a stale copy beats no library at all when the host is down.
    /// </summary>
    internal static class GameList
    {
        private static readonly Uri Source = new("https://gib.me/sam/games.xml");
        private static readonly TimeSpan Freshness = TimeSpan.FromHours(12);

        public static async Task<List<(uint Id, string Kind)>> LoadAsync()
        {
            var path = Path.Combine(AppPaths.Data, "games.xml");

            var cached = ReadIfFresh(path);
            if (cached != null)
            {
                return Parse(cached);
            }

            var downloaded = await DownloadAsync().ConfigureAwait(false);
            if (downloaded != null)
            {
                await Save(path, downloaded).ConfigureAwait(false);
                return Parse(downloaded);
            }

            var stale = ReadIfFresh(path, ignoreAge: true);
            return stale != null ? Parse(stale) : new List<(uint, string)>();
        }

        private static byte[] ReadIfFresh(string path, bool ignoreAge = false)
        {
            try
            {
                if (File.Exists(path) == false)
                {
                    return null;
                }

                if (ignoreAge == false && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > Freshness)
                {
                    return null;
                }

                return File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static async Task<byte[]> DownloadAsync()
        {
            try
            {
                using HttpClient client = new(new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                })
                {
                    Timeout = TimeSpan.FromSeconds(30),
                };

                client.DefaultRequestHeaders.Add("User-Agent", AppPaths.UserAgent);
                return await client.GetByteArrayAsync(Source).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task Save(string path, byte[] bytes)
        {
            try
            {
                await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Losing the cache only costs a download next time.
            }
        }

        private static List<(uint Id, string Kind)> Parse(byte[] xml)
        {
            List<(uint, string)> games = new(capacity: 1 << 16);

            try
            {
                using MemoryStream stream = new(xml, false);
                var navigator = new XPathDocument(stream).CreateNavigator();
                var nodes = navigator.Select("/games/game");

                while (nodes.MoveNext() == true)
                {
                    var kind = nodes.Current.GetAttribute("type", "");
                    games.Add((
                        (uint)nodes.Current.ValueAsLong,
                        string.IsNullOrEmpty(kind) == true ? "normal" : kind));
                }
            }
            catch (Exception)
            {
                return games;
            }

            return games;
        }
    }
}
