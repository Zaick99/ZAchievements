using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ZAchievements.Infra;
using ZAchievements.Library;
using ZAchievements.Media;
using ZAchievements.Steam;
using ZAchievements.Steam.Callbacks;
using APITypes = ZAchievements.Steam.Types;

namespace ZAchievements.Views
{
    internal partial class LibraryWindow : ShellWindow
    {
        private const double CardWidth = 196;
        private const double CardGap = 12;
        private const double ScrollbarWidth = 10;
        private const int CapsuleDecodeWidth = 231;

        private readonly SteamConnection _steam;
        private readonly GameScan _scan;
        private readonly AppDataChanged _appDataChanged;

        private readonly List<GameEntry> _library = new();

        private IReadOnlyList<(uint Id, string Kind)> _catalogue = Array.Empty<(uint, string)>();
        private HashSet<uint> _freeApps;
        private CancellationTokenSource _bulkStop;
        private int _columns = 1;
        private int _capsulesInFlight;
        private bool _isScanning;
        private bool _knowsUnowned;

        public LibraryWindow(SteamConnection steam)
        {
            this._steam = steam;
            this._scan = new GameScan(steam);

            this.InitializeComponent();

            this._appDataChanged = steam.Client.CreateAndRegisterCallback<AppDataChanged>();
            this._appDataChanged.OnRun += this.OnAppDataChanged;

            steam.StartPump();
            this.Loaded += this.OnFirstShow;
        }

        private async void OnFirstShow(object sender, RoutedEventArgs e)
        {
            this.Loaded -= this.OnFirstShow;
            await this.ScanAsync();
        }

        // ============================ library scan ============================

        private async Task ScanAsync()
        {
            if (this._isScanning == true)
            {
                return;
            }

            var wantUnowned = this.WantUnowned.IsChecked == true;

            this._isScanning = true;
            this.ReloadButton.IsEnabled = false;
            this.ScanBar.Visibility = Visibility.Visible;
            this.ScanBar.IsIndeterminate = true;
            this.StatusText.Text = "Fetching the shared game list…";

            if (this._catalogue.Count == 0)
            {
                this._catalogue = await GameList.LoadAsync();
            }

            Dictionary<uint, string> spareNames = null;
            if (wantUnowned == true)
            {
                this.StatusText.Text = "Fetching titles for the rest of the Steam catalogue…";
                spareNames = await AppNames.ForAsync(this._catalogue.Select(entry => entry.Id).ToHashSet());
            }

            this.ScanBar.IsIndeterminate = false;
            this.StatusText.Text = wantUnowned == true
                ? "Reading the whole Steam catalogue…"
                : "Checking which games you own…";

            Progress<ScanProgress> progress = new(step =>
            {
                this.ScanBar.Value = step.Total == 0 ? 0 : 100.0 * step.Checked / step.Total;
                this.StatusText.Text =
                    $"Reading the catalogue… {step.Found:N0} games in {step.Checked:N0} of {step.Total:N0}";
            });

            var found = this._catalogue.Count == 0
                ? new List<GameEntry>()
                : await this._scan.RunAsync(this._catalogue, wantUnowned, spareNames, progress);

            this._library.Clear();
            this._library.AddRange(found.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase));
            this._knowsUnowned = wantUnowned;

            if (this._library.Count == 0 && this._catalogue.Count == 0)
            {
                this.Fallback();
            }

            this.MarkFreeGames();

            this.ScanBar.Visibility = Visibility.Collapsed;
            this.ReloadButton.IsEnabled = true;
            this._isScanning = false;
            this.Rebuild();
        }

        /// <summary>
        /// The store's free listing is fetched once and kept for a week; entries are
        /// recreated by every scan, so the flag is stamped on again afterwards.
        /// </summary>
        private async Task<bool> LoadFreeGamesAsync()
        {
            if (this._freeApps != null)
            {
                return true;
            }

            this.ScanBar.Visibility = Visibility.Visible;
            this.ScanBar.IsIndeterminate = false;
            this.StatusText.Text = "Asking the store which games are free…";

            Progress<(int Done, int Total)> progress = new(step =>
            {
                this.ScanBar.Value = step.Total == 0 ? 0 : 100.0 * step.Done / step.Total;
                this.StatusText.Text = $"Asking the store which games are free… page {step.Done:N0} of {step.Total:N0}";
            });

            this._freeApps = await FreeApps.LoadAsync(progress);
            this.ScanBar.Visibility = Visibility.Collapsed;

            if (this._freeApps.Count == 0)
            {
                this._freeApps = null;
                Dialog.Error(
                    this,
                    "The store didn't answer",
                    "ZAchievements could not read the list of free games. Check your connection and try again.");
                return false;
            }

            this.MarkFreeGames();
            return true;
        }

        private void MarkFreeGames()
        {
            if (this._freeApps == null)
            {
                return;
            }

            foreach (var game in this._library)
            {
                game.IsFree = this._freeApps.Contains(game.Id);
            }
        }

