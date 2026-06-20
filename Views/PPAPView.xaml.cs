using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using InspectionApp.Helpers;
using InspectionApp.Services;

namespace InspectionApp.Views
{
    public partial class PPAPView : UserControl
    {
        private readonly DatabaseService _db = new();
        private readonly ObservableCollection<PPAPRowItem> _rows = new();
        private readonly ObservableCollection<string> _filteredParts = new();
        private List<string> _allParts = new();
        private string? _loadedPartNumber;
        private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        private const int MaxRows = 25;

        public PPAPView()
        {
            InitializeComponent();
            PPAPRowsControl.ItemsSource = _rows;
            PartListBox.ItemsSource = _filteredParts;
            _statusTimer.Tick += (_, _) => { StatusBorder.Visibility = Visibility.Collapsed; _statusTimer.Stop(); };
            RefreshPartList();
        }

        // ── Search panel toggle ──────────────────────────────────────────────────

        private void SearchToggle_Click(object sender, RoutedEventArgs e)
        {
            if (SearchPanel.Visibility == Visibility.Visible)
                CloseSearchPanel();
            else
                OpenSearchPanel();
        }

        private void OpenSearchPanel()
        {
            RefreshPartList();
            SearchPanel.Visibility     = Visibility.Visible;
            SidebarColumnDef.Width     = new GridLength(260, GridUnitType.Pixel);
            SidebarColumnDef.MinWidth  = 200;
            SidebarSplitter.Visibility = Visibility.Visible;
            SearchBox.Focus();
        }

        private void CloseSearchPanel()
        {
            SearchPanel.Visibility     = Visibility.Collapsed;
            SidebarColumnDef.Width     = new GridLength(0);
            SidebarColumnDef.MinWidth  = 0;
            SidebarSplitter.Visibility = Visibility.Collapsed;
        }

        private void RefreshPartList()
        {
            _allParts = _db.GetAllPPAPPartNumbers();
            ApplySearch(SearchBox.Text);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => ApplySearch(SearchBox.Text);

        private void ApplySearch(string query)
        {
            _filteredParts.Clear();
            var q = query.Trim();
            foreach (var p in _allParts)
                if (string.IsNullOrEmpty(q) || p.Contains(q, StringComparison.OrdinalIgnoreCase))
                    _filteredParts.Add(p);
        }

        private void PartListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PartListBox.SelectedItem is string partNumber)
                LoadPart(partNumber);
        }

