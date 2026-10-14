using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace ZAchievements.Views
{
    /// <summary>
    /// A borderless window that keeps native resizing, snapping and rounded corners
    /// while drawing its own caption. The look lives in Theme/Chrome.xaml.
    /// </summary>
    internal class ShellWindow : Window
    {
        public ShellWindow()
        {
            this.Bind(SystemCommands.CloseWindowCommand, () => SystemCommands.CloseWindow(this));
            this.Bind(SystemCommands.MinimizeWindowCommand, () => SystemCommands.MinimizeWindow(this));
            this.Bind(SystemCommands.MaximizeWindowCommand, () => SystemCommands.MaximizeWindow(this));
            this.Bind(SystemCommands.RestoreWindowCommand, () => SystemCommands.RestoreWindow(this));
        }

        public void ToggleMaximized()
        {
            this.WindowState = this.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        protected void DragCaption(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                this.ToggleMaximized();
                return;
            }

            this.DragMove();
        }

        private void Bind(ICommand command, System.Action run)
        {
            this.CommandBindings.Add(new CommandBinding(command, (_, e) =>
            {
                run();
                e.Handled = true;
            }));
        }
    }
}
