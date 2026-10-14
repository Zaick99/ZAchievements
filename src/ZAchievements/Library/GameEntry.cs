using System;
using System.Globalization;
using System.Windows.Media;
using ZAchievements.Infra;

namespace ZAchievements.Library
{
    internal sealed class GameEntry : Observable
    {
        private readonly SearchText _search;
        private readonly string _idText;

        private ImageSource _capsule;
        private bool _isFree;

        public GameEntry(uint id, string kind, string name, bool isOwned)
        {
            this.Id = id;
            this.Kind = kind;
            this.IsOwned = isOwned;
            // Valve's catalogue ships titles padded with whitespace, which would
            // otherwise sort ahead of everything else.
            this.Name = string.IsNullOrWhiteSpace(name) == true ? $"App {id}" : name.Trim();

            this._idText = id.ToString(CultureInfo.InvariantCulture);
            this._search = SearchText.For(this.Name);
        }

        public uint Id { get; }

        /// <summary>normal, demo, mod or junk, as classified by the shared game list.</summary>
        public string Kind { get; }

        /// <summary>
        /// False for games pulled in from the catalogue that this account has no
        /// licence for. Steam refuses most writes for those, but trying is allowed.
        /// </summary>
        public bool IsOwned { get; }

        /// <summary>Listed at no cost on the store, so a licence is one click away.</summary>
        public bool IsFree
        {
            get => this._isFree;
            set => this.Set(ref this._isFree, value);
        }

        public string Name { get; }

        public string CapsuleUrl { get; set; }

        public ImageSource Capsule
        {
            get => this._capsule;
            set => this.Set(ref this._capsule, value);
        }

        public string Initials => this.Name.Length > 0 ? this.Name.Substring(0, 1).ToUpperInvariant() : "?";

        /// <summary>Matches on title, title acronym, or app id — every term must land.</summary>
        public bool Matches(string[] terms)
        {
            foreach (var term in terms)
            {
                if (this._search.MatchesTerm(term) == false &&
                    this._idText.Contains(term, StringComparison.Ordinal) == false)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
