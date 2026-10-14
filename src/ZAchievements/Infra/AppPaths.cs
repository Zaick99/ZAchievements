using System;
using System.IO;

namespace ZAchievements.Infra
{
    internal static class AppPaths
    {
        public const string UserAgent = "ZAchievements/8.0";

        public static string Data { get; } = CreateDataFolder();

        private static string CreateDataFolder()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZAchievements");

            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
