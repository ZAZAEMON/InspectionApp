using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using InspectionApp.Helpers;

namespace InspectionApp.Views
{
    public partial class MainMenuView : UserControl
    {
        private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

        public MainMenuView()
        {
            InitializeComponent();
            UpdateClock();
            _clock.Tick += (_, _) => UpdateClock();
            _clock.Start();
            Unloaded += (_, _) => _clock.Stop();
        }

        private void UpdateClock()
            => DateTimeBlock.Text = DateTime.Now.ToString("dddd, dd MMM yyyy   HH:mm:ss");

        private void CreatePartType_Click(object sender, RoutedEventArgs e)
        {
            // Admin-only — change the password in Helpers/AdminGate.cs
            if (!AdminGate.RequireAdmin(Window.GetWindow(this))) return;
            NavigationService.GoTo(new CreatePartTypeView());
        }

        private void Home_Click(object sender, RoutedEventArgs e)
            => NavigationService.GoTo(new HomeView());

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Admin-only — change the password in Helpers/AdminGate.cs
            if (!AdminGate.RequireAdmin(Window.GetWindow(this))) return;
            var dlg = new SettingsDialog { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
        }

        private void PPAP_Click(object sender, RoutedEventArgs e)
            => NavigationService.GoTo(new PPAPView());
    }
}
