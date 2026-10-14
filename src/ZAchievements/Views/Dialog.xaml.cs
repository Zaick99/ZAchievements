using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZAchievements.Views
{
    internal partial class Dialog : ShellWindow
    {
        private Dialog(string heading, string body, string glyph, string accent)
        {
            this.InitializeComponent();

            this.Heading.Text = heading;
            this.Body.Text = body;
            this.Glyph.Text = glyph;
            this.Glyph.SetResourceReference(ForegroundProperty, accent);
        }

        public static void Error(Window owner, string heading, string body)
        {
            Show(owner, heading, body, "\uEA39", "Danger", confirm: null, danger: false);
        }

        public static void Note(Window owner, string heading, string body)
        {
            Show(owner, heading, body, "\uE946", "Accent", confirm: null, danger: false);
        }

        public static bool Confirm(Window owner, string heading, string body, string confirm, bool danger = false)
        {
            return Show(owner, heading, body, "\uE7BA", danger ? "Danger" : "Warn", confirm, danger);
        }

        private static bool Show(
            Window owner,
            string heading,
            string body,
            string glyph,
            string accent,
            string confirm,
            bool danger)
        {
            Dialog dialog = new(heading, body, glyph, accent)
            {
                Owner = owner,
            };

            if (confirm == null)
            {
                dialog.AddButton("Close", primary: true, result: true);
                dialog.ShowDialog();
                return true;
            }

            dialog.AddButton("Cancel", primary: false, result: false);
            dialog.AddButton(confirm, primary: danger == false, result: true, danger: danger);
            return dialog.ShowDialog() == true;
        }

        private void AddButton(string label, bool primary, bool result, bool danger = false)
        {
            Button button = new()
            {
                Content = label,
                MinWidth = 96,
                Margin = new Thickness(this.Buttons.Children.Count == 0 ? 0 : 8, 0, 0, 0),
                IsDefault = result,
                IsCancel = result == false,
            };

            button.SetResourceReference(
                StyleProperty,
                danger == true ? "DangerButton" : primary == true ? "PrimaryButton" : "GhostButton");

            button.Click += (_, __) =>
            {
                this.DialogResult = result;
            };

            this.Buttons.Children.Add(button);
        }

        private void OnCaptionDrag(object sender, MouseButtonEventArgs e) => this.DragMove();
    }
}
