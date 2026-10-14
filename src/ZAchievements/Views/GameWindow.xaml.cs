using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using ZAchievements.Achievements;
using ZAchievements.Infra;
using ZAchievements.Media;
using ZAchievements.Steam;

namespace ZAchievements.Views
{
    internal partial class GameWindow : ShellWindow
    {
        private static readonly TimeSpan StatsTimeout = TimeSpan.FromSeconds(20);
        private const int IconDecodeWidth = 64;

        private readonly SteamConnection _steam;
        private readonly uint _appId;
        private readonly AchievementSession _session;

        private readonly List<AchievementRow> _achievements = new();
        private readonly List<StatRow> _statistics = new();
        private readonly ObservableCollection<AchievementRow> _shownAchievements = new();
        private readonly ObservableCollection<StatRow> _shownStatistics = new();

        private int _iconsInFlight;

        public GameWindow(SteamConnection steam, uint appId)
        {
            this._steam = steam;
            this._appId = appId;
            this._session = new AchievementSession(steam, appId);

            this.InitializeComponent();

            this.AchievementList.ItemsSource = this._shownAchievements;
            this.StatList.ItemsSource = this._shownStatistics;
            this.Sections.SelectionChanged += this.OnSectionChanged;

            steam.StartPump();
            this.Loaded += this.OnFirstShow;
        }

        private async void OnFirstShow(object sender, RoutedEventArgs e)
        {
            this.Loaded -= this.OnFirstShow;

            this.Title = $"ZAchievements — {this._session.GameName}";
            this.GameTitle.Text = this._session.GameName;
            this.LoadArtwork();

            await this.ReloadAsync();
        }

        // ============================ loading ============================

        private async Task ReloadAsync()
        {
            this.AllowInput(false);
            this.Status("Asking Steam for your progress…");

            var request = this._session.RequestAsync();
            var finished = await Task.WhenAny(request, Task.Delay(StatsTimeout));

            if (finished != request)
            {
                this.Fail("Steam never answered", "Steam didn't send this game's stats. Try reloading.");
                return;
            }

            if (request.Result != 1)
            {
                this.Fail(
                    "Steam refused the request",
                    request.Result == 2
                        ? "Steam says this account doesn't own the game."
                        : $"Steam returned error code {request.Result}.");
                return;
            }

            var schema = this._session.LoadSchema();
            if (schema == null)
            {
                this.Fail(
                    "No achievement schema yet",
                    "Steam hasn't cached this game's achievement list. Launch the game once, " +
                    "let it reach the menu, then reload here.");
                return;
            }

            this.Adopt(this._session.ReadAchievements(schema), this._session.ReadStatistics(schema));
            this.AllowInput(true);
            this.Status($"{this._achievements.Count} achievements, {this._statistics.Count} statistics.");
        }

        private void Adopt(List<AchievementRow> achievements, List<StatRow> statistics)
        {
            foreach (var row in this._achievements)
            {
                row.PropertyChanged -= this.OnRowChanged;
            }

            this._achievements.Clear();
            this._achievements.AddRange(achievements);
            foreach (var row in this._achievements)
            {
                row.PropertyChanged += this.OnRowChanged;
            }

            this._statistics.Clear();
            this._statistics.AddRange(statistics);
            this.ApplyStatEditing();

            this.ApplyAchievementFilter();
            this.ApplyStatFilter();
            this.UpdateTally();
        }

        private void LoadArtwork()
        {
            var url = SteamArt.Capsule(this._steam.Client, this._appId);
            if (url == null)
            {
                return;
            }

            _ = this.ShowArtworkAsync(url);
        }

        private async Task ShowArtworkAsync(string url)
        {
            var art = await ImageCache.GetAsync(url, 460);
            this.CapsuleArt.Source = art;
            this.BannerArt.Source = art;
        }

        // ============================ filtering ============================

        private void ApplyAchievementFilter()
        {
            var terms = SearchText.Terms(this.AchievementSearch.Text);
            var wantLocked = this.ShowLocked.IsChecked == true;
            var wantUnlocked = this.ShowUnlocked.IsChecked == true;

            this._shownAchievements.Clear();
            foreach (var row in this._achievements)
            {
                var byState = (wantLocked == false && wantUnlocked == false) ||
                              (row.IsUnlocked == true ? wantUnlocked : wantLocked);

                if (byState == true && row.Matches(terms) == true)
                {
                    this._shownAchievements.Add(row);
                }
            }

            this.UpdateEmptyState();
        }

        private void ApplyStatFilter()
        {
            var terms = SearchText.Terms(this.StatSearch.Text);

            this._shownStatistics.Clear();
            foreach (var row in this._statistics.Where(row => row.Matches(terms) == true))
            {
                this._shownStatistics.Add(row);
            }

            this.UpdateEmptyState();
        }

        private void ApplyStatEditing()
        {
            var allowed = this.AllowStatEditing.IsChecked == true;
            foreach (var row in this._statistics)
            {
                row.IsEditable = allowed == true && row.IsProtected == false;
            }
        }

        private void UpdateEmptyState()
        {
            var onStats = this.StatList.Visibility == Visibility.Visible;
            var count = onStats == true ? this._shownStatistics.Count : this._shownAchievements.Count;
            var total = onStats == true ? this._statistics.Count : this._achievements.Count;

            this.EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            this.EmptyHeadline.Text = total == 0
                ? onStats == true ? "No statistics" : "No achievements"
                : "No match";
            this.EmptyHint.Text = total == 0
                ? "This game doesn't declare any."
                : "Loosen the filters, or clear the search box.";
        }

