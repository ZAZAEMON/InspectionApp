using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace InspectionApp.Helpers
{
    /// <summary>
    /// Large, high-contrast dialog helper for shop-floor visibility.
    /// Replaces System.Windows.MessageBox for warnings/info that need
    /// to be readable from a distance.
    ///
    /// To tweak the look:
    ///   • Change TitleFontSize / BodyFontSize below
    ///   • Change WarningColor (red) / InfoColor (blue)
    ///   • Change FlashOnWarning to disable the title flash
    /// </summary>
    public static class BigDialog
    {
        public const double TitleFontSize = 26;
        public const double BodyFontSize  = 20;
        public static readonly Color WarningColor = Color.FromRgb(198, 40, 40);   // strong red
        public static readonly Color InfoColor    = Color.FromRgb(21, 101, 192);  // blue
        public const bool FlashOnWarning = true;

        public static bool ShowWarning(string title, string body, Window? owner = null)
            => Show(title, body, isWarning: true, owner: owner, yesNo: true);

        public static void ShowInfo(string title, string body, Window? owner = null)
            => Show(title, body, isWarning: false, owner: owner, yesNo: false);

        private static bool Show(string title, string body, bool isWarning, Window? owner, bool yesNo)
        {
            var win = new Window
            {
                Title = title,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Owner = owner,
                Width = 620,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                Background = Brushes.White,
                AllowsTransparency = false,
                Topmost = true,
            };

            var color = isWarning ? WarningColor : InfoColor;
            var accentBrush = new SolidColorBrush(color);

            var root = new Border
            {
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(4),
                Background = Brushes.White,
            };

            var stack = new StackPanel { Margin = new Thickness(28, 22, 28, 22) };

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = TitleFontSize,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap,
            };
            stack.Children.Add(titleBlock);

            stack.Children.Add(new TextBlock
            {
                Text = body,
                FontSize = BodyFontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24),
                TextWrapping = TextWrapping.Wrap,
            });

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            bool result = false;

            if (yesNo)
            {
                var yes = MakeButton("YES", accentBrush, Brushes.White);
                yes.Click += (_, _) => { result = true; win.Close(); };
                var no = MakeButton("NO", Brushes.White, accentBrush);
                no.BorderBrush = accentBrush;
                no.BorderThickness = new Thickness(2);
                no.Click += (_, _) => { result = false; win.Close(); };
                btnRow.Children.Add(yes);
                btnRow.Children.Add(new Border { Width = 18 });
                btnRow.Children.Add(no);
            }
            else
            {
                var ok = MakeButton("OK", accentBrush, Brushes.White);
                ok.Click += (_, _) => { result = true; win.Close(); };
                btnRow.Children.Add(ok);
            }

            stack.Children.Add(btnRow);
            root.Child = stack;
            win.Content = root;

            // Optional flashing title for warnings
            if (isWarning && FlashOnWarning)
            {
                var anim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.35,
                    Duration = TimeSpan.FromSeconds(0.55),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                titleBlock.BeginAnimation(UIElement.OpacityProperty, anim);
            }

            win.ShowDialog();
            return result;
        }

        private static Button MakeButton(string text, Brush bg, Brush fg)
        {
            return new Button
            {
                Content = new TextBlock
                {
                    Text = text,
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = fg,
                },
                Background = bg,
                Foreground = fg,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(36, 12, 36, 12),
                MinWidth = 140,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
        }
    }
}
