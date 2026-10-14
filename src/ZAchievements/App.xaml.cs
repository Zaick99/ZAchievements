using System;
using System.Windows;
using ZAchievements.Steam;
using ZAchievements.Views;

namespace ZAchievements
{
    public partial class App : Application
    {
        /// <summary>
        /// App id 0 opens the library; anything else opens that game directly.
        /// Steam binds one app id per process, so the library re-launches this
        /// same executable instead of switching app id in place.
        /// </summary>
        internal int Run(uint appId)
        {
            if (SteamConnection.TryOpen(appId, out var steam, out var problem) == false)
            {
                Dialog.Error(null, "Steam unavailable", problem);
                return 1;
            }

            using (steam)
            {
                Window window = appId == 0
                    ? new LibraryWindow(steam)
                    : new GameWindow(steam, appId);

                return this.Run(window);
            }
        }
    }
}