        /// <summary>With no catalogue there is still Spacewar, which every account owns.</summary>
        private void Fallback() => this._library.Add(this._scan.Single(480));

        private void OnAppDataChanged(APITypes.AppDataChanged info)
        {
            if (info.Result == false)
            {
                return;
            }

            var game = this._library.FirstOrDefault(entry => entry.Id == info.Id);
            if (game != null && game.Capsule == null)
            {
                this.LoadCapsule(game);
            }
        }

        // ============================ filtering ============================

        private IEnumerable<GameEntry> Filtered()
        {
            var terms = SearchText.Terms(this.SearchField.Text);
            var showUnowned = this.WantUnowned.IsChecked == true;
            var freeOnly = this.WantFree.IsChecked == true;

            HashSet<string> kinds = new();
            Keep(this.WantGames, "normal");
            Keep(this.WantDemos, "demo");
            Keep(this.WantMods, "mod");
            Keep(this.WantJunk, "junk");

            return this._library.Where(game =>
                (game.IsOwned == true || showUnowned == true) &&
                (freeOnly == false || game.IsFree == true) &&
                (kinds.Contains(game.Kind) == true || IsUnclassified(game.Kind) == true) &&
                game.Matches(terms) == true);

            void Keep(ToggleButton toggle, string kind)
            {
                if (toggle.IsChecked == true)
                {
                    kinds.Add(kind);
                }
            }

            bool IsUnclassified(string kind)
            {
                return kind != "normal" && kind != "demo" && kind != "mod" && kind != "junk";
            }
        }

