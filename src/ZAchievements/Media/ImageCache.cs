using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZAchievements.Infra;

namespace ZAchievements.Media
{
    /// <summary>
    /// Capsules and achievement icons, remembered in memory for the session and
    /// on disk across runs: every artwork URL Steam hands out is content-addressed,
    /// so a cached file can never go stale.
    /// </summary>
    internal static class ImageCache
    {
        private const int MaxParallelDownloads = 8;

        private static readonly HttpClient Http = CreateClient();
        private static readonly SemaphoreSlim Gate = new(MaxParallelDownloads);
        private static readonly ConcurrentDictionary<string, Task<ImageSource>> Live = new();
        private static readonly string Folder = CreateFolder();

        public static Task<ImageSource> GetAsync(string url, int decodeWidth)
        {
            if (string.IsNullOrEmpty(url) == true)
            {
                return Task.FromResult<ImageSource>(null);
            }

            return Live.GetOrAdd(url, key => FetchAsync(key, decodeWidth));
        }

        private static async Task<ImageSource> FetchAsync(string url, int decodeWidth)
        {
            var path = Path.Combine(Folder, FileNameFor(url));

            var cached = await Task.Run(() => Decode(ReadFile(path), decodeWidth)).ConfigureAwait(false);
            if (cached != null)
            {
                return cached;
            }

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
                var image = Decode(bytes, decodeWidth);
                if (image == null)
                {
                    return null;
                }

                await WriteFileAsync(path, bytes).ConfigureAwait(false);
                return image;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Gate.Release();
            }
        }

        private static ImageSource Decode(byte[] bytes, int decodeWidth)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            try
            {
                BitmapImage image = new();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = decodeWidth;
                image.StreamSource = new MemoryStream(bytes, false);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[] ReadFile(string path)
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

        private static async Task WriteFileAsync(string path, byte[] bytes)
        {
            try
            {
                await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // A cache that cannot be written is still a working cache.
            }
        }

        private static string FileNameFor(string url)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
            var name = Convert.ToHexString(hash, 0, 10);
            var extension = Path.GetExtension(new Uri(url).AbsolutePath);
            return extension.Length is > 1 and <= 5 ? name + extension : name + ".img";
        }

        private static string CreateFolder()
        {
            var folder = Path.Combine(AppPaths.Data, "images");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static HttpClient CreateClient()
        {
            HttpClient client = new(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                MaxConnectionsPerServer = MaxParallelDownloads,
            })
            {
                Timeout = TimeSpan.FromSeconds(20),
            };

            client.DefaultRequestHeaders.Add("User-Agent", AppPaths.UserAgent);
            return client;
        }
    }
}
