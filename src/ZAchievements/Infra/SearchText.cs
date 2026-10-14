using System;
using System.Globalization;
using System.Text;

namespace ZAchievements.Infra
{
    /// <summary>
    /// A searchable form of a title: folded to lowercase ASCII, plus the acronym
    /// of its words. "Rocket League" is then found by "rocket", "league", "rl",
    /// "rocket lea" or "léague".
    /// </summary>
    internal readonly struct SearchText
    {
        private readonly string _flat;
        private readonly string _initials;

        private SearchText(string flat, string initials)
        {
            this._flat = flat;
            this._initials = initials;
        }

        public static SearchText For(string value)
        {
            if (string.IsNullOrEmpty(value) == true)
            {
                return new SearchText("", "");
            }

            var flat = Fold(value);

            StringBuilder initials = new();
            bool atWordStart = true;
            foreach (var letter in flat)
            {
                if (letter == ' ')
                {
                    atWordStart = true;
                    continue;
                }

                if (atWordStart == true)
                {
                    initials.Append(letter);
                    atWordStart = false;
                }
            }

            return new SearchText(flat, initials.ToString());
        }

        /// <summary>Every term must appear somewhere; an empty query matches everything.</summary>
        public bool Matches(string[] terms)
        {
            foreach (var term in terms)
            {
                if (this.MatchesTerm(term) == false)
                {
                    return false;
                }
            }

            return true;
        }

        public bool MatchesTerm(string term)
        {
            return this._flat.Contains(term, StringComparison.Ordinal) == true ||
                   this._initials.Contains(term, StringComparison.Ordinal) == true;
        }

        /// <summary>Splits what the user typed into the terms every candidate must satisfy.</summary>
        public static string[] Terms(string query)
        {
            if (string.IsNullOrWhiteSpace(query) == true)
            {
                return Array.Empty<string>();
            }

            return Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>Lowercase, accents removed, punctuation turned into spaces.</summary>
        private static string Fold(string value)
        {
            var decomposed = value.Normalize(NormalizationForm.FormD);
            StringBuilder folded = new(decomposed.Length);
            bool lastWasSpace = true;

            foreach (var letter in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(letter) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(letter) == true)
                {
                    folded.Append(char.ToLowerInvariant(letter));
                    lastWasSpace = false;
                    continue;
                }

                if (lastWasSpace == false)
                {
                    folded.Append(' ');
                    lastWasSpace = true;
                }
            }

            return folded.ToString().TrimEnd();
        }
    }
}
