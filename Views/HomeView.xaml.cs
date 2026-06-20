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
    public partial class HomeView : UserControl
    {
        private readonly DatabaseService _db = new();
        private readonly ObservableCollection<InspectionRowItem> _inspRows = new();
        private readonly ObservableCollection<string> _filteredParts = new();
        private List<string> _allParts = new();
        private string? _loadedPartNumber;
        private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };

        public HomeView()
        {
            InitializeComponent();
            InspectionRowsControl.ItemsSource = _inspRows;
            PartListBox.ItemsSource = _filteredParts;
            _statusTimer.Tick += (_, _) => { StatusBorder.Visibility = Visibility.Collapsed; _statusTimer.Stop(); };
            RefreshPartList();
        }

        // ── Part Loader ──────────────────────────────────────────────────────────

        private void RefreshPartList()
        {
            _allParts = _db.GetAllPartNumbers();
            ApplySearch(SearchBox.Text);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => ApplySearch(SearchBox.Text);

        private void ApplySearch(string query)
        {
            _filteredParts.Clear();
            var q = query.Trim();
            foreach (var p in _allParts)
            {
                if (string.IsNullOrEmpty(q) || p.Contains(q, StringComparison.OrdinalIgnoreCase))
                    _filteredParts.Add(p);
            }
        }

        private void PartListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

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

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            RefreshPartList();
            PartListBox.Visibility = Visibility.Visible;
        }

        private void PartLoaderPanel_FocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (!PartLoaderPanel.IsKeyboardFocusWithin)
                    {
                        PartListBox.Visibility = Visibility.Collapsed;
                        PartLoaderPanel.Visibility = Visibility.Collapsed;
                        SidebarColumnDef.Width = new GridLength(0);
                        SidebarColumnDef.MinWidth = 0;
                        SidebarSplitter.Visibility = Visibility.Collapsed;
                    }
                }));
            }
        }

        private void SelectPartType_Click(object sender, RoutedEventArgs e)
        {
            PartLoaderPanel.Visibility = Visibility.Visible;
            SidebarColumnDef.Width = new GridLength(220, GridUnitType.Pixel);
            SidebarColumnDef.MinWidth = 180;
            SidebarSplitter.Visibility = Visibility.Visible;
            RefreshPartList();
            PartListBox.Visibility = Visibility.Visible;
            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        private void LoadPart_Click(object sender, RoutedEventArgs e)
        {
            if (PartListBox.SelectedItem is not string partNumber)
            {
                ShowStatus("Select a part number from the list first.", false);
                return;
            }
            LoadPart(partNumber);
        }

        private void LoadPart(string partNumber)
        {
            var parameters = _db.GetParametersForPart(partNumber);
            if (parameters.Count == 0)
            {
                ShowStatus($"Part '{partNumber}' has no parameters configured.", false);
                return;
            }

            _loadedPartNumber = partNumber;
            _inspRows.Clear();

            var sortedParams = parameters
                .Select((p, idx) => new { p, idx })
                .OrderBy(x => x.p.InputType == Models.ParameterInputType.Check ? 0 : 1)
                .ThenBy(x => x.idx)
                .Select(x => x.p)
                .ToList();

            for (int i = 0; i < sortedParams.Count; i++)
            {
                var p = sortedParams[i];
                _inspRows.Add(new InspectionRowItem
                {
                    SerialNumber  = i + 1,
                    ParameterName = p.ParameterName,
                    InputType     = p.InputType,
                    IsAlternate   = i % 2 != 0,
                    Reading1 = p.InputType == Models.ParameterInputType.Check ? "-Select-" : "",
                });
            }

            LoadedPartLabel.Text = $"Loaded: {partNumber}  ({parameters.Count} parameters)";
            LoadedPartLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 181, 246));
            NoPartPlaceholder.Visibility = Visibility.Collapsed;
            TableContainer.Visibility = Visibility.Visible;
            SubmitBtn.IsEnabled = true;
            SubmitBtnLabel.Text = "Submit";

            FilePathBox.Visibility = Visibility.Collapsed;
            FilePathPrefix.Text = "";

            PartListBox.Visibility = Visibility.Collapsed;
            PartLoaderPanel.Visibility = Visibility.Collapsed;
            SidebarColumnDef.Width = new GridLength(0);
            SidebarColumnDef.MinWidth = 0;
            SidebarSplitter.Visibility = Visibility.Collapsed;
            ShowStatus($"Part '{partNumber}' loaded — {parameters.Count} parameters.", true);
        }

        // ── Block readings until Shift + Auditor are filled ──────────────────────

        private void TableContainer_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus is not FrameworkElement fe) return;
            if (fe.Tag is not string tag || tag != "input") return;

            bool shiftMissing   = string.IsNullOrWhiteSpace(GetShift());
            bool auditorMissing = string.IsNullOrWhiteSpace(AuditorBox.Text);

            if (!shiftMissing && !auditorMissing) return;

            e.Handled = true;
            Helpers.BigDialog.ShowInfo(
                "SHIFT & AUDITOR REQUIRED",
                "Please enter Shift and Auditor name before entering readings.",
                Window.GetWindow(this));

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (shiftMissing) ShiftBox.IsDropDownOpen = true;
                else              AuditorBox.Focus();
            }));
        }

        // ── Submit — saves to SQLite ──────────────────────────────────────────────

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedPartNumber == null || _inspRows.Count == 0)
            {
                ShowStatus("No part loaded. Load a part before submitting.", false);
                return;
            }

            var shift   = GetShift();
            var auditor = AuditorBox.Text.Trim();

            if (string.IsNullOrEmpty(shift) || string.IsNullOrEmpty(auditor))
            {
                Helpers.BigDialog.ShowInfo(
                    "SHIFT & AUDITOR REQUIRED",
                    "Please enter both Shift and Auditor name before submitting.",
                    Window.GetWindow(this));
                if (string.IsNullOrEmpty(shift)) ShiftBox.IsDropDownOpen = true;
                else                             AuditorBox.Focus();
                return;
            }

            try
            {
                var readings = _inspRows.Select(r => new Models.InspectionReading
                {
                    SerialNumber  = r.SerialNumber,
                    ParameterName = r.ParameterName,
                    Reading       = r.Reading1,
                    InputType     = r.InputType.ToString()
                }).ToList();

                _db.SaveInspectionSession(_loadedPartNumber!, shift, auditor, readings);
                _db.LogAuditor(auditor, "Product Audit");

                ClearReadings();
                ShowStatus("Inspection saved to database.", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Save failed: {ex.Message}", false);
            }
        }

        private void ClearReadings()
        {
            foreach (var row in _inspRows)
            {
                row.Reading1 = row.InputType == Models.ParameterInputType.Check
                    ? "-Select-"
                    : string.Empty;
            }
        }

        // ── Enter key navigation ──────────────────────────────────────────────────

        private void InspectionArea_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;

            var inputs = CollectInputControls(InspectionRowsControl);
            if (inputs.Count == 0) return;

            var focused = Keyboard.FocusedElement as UIElement;
            if (focused == null) return;
            int idx = inputs.IndexOf(focused);
            if (idx < 0) return;

            int numRows = _inspRows.Count;
            int row     = idx;
            int nextIdx = (row + 1) < numRows ? row + 1 : 0;

            if (nextIdx >= 0 && nextIdx < inputs.Count)
                inputs[nextIdx].Focus();
        }

        private static List<UIElement> CollectInputControls(DependencyObject root)
        {
            var list = new List<UIElement>();
            CollectRecursive(root, list);
            return list;
        }

        private static void CollectRecursive(DependencyObject parent, List<UIElement> list)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe &&
                    fe.Tag is string tag && tag == "input" &&
                    fe.Visibility == Visibility.Visible &&
                    fe.IsEnabled)
                {
                    list.Add((UIElement)fe);
                }
                else
                {
                    CollectRecursive(child, list);
                }
            }
        }

        // ── Auditor: alphabets and spaces only ───────────────────────────────────

        private void AuditorBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => char.IsLetter(c) || c == ' ');
        }

        private string GetShift() =>
            (ShiftBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        // ── Numeric input validation ──────────────────────────────────────────────

        private void NumericInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var box = (TextBox)sender;
            var proposed = box.Text.Remove(box.SelectionStart, box.SelectionLength)
                               .Insert(box.SelectionStart, e.Text);
            e.Handled = !IsValidNumericPartial(proposed);
        }

        private static bool IsValidNumericPartial(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            if (s == "-" || s == ".") return true;
            return double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _);
        }

        // ── Status message ────────────────────────────────────────────────────────

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
            var dlg = new ReportDialog { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
        }

        private void TableScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0)
                HeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    public class InspectionRowItem : INotifyPropertyChanged
    {
        private string _reading1 = string.Empty;
        private bool   _isAlternate;

        public int    SerialNumber  { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public Models.ParameterInputType InputType { get; set; }

        public bool IsValueType => InputType == Models.ParameterInputType.Value;
        public bool IsCheckType => InputType == Models.ParameterInputType.Check;

        public string Reading1
        {
            get => _reading1;
            set { _reading1 = value; OnPropertyChanged(nameof(Reading1)); }
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
