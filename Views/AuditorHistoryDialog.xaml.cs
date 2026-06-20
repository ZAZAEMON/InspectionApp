using System.Windows;
using InspectionApp.Services;

namespace InspectionApp.Views
{
    public partial class AuditorHistoryDialog : Window
    {
        public AuditorHistoryDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => LoadHistory();
        }

        private void LoadHistory()
        {
            var db = new DatabaseService();
            var entries = db.GetAuditorHistory();

            var items = entries.Select(e => new AuditorLogEntry
            {
                DisplayTime  = e.Time.ToString("dd-MM-yyyy  HH:mm"),
                AuditorName  = e.AuditorName,
                Context      = e.Context
            }).ToList();

            HistoryList.ItemsSource = items;
            CountLabel.Text = $"{items.Count} entr{(items.Count == 1 ? "y" : "ies")}";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }

    internal class AuditorLogEntry
    {
        public string DisplayTime  { get; set; } = "";
        public string AuditorName  { get; set; } = "";
        public string Context      { get; set; } = "";
    }
}
