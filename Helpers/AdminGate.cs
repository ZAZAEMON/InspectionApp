using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InspectionApp.Helpers
{
    /// <summary>
    /// Admin password gate — used to protect Settings and Create Part Type.
    /// </summary>
    public static class AdminGate
    {
        // =====================================================================
        //   ADMIN PASSWORD — change the value below and rebuild to set
        //   a new password. (File: Helpers/AdminGate.cs)
        // =====================================================================
        public const string Password = "admin203077";
        // =====================================================================

        /// <summary>
        /// Shows a password prompt. Returns true if the user typed the correct
        /// password and clicked OK; false otherwise (wrong password or cancel).
        /// </summary>
        public static bool RequireAdmin(Window? owner = null)
        {
            var win = new Window
            {
                Title = "Admin Required",
                Width = 440,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                Owner = owner,
                Background = Brushes.White,
                Topmost = true,
            };

            var accent = new SolidColorBrush(Color.FromRgb(21, 101, 192)); // AccentBrush

            var root = new Border
            {
                BorderBrush = accent,
                BorderThickness = new Thickness(3),
                Padding = new Thickness(24, 20, 24, 20),
            };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "ADMIN ACCESS",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12),
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Enter admin password to continue:",
                FontSize = 14,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 10),
            });

            var pwd = new PasswordBox
            {
                FontSize = 18,
                Padding = new Thickness(8, 6, 8, 6),
                Height = 38,
                Margin = new Thickness(0, 0, 0, 6),
            };
            stack.Children.Add(pwd);

            var error = new TextBlock
            {
                Text = "",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed,
            };
            stack.Children.Add(error);

            bool result = false;

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var cancel = new Button
            {
                Content = new TextBlock
                {
                    Text = "Cancel",
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = accent,
                },
                Padding = new Thickness(20, 8, 20, 8),
                MinWidth = 100,
                Background = Brushes.Transparent,
                Foreground = accent,
                BorderBrush = accent,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
            };
            cancel.Click += (_, _) => { result = false; win.Close(); };

            var ok = new Button
            {
                // Explicit TextBlock content with hard-coded white foreground so it
                // can never be overridden by any global TextBlock style.
                Content = new TextBlock
                {
                    Text = "OK",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                },
                Padding = new Thickness(28, 8, 28, 8),
                MinWidth = 100,
                Background = accent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                IsDefault = true,
                Cursor = Cursors.Hand,
            };
            ok.Click += (_, _) =>
            {
                if (pwd.Password == Password)
                {
                    result = true;
                    win.Close();
                }
                else
                {
                    error.Text = "Wrong password. Try again.";
                    error.Visibility = Visibility.Visible;
                    pwd.Clear();
                    pwd.Focus();
                }
            };

            btnRow.Children.Add(cancel);
            btnRow.Children.Add(ok);
            stack.Children.Add(btnRow);

            root.Child = stack;
            win.Content = root;
            win.Loaded += (_, _) => pwd.Focus();
            win.ShowDialog();
            return result;
        }
    }
}
