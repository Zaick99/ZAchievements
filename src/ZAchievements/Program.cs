using System;
using System.Globalization;

namespace ZAchievements
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--unlock-all")
            {
                return uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var headlessId)
                    ? Achievements.UnlockAllTask.Run(headlessId)
                    : 2;
            }

            uint appId = 0;
            if (args.Length == 1 &&
                uint.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out appId) == false)
            {
                return 2;
            }

            var app = new App();
            app.InitializeComponent();
            return app.Run(appId);
        }
    }
}