        private void Rebuild()
        {
            var shown = this.Filtered().ToArray();

            List<GameEntry[]> rows = new(1 + shown.Length / this._columns);
            for (int start = 0; start < shown.Length; start += this._columns)
            {
                rows.Add(shown[start..Math.Min(start + this._columns, shown.Length)]);
            }

            // One reset beats thousands of collection-changed notifications.
            this.GameRows.ItemsSource = rows;

            var hasGames = this._library.Count > 0;
            this.EmptyState.Visibility = shown.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            this.EmptyHeadline.Text = hasGames == true ? "No match" : "No games found";
            this.EmptyHint.Text = hasGames == true
                ? "Loosen the filters, or clear the search box."
                : "Make sure Steam is signed in, then rescan.";

            this.CaptionNote.Text = hasGames == true ? $"· {this._library.Count:N0} games" : "";

            if (this._isScanning == false)
            {
                this.StatusText.Text = $"Showing {shown.Length:N0} of {this._library.Count:N0} games";
            }

            this.UnlockEverythingButton.IsEnabled = shown.Length > 0;
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                this.Rebuild();
            }
        }

        private void OnSearchKey(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            var typed = this.SearchField.Text.Trim();
            if (uint.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) == false)
            {
                return;
            }

            e.Handled = true;

            if (this._library.Any(game => game.Id == appId) == false)
            {
                var added = this._scan.Single(appId);
                this._library.Add(added);
                this._library.Sort((left, right) =>
                    string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));

                if (added.IsOwned == false)
                {
                    this.WantUnowned.IsChecked = true;
                }
            }

            this.SearchField.Text = "";
            this.Rebuild();
        }

        // The pills raise Checked while the window is still being parsed, when the
        // controls further down the file do not exist yet.
        private void OnFilterChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                this.Rebuild();
            }
        }

        private async void OnUnownedFilterChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == false)
            {
                return;
            }

            // The unowned half of the catalogue is only read once it is asked for.
            if (this.WantUnowned.IsChecked == true && this._knowsUnowned == false)
            {
                await this.ScanAsync();
                return;
            }

            this.Rebuild();
        }

        private async void OnFreeFilterChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == false)
            {
                return;
            }

            if (this.WantFree.IsChecked == true && await this.LoadFreeGamesAsync() == false)
            {
                this.WantFree.IsChecked = false;
                return;
            }

            this.Rebuild();
        }

        private void OnReload(object sender, RoutedEventArgs e) => _ = this.ScanAsync();

        private void OnListResized(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged == false)
            {
                return;
            }

            // The last card in a row carries a trailing gap that costs nothing, so it is added back.
            var usable = e.NewSize.Width - ScrollbarWidth + CardGap;
            var columns = Math.Max(1, (int)(usable / (CardWidth + CardGap)));
            if (columns == this._columns)
            {
                return;
            }

            this._columns = columns;
            this.Rebuild();
        }

        // ============================ capsules ============================

        private void OnCardRealized(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is GameEntry game && game.Capsule == null)
            {
                this.LoadCapsule(game);
            }
        }

        private async void LoadCapsule(GameEntry game)
        {
            game.CapsuleUrl ??= SteamArt.Capsule(this._steam.Client, game.Id);
            if (game.CapsuleUrl == null)
            {
                return;
            }

            this._capsulesInFlight++;
            this.ShowDownloadCount();

            game.Capsule = await ImageCache.GetAsync(game.CapsuleUrl, CapsuleDecodeWidth);

            this._capsulesInFlight--;
            this.ShowDownloadCount();
        }

        private void ShowDownloadCount()
        {
            this.DownloadText.Text = this._capsulesInFlight > 0
                ? $"loading {this._capsulesInFlight} capsules…"
                : "";
        }

        // ============================ opening a game ============================

        private void OnCardClicked(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not GameEntry game)
            {
                return;
            }

            if (game.IsOwned == false && this.OfferLicence(game) == false)
            {
                return;
            }

            this.Open(Environment.ProcessPath, game.Id.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Steam checks ownership on its own servers, so an unowned game is a dead end
        /// until a licence exists. Free ones are a single click away in the client.
        /// </summary>
        private bool OfferLicence(GameEntry game)
        {
            if (game.IsFree == true)
            {
                var claim = Dialog.Confirm(
                    this,
                    "Not in your library yet",
                    $"{game.Name} is free on Steam. Add it from the Steam client first — Steam refuses " +
                    "achievement writes for games the account has no licence for.",
                    "Add it in Steam");

                if (claim == true)
                {
                    this.Open($"steam://install/{game.Id.ToString(CultureInfo.InvariantCulture)}", null);
                    return false;
                }

                return false;
            }

            return Dialog.Confirm(
                this,
                "Not in your library",
                $"Your account has no licence for {game.Name}. ZAchievements can open its manager, " +
                "but Steam will almost certainly refuse every write.",
                "Open it anyway");
        }

        private void Open(string target, string argument)
        {
            try
            {
                ProcessStartInfo start = new(target) { UseShellExecute = argument == null };
                if (argument != null)
                {
                    start.ArgumentList.Add(argument);
                }

                Process.Start(start);
            }
            catch (Exception error)
            {
                Dialog.Error(this, "Could not open that", error.Message);
            }
        }

        // ============================ claiming ============================

        private async void OnClaimFree(object sender, RoutedEventArgs e)
        {
            this.ClaimFreeButton.IsEnabled = false;
            var ready = await this.LoadFreeGamesAsync();
            this.ClaimFreeButton.IsEnabled = true;

            if (ready == false)
            {
                return;
            }

            new ClaimWindow(this._steam, this._freeApps) { Owner = this }.ShowDialog();

            // Licences acquired in there change what the library owns.
            await this.ScanAsync();
        }

        // ============================ bulk unlock ============================

        private async void OnUnlockEverything(object sender, RoutedEventArgs e)
        {
            var games = this.Filtered().ToArray();
            if (games.Length == 0)
            {
                return;
            }

            var unowned = games.Count(game => game.IsOwned == false);
            var warning = unowned == 0
                ? ""
                : $"\n\n{unowned:N0} of them are not in your library. ZAchievements will still try, " +
                  "but Steam validates ownership on its own servers and usually refuses those.";

            var confirmed = Dialog.Confirm(
                this,
                "Unlock every achievement?",
                $"ZAchievements will walk through {games.Length:N0} games and unlock every achievement " +
                "it is allowed to touch. Steam keeps the unlock dates, so this cannot be undone from here." +
                warning,
                $"Unlock {games.Length:N0} games",
                danger: true);

            if (confirmed == true)
            {
                await this.RunBulkAsync(games);
            }
        }

        private async Task RunBulkAsync(IReadOnlyList<GameEntry> games)
        {
            this._bulkStop = new CancellationTokenSource();
            this.BulkOverlay.Visibility = Visibility.Visible;
            this.BulkStopButton.IsEnabled = true;
            this.BulkBar.Maximum = games.Count;
            this.BulkBar.Value = 0;

            int unlocked = 0;
            int done = 0;
            int refused = 0;

            foreach (var game in games)
            {
                if (this._bulkStop.IsCancellationRequested == true)
                {
                    break;
                }

                this.BulkGame.Text = game.Name;
                this.BulkCount.Text = $"{done:N0} of {games.Count:N0} games";
                this.BulkTally.Text = $"{unlocked:N0} achievements unlocked";

                var result = await BulkUnlock.RunOneAsync(game.Id, this._bulkStop.Token);
                if (result.HasValue == true)
                {
                    unlocked += result.Value;
                }
                else
                {
                    refused++;
                }

                done++;
                this.BulkBar.Value = done;
            }

            this.BulkOverlay.Visibility = Visibility.Collapsed;
            this._bulkStop.Dispose();
            this._bulkStop = null;

            Dialog.Note(
                this,
                "Done",
                $"{unlocked:N0} achievements unlocked across {done:N0} games." +
                (refused > 0 ? $"\n{refused:N0} games had nothing ZAchievements could change." : ""));
        }

        private void OnStopBulk(object sender, RoutedEventArgs e)
        {
            this.BulkStopButton.IsEnabled = false;
            this.BulkGame.Text = "Stopping after this game…";
            this._bulkStop?.Cancel();
        }

        // ============================ chrome ============================

        private void OnMaximize(object sender, RoutedEventArgs e) => this.ToggleMaximized();
    }
}
