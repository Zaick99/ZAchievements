using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ZAchievements.Infra;
using ZAchievements.Library;
using ZAchievements.Steam;

namespace ZAchievements.Views
{
    /// <summary>
    /// Adds free licences to the signed-in account. Steam has no client API for this,
    /// so the work happens inside Steam's own store page: the user signs in there and
    /// the requests go out from that origin, with ZAchievements never holding a token.
    /// </summary>
    internal partial class ClaimWindow : ShellWindow
    {
        private const int RequestSpacingMs = 2000;
        // One line every couple of seconds costs nothing, and a hard kill loses nothing.
        private const int LogBatch = 1;

        private static readonly TimeSpan SignInPoll = TimeSpan.FromSeconds(2);
        private static readonly Uri Store = new("https://store.steampowered.com/");
        private static readonly Uri SignIn = new("https://store.steampowered.com/login/");

        private readonly SteamConnection _steam;
        private readonly IReadOnlyCollection<uint> _freeApps;
        private readonly ClaimLog _log = new();
        private readonly List<uint> _pendingLog = new();

        private DispatcherTimer _signInWatch;
        private uint[] _queue = Array.Empty<uint>();
        private bool _isRunning;

        public ClaimWindow(SteamConnection steam, IReadOnlyCollection<uint> freeApps)
        {
            this._steam = steam;
            this._freeApps = freeApps;

            this.InitializeComponent();
            this.Loaded += this.OnFirstShow;
            this.Closing += this.OnClosing;
        }

        private async void OnFirstShow(object sender, RoutedEventArgs e)
        {
            this.Loaded -= this.OnFirstShow;

            if (await this.StartBrowserAsync() == false)
            {
                return;
            }

            this.Browser.CoreWebView2.WebMessageReceived += this.OnScriptMessage;

            // Steam sends an live session straight back to the store, so this lands
            // on the sign-in form only when there is nothing to resume.
            this.Browser.CoreWebView2.Navigate(SignIn.AbsoluteUri);

            this._signInWatch = new DispatcherTimer { Interval = SignInPoll };
            this._signInWatch.Tick += async (_, __) => await this.CheckSignInAsync();
            this._signInWatch.Start();
        }

        private async Task<bool> StartBrowserAsync()
        {
            try
            {
                var profile = Path.Combine(AppPaths.Data, "webview");
                Directory.CreateDirectory(profile);

                var environment = await CoreWebView2Environment.CreateAsync(null, profile);
                await this.Browser.EnsureCoreWebView2Async(environment);
                return true;
            }
            catch (Exception error)
            {
                this.StatusText.Text = "The browser component is missing.";
                Dialog.Error(
                    this,
                    "WebView2 is not available",
                    "Claiming free games needs the Microsoft Edge WebView2 runtime, which this " +
                    "machine does not have. Install it from Microsoft's site, then try again.\n\n" +
                    "(" + error.Message + ")");
                return false;
            }
        }

        // ============================ signing in ============================

        private async Task CheckSignInAsync()
        {
            // The store does not expose one reliable flag, so take the first signal
            // that only ever appears for a signed-in visitor.
            var answer = await this.Browser.CoreWebView2.ExecuteScriptAsync(
                """
                (function () {
                  try {
                    if (typeof g_steamID !== 'undefined' && g_steamID) { return String(g_steamID); }
                    if (document.getElementById('account_pulldown')) { return 'account'; }
                    if (document.querySelector('a[href*="/logout/"]')) { return 'logout'; }
                    if (document.querySelector('#global_actions .playerAvatar')) { return 'avatar'; }
                  } catch (e) { }
                  return '';
                })()
                """);

            var signedIn = string.IsNullOrEmpty(answer) == false &&
                           answer != "null" &&
                           answer.Trim('"').Length > 0;

            if (signedIn == false)
            {
                this.StatusText.Text = "Waiting for you to sign in to Steam…";
                return;
            }

            this._signInWatch.Stop();
            this.Headline.Text = "Signed in";
            this.PrepareQueue();
        }

        /// <summary>Recounts what is left. Never touches the headline: that reports the last run.</summary>
        private void PrepareQueue()
        {
            var apps = this._steam.Client.SteamApps008;

            this._queue = this._freeApps
                .Where(id => this._log.Knows(id) == false && apps.IsSubscribedApp(id) == false)
                .ToArray();

            this.Progress.Maximum = Math.Max(1, this._queue.Length);
            this.Progress.Value = 0;

            if (this._queue.Length == 0)
            {
                this.StatusText.Text = "Nothing left to claim.";
                this.StartButton.IsEnabled = false;
                return;
            }

            this.StatusText.Text =
                $"{Plural.Of(this._queue.Length, "free game", "free games")} to claim" +
                (this._log.Count > 0 ? $" · {this._log.Count:N0} already tried in earlier runs" : "");
            this.DetailText.Text =
                "Steam grants only a few dozen free licences an hour, so this runs for as long as it is " +
                "allowed, waits, and carries on. Leave it open, or come back and press Claim again.";
            this.StartButton.IsEnabled = true;
        }

        // ============================ running ============================

