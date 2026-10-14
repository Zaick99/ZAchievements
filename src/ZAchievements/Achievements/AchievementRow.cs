using System;
using System.Windows.Media;
using ZAchievements.Infra;
using ZAchievements.Schema;

namespace ZAchievements.Achievements
{
    /// <summary>One achievement as the user currently has it, and as they want it.</summary>
    internal sealed class AchievementRow : Observable
    {
        private readonly SearchText _search;

        private bool _isUnlocked;
        private ImageSource _icon;

        public AchievementRow(AchievementDefinition definition, bool isUnlocked, DateTime? unlockedAt)
        {
            this.Id = definition.Id;
            this.Name = string.IsNullOrEmpty(definition.Name) || definition.Name.StartsWith("#", StringComparison.Ordinal)
                ? definition.Id
                : definition.Name;
            this.Description = definition.Description ?? "";
            this.IsHidden = definition.IsHidden;
            this.IsProtected = (definition.Permission & 3) != 0;
            // Only the colour icon is fetched: locked rows desaturate it in the view,
            // so flipping a row never costs another download.
            this.IconName = string.IsNullOrEmpty(definition.Icon) ? definition.IconLocked : definition.Icon;

            this.WasUnlocked = isUnlocked;
            this._isUnlocked = isUnlocked;
            this.UnlockedAt = unlockedAt;

            this._search = SearchText.For($"{this.Name} {this.Description} {this.Id}");
        }

        public string Id { get; }

        public string Name { get; }

        public string Description { get; }

        public bool IsHidden { get; }

        /// <summary>Steam validates this one server side, so a write may simply be refused.</summary>
        public bool IsProtected { get; }

        public bool WasUnlocked { get; }

        public DateTime? UnlockedAt { get; }

        public string IconName { get; }

        public bool IsUnlocked
        {
            get => this._isUnlocked;
            set
            {
                if (this.Set(ref this._isUnlocked, value) == false)
                {
                    return;
                }

                this.Raise(nameof(this.IsModified));
            }
        }

        public bool IsModified => this._isUnlocked != this.WasUnlocked;

        public ImageSource Icon
        {
            get => this._icon;
            set => this.Set(ref this._icon, value);
        }

        public string UnlockedAtText => this.UnlockedAt.HasValue == true
            ? this.UnlockedAt.Value.ToString("d MMM yyyy, HH:mm")
            : "";

        public bool Matches(string[] terms) => this._search.Matches(terms);
    }
}