        private void OnSectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var onStats = this.Sections.SelectedIndex == 1;
            this.AchievementList.Visibility = onStats == true ? Visibility.Collapsed : Visibility.Visible;
            this.StatList.Visibility = onStats == true ? Visibility.Visible : Visibility.Collapsed;
            this.UpdateEmptyState();
        }

        private void OnAchievementFilterChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                this.ApplyAchievementFilter();
            }
        }

        private void OnStatFilterChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                this.ApplyStatFilter();
            }
        }

        private void OnLockedFilter(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == false)
            {
                return;
            }

            if (this.ShowLocked.IsChecked == true)
            {
                this.ShowUnlocked.IsChecked = false;
            }

            this.ApplyAchievementFilter();
        }

        private void OnUnlockedFilter(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == false)
            {
                return;
            }

            if (this.ShowUnlocked.IsChecked == true)
            {
                this.ShowLocked.IsChecked = false;
            }

            this.ApplyAchievementFilter();
        }

        private void OnStatEditingToggled(object sender, RoutedEventArgs e) => this.ApplyStatEditing();

        // ============================ icons ============================

        private void OnIconRealized(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is AchievementRow row && row.Icon == null)
            {
                _ = this.LoadIconAsync(row);
            }
        }

        private async Task LoadIconAsync(AchievementRow row)
        {
            var url = SteamArt.AchievementIcon(this._appId, row.IconName);
            if (url == null)
            {
                return;
            }

            this._iconsInFlight++;
            this.ShowDownloadCount();

            row.Icon = await ImageCache.GetAsync(url, IconDecodeWidth);

            this._iconsInFlight--;
            this.ShowDownloadCount();
        }

        private void ShowDownloadCount()
        {
            this.DownloadText.Text = this._iconsInFlight > 0
                ? $"loading {this._iconsInFlight} icons…"
                : "";
        }

        // ============================ tally ============================

        private void OnRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AchievementRow.IsUnlocked))
            {
                this.UpdateTally();
            }
        }

        private void UpdateTally()
        {
            var total = this._achievements.Count;
            var unlocked = this._achievements.Count(row => row.IsUnlocked == true);
            var pending = this._achievements.Count(row => row.IsModified == true);

            this.GameTally.Text = total == 0
                ? "No achievements in this game"
                : $"{unlocked} of {total} unlocked" + (pending > 0 ? $" · {pending} unsaved" : "");

            var ratio = total == 0 ? 0 : (double)unlocked / total;
            this.CompletionText.Text = total == 0 ? "—" : $"{Math.Round(ratio * 100)}%";

            this.CompletionRing.BeginAnimation(
                Ring.ProgressProperty,
                new DoubleAnimation(ratio, TimeSpan.FromMilliseconds(420))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
        }

        // ============================ writing ============================

        private async void OnSave(object sender, RoutedEventArgs e) => await this.CommitAsync();

        private async void OnUnlockAllAndSave(object sender, RoutedEventArgs e)
        {
            foreach (var row in this._achievements)
            {
                row.IsUnlocked = true;
            }

            await this.CommitAsync();
        }

        private async Task CommitAsync()
        {
            this.AllowInput(false);
            this.Status("Writing to Steam…");

            var result = this._session.Commit(this._achievements, this._statistics);

            var summary = $"Stored {result.Achievements} achievements and {result.Statistics} statistics";
            this.Status(result.Refused > 0
                ? $"{summary}. Steam refused {result.Refused}."
                : summary + ".");

            await this.ReloadAsync();
        }

        private void OnLockAll(object sender, RoutedEventArgs e)
        {
            foreach (var row in this._achievements)
            {
                row.IsUnlocked = false;
            }
        }

        private async void OnReload(object sender, RoutedEventArgs e) => await this.ReloadAsync();

        private async void OnReset(object sender, RoutedEventArgs e)
        {
            var wipeAchievements = Dialog.Confirm(
                this,
                "Reset this game's progress?",
                "Steam will clear every statistic for this game. Choose whether achievements go too — " +
                "this cannot be undone.",
                "Reset statistics and achievements",
                danger: true);

            if (wipeAchievements == false)
            {
                return;
            }

            if (this._session.ResetEverything(achievementsToo: true) == false)
            {
                Dialog.Error(this, "Reset failed", "Steam refused to reset this game.");
                return;
            }

            await this.ReloadAsync();
        }

        // ============================ chrome and state ============================

        private void AllowInput(bool allowed)
        {
            this.ReloadButton.IsEnabled = allowed;
            this.UnlockAllButton.IsEnabled = allowed;
            this.LockAllButton.IsEnabled = allowed;
            this.ResetButton.IsEnabled = allowed;
            this.SaveButton.IsEnabled = allowed;
        }

        private void Status(string message) => this.StatusText.Text = message;

        private void Fail(string heading, string body)
        {
            this.Status(heading);
            this.AllowInput(true);
            Dialog.Error(this, heading, body);
        }

        private void OnMaximize(object sender, RoutedEventArgs e) => this.ToggleMaximized();
    }
}
