using System;
using System.IO;
using System.Windows.Threading;

namespace ZAchievements.Steam
{
    /// <summary>
    /// A live connection to the running Steam client, plus the callback pump
    /// that delivers its asynchronous answers.
    /// </summary>
    internal sealed class SteamConnection : IDisposable
    {
        private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(80);

        private readonly Client _client;
        private DispatcherTimer _pump;

        private SteamConnection(Client client)
        {
            this._client = client;
        }

        public Client Client => this._client;

        public static bool TryOpen(uint appId, out SteamConnection connection, out string problem)
        {
            connection = null;

            var installPath = SteamLibrary.GetInstallPath();
            if (string.IsNullOrEmpty(installPath) == true)
            {
                problem = "Steam isn't installed, or its registry entry is missing. " +
                          "Install Steam, then open ZAchievements again.";
                return false;
            }

            if (PathsMatch(installPath, AppContext.BaseDirectory) == true)
            {
                problem = "Move ZAchievements out of the Steam folder and run it from there instead.";
                return false;
            }

            var client = new Client();
            try
            {
                client.Initialize(appId);
            }
            catch (ClientInitializeException e)
            {
                client.Dispose();
                problem = Explain(e);
                return false;
            }
            catch (DllNotFoundException)
            {
                client.Dispose();
                problem = "Steam's client library could not be loaded. " +
                          "Verify your Steam installation, then open ZAchievements again.";
                return false;
            }

            connection = new SteamConnection(client);
            problem = null;
            return true;
        }

        /// <summary>Delivers Steam callbacks on the UI thread until disposed.</summary>
        public void StartPump()
        {
            if (this._pump != null)
            {
                return;
            }

            this._pump = new DispatcherTimer(DispatcherPriority.Background) { Interval = PumpInterval };
            this._pump.Tick += (_, __) => this._client.RunCallbacks(false);
            this._pump.Start();
        }

        public void Pump() => this._client.RunCallbacks(false);

        public void Dispose()
        {
            this._pump?.Stop();
            this._pump = null;
            this._client.Dispose();
        }

        private static string Explain(ClientInitializeException e)
        {
            if (e.Failure == ClientInitializeFailure.ConnectToGlobalUser)
            {
                return "Steam isn't running, or it won't let this account in right now.\n\n" +
                       "Start Steam and sign in, then open ZAchievements again. If the game came " +
                       "from Family Sharing, it stays locked while the lender is playing.";
            }

            if (e.Failure == ClientInitializeFailure.AppIdMismatch)
            {
                return "Steam handed back a different game than the one requested. " +
                       "Close every other copy of ZAchievements, then try again.";
            }

            return string.IsNullOrEmpty(e.Message) == true
                ? "Steam isn't running. Start Steam and sign in, then open ZAchievements again."
                : "Steam isn't running. Start Steam and sign in, then open ZAchievements again.\n\n" +
                  "(" + e.Message + ")";
        }

        private static bool PathsMatch(string left, string right)
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
