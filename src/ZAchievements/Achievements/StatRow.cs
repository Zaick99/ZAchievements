using System;
using System.Globalization;
using ZAchievements.Infra;
using ZAchievements.Schema;

namespace ZAchievements.Achievements
{
    internal enum StatKind
    {
        Integer,
        Float,
    }

    /// <summary>One game statistic, edited as text and validated on the way back out.</summary>
    internal sealed class StatRow : Observable
    {
        private readonly SearchText _search;

        private string _value;
        private bool _isEditable;

        private StatRow(StatDefinition definition, StatKind kind, string value)
        {
            this.Id = definition.Id;
            this.DisplayName = string.IsNullOrEmpty(definition.DisplayName) ? definition.Id : definition.DisplayName;
            this.Kind = kind;
            this.IncrementOnly = definition.IncrementOnly;
            this.IsProtected = (definition.Permission & 3) != 0;
            this.Original = value;
            this._value = value;

            this._search = SearchText.For($"{this.DisplayName} {this.Id}");
        }

        public static StatRow Integer(StatDefinition definition, int value)
        {
            return new StatRow(definition, StatKind.Integer, value.ToString(CultureInfo.InvariantCulture));
        }

        public static StatRow Float(StatDefinition definition, float value)
        {
            return new StatRow(definition, StatKind.Float, value.ToString("R", CultureInfo.InvariantCulture));
        }

        public string Id { get; }

        public string DisplayName { get; }

        public StatKind Kind { get; }

        public bool IncrementOnly { get; }

        public bool IsProtected { get; }

        public string Original { get; }

        public string Value
        {
            get => this._value;
            set
            {
                if (this.IsProtected == true || this.Set(ref this._value, value) == false)
                {
                    return;
                }

                this.Raise(nameof(this.IsModified));
                this.Raise(nameof(this.Note));
            }
        }

        /// <summary>Set by the view once the user accepts that editing statistics is risky.</summary>
        public bool IsEditable
        {
            get => this._isEditable;
            set => this.Set(ref this._isEditable, value);
        }

        public bool IsModified => this.IsValid == true && this._value != this.Original;

        public bool IsValid => this.Kind == StatKind.Integer
            ? int.TryParse(this._value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            : float.TryParse(this._value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        public string Note
        {
            get
            {
                if (this.IsProtected == true)
                {
                    return "protected";
                }

                if (this.IsValid == false)
                {
                    return this.Kind == StatKind.Integer ? "whole numbers only" : "numbers only";
                }

                return this.IncrementOnly == true ? "increment only" : "";
            }
        }

        public int AsInteger => int.Parse(this._value, NumberStyles.Integer, CultureInfo.InvariantCulture);

        public float AsFloat => float.Parse(this._value, NumberStyles.Float, CultureInfo.InvariantCulture);

        public bool Matches(string[] terms) => this._search.Matches(terms);
    }
}
