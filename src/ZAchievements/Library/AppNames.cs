using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ZAchievements.Infra;

namespace ZAchievements.Library
{
    /// <summary>
    /// Titles for apps this account has no licence for. Steam only keeps app data
    /// for what it needs, so those names come from Valve's public app list instead,
    /// cached on disk and read as a stream — the file is tens of megabytes.
    /// </summary>
    internal static class AppNames
    {
        private static readonly Uri Source = new("https://api.steampowered.com/ISteamApps/GetAppList/v2/");
        private static readonly TimeSpan Freshness = TimeSpan.FromDays(7);

        /// <summary>Names for the wanted ids only; empty when the list cannot be had.</summary>
        public static async Task<Dictionary<uint, string>> ForAsync(HashSet<uint> wanted)
        {
            var bytes = await BytesAsync().ConfigureAwait(false);
            return bytes == null ? new Dictionary<uint, string>() : Extract(bytes, wanted);
        }

        private static async Task<byte[]> BytesAsync()
        {
            var path = Path.Combine(AppPaths.Data, "applist.json");

            try
            {
                if (File.Exists(path) == true &&
                    DateTime.UtcNow - File.GetLastWriteTimeUtc(path) <= Freshness)
                {
                    return await File.ReadAllBytesAsync(path).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
                // Fall through to the download.
            }

            try
            {
                using HttpClient client = new(new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                })
                {
                    Timeout = TimeSpan.FromSeconds(90),
                };

                client.DefaultRequestHeaders.Add("User-Agent", AppPaths.UserAgent);
                var bytes = await client.GetByteArrayAsync(Source).ConfigureAwait(false);

                await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
                return bytes;
            }
            catch (Exception)
            {
                return TryStale(path);
            }
        }

        private static byte[] TryStale(string path)
        {
            try
            {
                return File.Exists(path) == true ? File.ReadAllBytes(path) : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static Dictionary<uint, string> Extract(byte[] json, HashSet<uint> wanted)
        {
            Dictionary<uint, string> names = new(wanted.Count);
            Utf8JsonReader reader = new(json);

            uint appId = 0;
            bool haveId = false;

            while (reader.Read() == true)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                if (reader.ValueTextEquals("appid") == true)
                {
                    reader.Read();
                    haveId = reader.TryGetUInt32(out appId);
                    continue;
                }

                if (reader.ValueTextEquals("name") == false)
                {
                    continue;
                }

                reader.Read();
                if (haveId == true && wanted.Contains(appId) == true)
                {
                    var name = reader.GetString();
                    if (string.IsNullOrWhiteSpace(name) == false)
                    {
                        names[appId] = name;
                    }
                }

                haveId = false;
            }

            return names;
        }
    }
}
