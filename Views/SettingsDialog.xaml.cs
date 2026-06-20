using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace InspectionApp.Views
{
    public partial class SettingsDialog : Window
    {
        private static readonly string[] SupportedExt = { ".png", ".jpg", ".jpeg", ".bmp" };
        private static string BackgroundsFolder =>
            Path.Combine(AppContext.BaseDirectory, "Backgrounds");

        public SettingsDialog()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                // Sync slider with the current background opacity
                if (Owner is MainWindow mw && mw.BackgroundImage != null)
                    OpacitySlider.Value = mw.BackgroundImage.Opacity;
                else
                    OpacitySlider.Value = 0.5;

                UpdateCurrentBgLabel();
            };
        }

        private void UpdateCurrentBgLabel()
        {
            try
            {
                Directory.CreateDirectory(BackgroundsFolder);
                var file = Directory.EnumerateFiles(BackgroundsFolder)
                    .Where(f => SupportedExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => f)
                    .FirstOrDefault();
                CurrentBgLabel.Text = file == null
                    ? "No background image set. Click Change Background to pick one."
                    : "Current: " + Path.GetFileName(file);
            }
            catch
            {
                CurrentBgLabel.Text = "(unable to read Backgrounds folder)";
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacityValueLabel != null)
                OpacityValueLabel.Text = $"{(int)Math.Round(e.NewValue * 100)}%";

            if (Owner is MainWindow mw && mw.BackgroundImage != null)
                mw.BackgroundImage.Opacity = e.NewValue;
        }

        private void ChangeBackground_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select a background image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                Directory.CreateDirectory(BackgroundsFolder);

                // Replace any existing background image so the new one is the only one
                foreach (var existing in Directory.EnumerateFiles(BackgroundsFolder))
                {
                    if (SupportedExt.Contains(Path.GetExtension(existing).ToLowerInvariant()))
                    {
                        try { File.Delete(existing); } catch { /* file might be locked, ignore */ }
                    }
                }

                var dest = Path.Combine(BackgroundsFolder, Path.GetFileName(dlg.FileName));
                File.Copy(dlg.FileName, dest, overwrite: true);

                if (Owner is MainWindow mw) mw.ReloadBackground();
                UpdateCurrentBgLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not change the background:\n" + ex.Message,
                                "Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveBackground_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(BackgroundsFolder)) return;
                foreach (var f in Directory.EnumerateFiles(BackgroundsFolder))
                {
                    if (SupportedExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                if (Owner is MainWindow mw) mw.ReloadBackground();
                UpdateCurrentBgLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(BackgroundsFolder);
            try { Process.Start(new ProcessStartInfo(BackgroundsFolder) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AuditorHistory_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AuditorHistoryDialog { Owner = this };
            dlg.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
