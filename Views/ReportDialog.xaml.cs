using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InspectionApp.Services;

namespace InspectionApp.Views
{
    public partial class ReportDialog : Window
    {
        private readonly DatabaseService _db = new();
        private readonly ExcelExportService _excel = new();
        private readonly bool _isPPAP;

        public ReportDialog(bool isPPAP = false)
        {
            _isPPAP = isPPAP;
            InitializeComponent();
            Title = isPPAP ? "PPAP Reports" : "Inspection Reports";
            DialogHeader.Text = isPPAP ? "PPAP REPORTS" : "INSPECTION REPORTS";
            FromDatePicker.SelectedDate = DateTime.Today.AddMonths(-1);
            ToDatePicker.SelectedDate   = DateTime.Today;
            LoadPartNumbers();
        }

        private void LoadPartNumbers()
        {
            var parts = _isPPAP ? _db.GetAllPPAPPartNumbers() : _db.GetAllPartNumbers();
            PartNumberCombo.Items.Clear();
            PartNumberCombo.Items.Add("(All)");
            foreach (var p in parts) PartNumberCombo.Items.Add(p);
            PartNumberCombo.SelectedIndex = 0;
        }

        private void PartNumberCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected   = PartNumberCombo.SelectedItem as string;
            string? partNum = selected == "(All)" || selected == null ? null : selected;
            LoadShifts(partNum);
        }

        private void LoadShifts(string? partNumber)
        {
            var shifts = _isPPAP ? _db.GetDistinctPPAPShifts(partNumber) : _db.GetDistinctShifts(partNumber);
            ShiftCombo.Items.Clear();
            ShiftCombo.Items.Add("(All)");
            foreach (var s in shifts) ShiftCombo.Items.Add(s);
            ShiftCombo.SelectedIndex = 0;
        }

        private void OpenReport_Click(object sender, RoutedEventArgs e)
        {
            var partSel   = PartNumberCombo.SelectedItem as string;
            var shiftSel  = (ShiftCombo.SelectedItem as string)?.Trim();
            var audText   = AuditorFilter.Text?.Trim();

            string? partNumber = string.IsNullOrEmpty(partSel) || partSel == "(All)" ? null : partSel;
            string? shift      = string.IsNullOrEmpty(shiftSel) || shiftSel == "(All)" ? null : shiftSel;
            string? auditor    = string.IsNullOrEmpty(audText) ? null : audText;

            DateTime? fromDate = FromDatePicker.SelectedDate?.Date;
            DateTime? toDate   = ToDatePicker.SelectedDate.HasValue
                                 ? ToDatePicker.SelectedDate.Value.Date.AddDays(1).AddTicks(-1)
                                 : null;

            SetStatus("Querying database…", Colors.Gray);

            var sessions = _isPPAP
                ? _db.GetPPAPSessions(partNumber, shift, auditor, fromDate, toDate)
                : _db.GetInspectionSessions(partNumber, shift, auditor, fromDate, toDate);

            if (sessions.Count == 0)
            {
                SetStatus("No records found for the selected filters. Try widening the date range or clearing some filters.", Color.FromRgb(198, 40, 40));
                return;
            }

            try
            {
                string label    = partNumber ?? "All Parts";
                string sub      = _isPPAP ? "PPAP" : "Product Audit";
                string filePath = _excel.GenerateReport(label, sessions, sub);
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                SetStatus($"Report opened — {sessions.Count} submission(s) found.", Color.FromRgb(46, 125, 50));
            }
            catch (Exception ex)
            {
                SetStatus($"Error generating report: {ex.Message}", Color.FromRgb(198, 40, 40));
            }
        }

        private void SetStatus(string text, Color color)
        {
            StatusBlock.Text       = text;
            StatusBlock.Foreground = new SolidColorBrush(color);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