        private void PartListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && PartListBox.SelectedItem is string partNumber)
            {
                e.Handled = true;
                LoadPart(partNumber);
            }
        }

        // ── Add Part — opens NewPPAPPartDialog ───────────────────────────────────

        private void AddPart_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new NewPPAPPartDialog(_allParts) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && dlg.PartNumber != null)
            {
                _db.CreatePPAPPart(dlg.PartNumber);
                RefreshPartList();
                LoadNewPart(dlg.PartNumber);
            }
        }

        private void LoadNewPart(string partNumber)
        {
            _loadedPartNumber = partNumber;
            _rows.Clear();

            LoadedPartLabel.Text       = $"New: {partNumber}";
            LoadedPartLabel.Foreground = new SolidColorBrush(Color.FromRgb(255, 167, 38));
            NoPartPlaceholder.Visibility = Visibility.Collapsed;
            TableContainer.Visibility    = Visibility.Visible;
            CloseSearchPanel();
            UpdateBottomBar();
            ShowStatus($"Created '{partNumber}' — set Shift & Auditor, add rows, fill parameters, then submit.", true);
        }

        private void LoadPart(string partNumber)
        {
            var parameters = _db.GetPPAPParameters(partNumber);
            _loadedPartNumber = partNumber;
            _rows.Clear();

            for (int i = 0; i < parameters.Count; i++)
            {
                var p   = parameters[i];
                var row = new PPAPRowItem
                {
                    SerialNumber  = i + 1,
                    ParameterName = p.ParameterName,
                    IsAlternate   = i % 2 != 0
                };
                row.IsValueType = p.InputType == "Value";
                _rows.Add(row);
            }

            int count = parameters.Count;
            LoadedPartLabel.Text       = $"Loaded: {partNumber}  ({count} parameter{(count == 1 ? "" : "s")})";
            LoadedPartLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 181, 246));
            NoPartPlaceholder.Visibility = Visibility.Collapsed;
            TableContainer.Visibility    = Visibility.Visible;
            CloseSearchPanel();
            UpdateBottomBar();
            ShowStatus($"PPAP part '{partNumber}' loaded.", true);
        }

        // ── Add / Delete Row ─────────────────────────────────────────────────────

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Count >= MaxRows) return;
            int sn = _rows.Count + 1;
            _rows.Add(new PPAPRowItem { SerialNumber = sn, IsAlternate = sn % 2 == 0 });
            UpdateBottomBar();
            ScrollRowsToEnd();
        }

        private void ScrollRowsToEnd()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                RowsScrollViewer.ScrollToBottom()));
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PPAPRowItem row)
            {
                _rows.Remove(row);
                for (int i = 0; i < _rows.Count; i++)
                {
                    _rows[i].SerialNumber = i + 1;
                    _rows[i].IsAlternate  = i % 2 != 0;
                }
                UpdateBottomBar();
            }
        }

        // ── Bottom bar visibility ─────────────────────────────────────────────────

        private void UpdateBottomBar()
        {
            bool partLoaded = _loadedPartNumber != null;
            int count = _rows.Count;

            AddRowBtn.Visibility = (partLoaded && count < MaxRows)
                ? Visibility.Visible : Visibility.Collapsed;

            SubmitBtn.Visibility = (partLoaded && count >= 1)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Keyboard navigation ──────────────────────────────────────────────────

        private void TableContainer_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus is not FrameworkElement fe) return;
            if (fe.Tag is not string tag || tag != "input") return;

            bool shiftMissing   = string.IsNullOrWhiteSpace(GetShift());
            bool auditorMissing = string.IsNullOrWhiteSpace(AuditorBox.Text);
            if (!shiftMissing && !auditorMissing) return;

            e.Handled = true;
            BigDialog.ShowInfo("SHIFT & AUDITOR REQUIRED",
                "Please set Shift and Auditor name before entering readings.",
                Window.GetWindow(this));
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (shiftMissing) ShiftBox.IsDropDownOpen = true;
                else              AuditorBox.Focus();
            }));
        }

        private void TableContainer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Down && e.Key != Key.Up) return;
            if (e.OriginalSource is not FrameworkElement current) return;
            if (current.Tag is not string tag) return;

            // Enter only navigates from reading cells; Up/Down stay in their own column
            if (e.Key == Key.Enter && tag != "input") return;

            var cells = GetTaggedCells(tag);
            int idx = cells.IndexOf(current);
            if (idx < 0) return;

            int targetIdx = e.Key == Key.Up ? idx - 1 : idx + 1;
            if (targetIdx < 0 || targetIdx >= cells.Count) return;

            e.Handled = true;
            var target = cells[targetIdx];
            target.Focus();
            target.BringIntoView();
        }

        private List<FrameworkElement> GetTaggedCells(string tag)
        {
            var result = new List<FrameworkElement>();
            CollectTaggedChildren(TableContainer, result, tag);
            return result;
        }

        private static void CollectTaggedChildren(DependencyObject parent, List<FrameworkElement> result, string targetTag)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe &&
                    fe.Tag is string t && t == targetTag &&
                    fe.IsVisible && fe.IsEnabled)
                {
                    result.Add(fe);
                }
                CollectTaggedChildren(child, result, targetTag);
            }
        }

        // ── Numeric-only validation for Value-type readings ───────────────────────

        private void NumericInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb       = sender as TextBox;
            var fullText = (tb?.Text ?? "") + e.Text;
            e.Handled    = !IsValidDecimalInput(fullText);
        }

        private static bool IsValidDecimalInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            var s = text.StartsWith('-') ? text[1..] : text;
            return s.Count(c => c == '.') <= 1 && s.Replace(".", "").All(char.IsDigit);
        }

        // ── Submit ────────────────────────────────────────────────────────────────

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedPartNumber == null || _rows.Count == 0)
            {
                ShowStatus("No part loaded or no rows added.", false);
                return;
            }

            var shift   = GetShift();
            var auditor = AuditorBox.Text.Trim();
            if (string.IsNullOrEmpty(shift) || string.IsNullOrEmpty(auditor))
            {
                BigDialog.ShowInfo("SHIFT & AUDITOR REQUIRED",
                    "Please enter both Shift and Auditor before submitting.",
                    Window.GetWindow(this));
                if (string.IsNullOrEmpty(shift)) ShiftBox.IsDropDownOpen = true;
                else                             AuditorBox.Focus();
                return;
            }

            // Every row must have a parameter name
            bool hasBlank = _rows.Any(r => string.IsNullOrWhiteSpace(r.ParameterName));
            if (hasBlank)
            {
                ShowStatus("All rows must have a parameter name. Fill in or delete blank rows.", false);
                return;
            }

            try
            {
                var paramDefs = _rows
                    .Select(r => (r.SerialNumber, r.ParameterName, r.IsValueType ? "Value" : "Check"))
                    .ToList();
                _db.SaveOrUpdatePPAPPart(_loadedPartNumber!, paramDefs);

                var readings = _rows
                    .Select(r => new Models.InspectionReading
                    {
                        SerialNumber  = r.SerialNumber,
                        ParameterName = r.ParameterName,
                        Reading       = r.Reading,
                        InputType     = r.IsValueType ? "Value" : "Check"
                    }).ToList();
                _db.SavePPAPSession(_loadedPartNumber!, shift, auditor, readings);
                _db.LogAuditor(auditor, "PPAP");

                RefreshPartList();
                ClearReadings();
                int pc = paramDefs.Count;
                LoadedPartLabel.Text       = $"Loaded: {_loadedPartNumber}  ({pc} parameter{(pc == 1 ? "" : "s")})";
                LoadedPartLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 181, 246));
                ShowStatus("PPAP inspection saved successfully.", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Save failed: {ex.Message}", false);
            }
        }

        private void ClearReadings()
        {
            foreach (var row in _rows)
                row.Reading = row.IsValueType ? "" : "-Select-";
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string GetShift() =>
            (ShiftBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        private void AuditorBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => char.IsLetter(c) || c == ' ');
        }

        private void ShowStatus(string message, bool success)
        {
            StatusText.Text = message;
            StatusBorder.Background = new SolidColorBrush(
                success ? Color.FromRgb(232, 245, 233) : Color.FromRgb(255, 235, 238));
            StatusText.Foreground = new SolidColorBrush(
                success ? Color.FromRgb(46, 125, 50) : Color.FromRgb(198, 40, 40));
            StatusBorder.BorderBrush = new SolidColorBrush(
                success ? Color.FromRgb(165, 214, 167) : Color.FromRgb(239, 154, 154));
            StatusBorder.BorderThickness = new Thickness(1);
            StatusBorder.Visibility = Visibility.Visible;
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private void BackToMenu_Click(object sender, RoutedEventArgs e)
            => NavigationService.GoTo(new MainMenuView());

        private void Report_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ReportDialog(isPPAP: true) { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
        }
    }

    // ── Row view-model ────────────────────────────────────────────────────────────
    public class PPAPRowItem : INotifyPropertyChanged
    {
        private int    _serialNumber;
        private string _paramName   = "";
        private bool   _isValueType = false;
        private string _reading     = "-Select-";
        private bool   _isAlternate;

        public int SerialNumber
        {
            get => _serialNumber;
            set { _serialNumber = value; OnPropertyChanged(nameof(SerialNumber)); }
        }

        public string ParameterName
        {
            get => _paramName;
            set { _paramName = value; OnPropertyChanged(nameof(ParameterName)); }
        }

        public bool IsValueType
        {
            get => _isValueType;
            set
            {
                if (_isValueType == value) return;
                _isValueType = value;
                OnPropertyChanged(nameof(IsValueType));
                OnPropertyChanged(nameof(IsCheckType));
                Reading = value ? "" : "-Select-";
            }
        }

        public bool IsCheckType => !_isValueType;

        public string Reading
        {
            get => _reading;
            set { _reading = value; OnPropertyChanged(nameof(Reading)); }
        }

        public bool IsAlternate
        {
            get => _isAlternate;
            set { _isAlternate = value; OnPropertyChanged(nameof(IsAlternate)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