        private async void OnStart(object sender, RoutedEventArgs e)
        {
            if (this._isRunning == true || this._queue.Length == 0)
            {
                return;
            }

            this._isRunning = true;
            this.StartButton.IsEnabled = false;
            this.ForgetButton.IsEnabled = false;
            this.StopButton.IsEnabled = true;
            this.Headline.Text = "Claiming";

            await this.BackToStoreAsync();

            var ids = string.Join(",", this._queue.Select(id => id.ToString(CultureInfo.InvariantCulture)));

            await this.Browser.CoreWebView2.ExecuteScriptAsync(ClaimScript.Source);
            await this.Browser.CoreWebView2.ExecuteScriptAsync(
                $"window.__za.run([{ids}], {RequestSpacingMs.ToString(CultureInfo.InvariantCulture)})");
        }

        /// <summary>The requests are relative, so the page has to be on the store itself.</summary>
        private async Task BackToStoreAsync()
        {
            if (this.Browser.CoreWebView2.Source.StartsWith(Store.AbsoluteUri, StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }

            TaskCompletionSource arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void Landed(object sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                this.Browser.CoreWebView2.NavigationCompleted -= Landed;
                arrived.TrySetResult();
            }

            this.Browser.CoreWebView2.NavigationCompleted += Landed;
            this.Browser.CoreWebView2.Navigate(Store.AbsoluteUri);
            await arrived.Task;
        }

        private async void OnStop(object sender, RoutedEventArgs e)
        {
            this.StopButton.IsEnabled = false;
            this.StatusText.Text = "Stopping after the current game…";
            await this.Browser.CoreWebView2.ExecuteScriptAsync("window.__za.stop = true");
        }

        private void OnScriptMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            JsonElement message;
            try
            {
                message = JsonDocument.Parse(e.TryGetWebMessageAsString()).RootElement;
            }
            catch (JsonException)
            {
                return;
            }

            switch (Text(message, "kind"))
            {
                case "tick":
                {
                    this.Advance(message);
                    break;
                }

                case "wait":
                {
                    var minutes = Math.Max(1, Number(message, "seconds") / 60);
                    this.StatusText.Text =
                        $"Steam has stopped granting — its quota is spent. Waiting {minutes} minutes, " +
                        "then carrying on. You can leave this open.";
                    break;
                }

                case "sample":
                {
                    this.DetailText.Text = $"Steam answered: {Text(message, "detail")}";
                    break;
                }

                case "fatal":
                {
                    this.Finish("Could not start", Text(message, "text"));
                    break;
                }

                case "end":
                {
                    this.Close(message);
                    break;
                }
            }
        }

        private void Advance(JsonElement message)
        {
            var settled = message.TryGetProperty("settled", out var flag) == true &&
                          flag.ValueKind == JsonValueKind.True;

            if (settled == true &&
                message.TryGetProperty("id", out var id) == true &&
                id.TryGetUInt32(out var appId) == true)
            {
                this._pendingLog.Add(appId);
                if (this._pendingLog.Count >= LogBatch)
                {
                    this._log.Remember(this._pendingLog);
                    this._pendingLog.Clear();
                }
            }

            if (message.TryGetProperty("tally", out var tally) == false)
            {
                return;
            }

            var done = Number(tally, "done");
            this.Progress.Value = done;
            this.StatusText.Text =
                $"{done:N0} of {this._queue.Length:N0} · {Number(tally, "added"):N0} added · " +
                $"{Number(tally, "skipped"):N0} nothing free · {Number(tally, "blocked"):N0} refused by Steam";
        }

        private void Close(JsonElement message)
        {
            this._log.Remember(this._pendingLog);
            this._pendingLog.Clear();

            var tally = message.TryGetProperty("tally", out var found) ? found : default;
            var added = tally.ValueKind == JsonValueKind.Object ? Number(tally, "added") : 0;

            var reason = Text(message, "reason") switch
            {
                "stopped" => "Stopped.",
                "quota" => "Steam kept refusing after an hour of waiting: its free-licence quota is spent for " +
                           "now. Nothing is lost — press Claim them again later and the refused games are " +
                           "still in the queue.",
                _ => "Finished.",
            };

            this.Finish(Plural.Of(added, "game", "games") + " added", reason);
        }

        private void Finish(string headline, string detail)
        {
            this._isRunning = false;
            this.Headline.Text = headline;
            this.DetailText.Text = detail;
            this.StopButton.IsEnabled = false;
            this.ForgetButton.IsEnabled = true;
            this.PrepareQueue();
        }

        // ============================ odds and ends ============================

        private void OnForget(object sender, RoutedEventArgs e)
        {
            if (Dialog.Confirm(
                this,
                "Try everything again?",
                "ZAchievements will forget which apps it already offered to Steam and start the whole " +
                "list from the top next time.",
                "Forget and start over") == false)
            {
                return;
            }

            this._log.Forget();
            this.PrepareQueue();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this._signInWatch?.Stop();
            this._log.Remember(this._pendingLog);
            this._pendingLog.Clear();
        }

        private void OnMaximize(object sender, RoutedEventArgs e) => this.ToggleMaximized();

        private static string Text(JsonElement message, string name)
        {
            return message.TryGetProperty(name, out var value) == true && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : "";
        }

        private static int Number(JsonElement message, string name)
        {
            return message.TryGetProperty(name, out var value) == true && value.TryGetInt32(out var found) == true
                ? found
                : 0;
        }
    }
}
