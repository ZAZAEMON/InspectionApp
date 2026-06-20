using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using InspectionApp.Helpers;
using InspectionApp.Models;
using InspectionApp.Services;

namespace InspectionApp.Views
{
    public partial class CreatePartTypeView : UserControl
    {
        private readonly DatabaseService _db = new();
        private readonly ObservableCollection<ParameterRowItem> _rows = new();
        private bool _isEditingExisting = false;
        private string? _originalPartNumber;
        private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(4) };

        // ===== ADJUST PARAMETERS HERE =====
        // Each entry: (name, Value or Check). To rename a parameter or change its type,
        // edit it in this list — every other place reads from here.
        public static readonly List<(string Name, ParameterInputType Type)> AllParameters = new()
        {
            ("Packaging label on primary box",                                       ParameterInputType.Check),
            ("Weight label as per Pasg/Pack & correct data",                         ParameterInputType.Check),
            ("Part Qty inside box",                                                  ParameterInputType.Value),
            ("Identification / Color ring",                                          ParameterInputType.Check),
            ("Protection cap damage (Inlet/Nozzle and leakoff hole)",                ParameterInputType.Check),
            ("NHA Part no. inscription",                                             ParameterInputType.Check),
            ("Nozzle Part no. inscription at Nozzle shaft",                          ParameterInputType.Check),
            ("Cap nut type",                                                         ParameterInputType.Check),
            ("NHB Type (Part no.)",                                                  ParameterInputType.Check),
            ("CU washer Type (Part no.)",                                            ParameterInputType.Check),
            ("CU washer OD",                                                         ParameterInputType.Value),
            ("CU washer Thickness",                                                  ParameterInputType.Value),
            ("Nozzle shaft projection",                                              ParameterInputType.Value),
            ("Rust Preventive oil application on Nozzle tip",                        ParameterInputType.Check),
            ("Banjo Bolt (part no.)",                                                ParameterInputType.Check),
            ("Retaining Screw (Part no.)",                                           ParameterInputType.Check),
            ("Viton Ring / \"O\" ring",                                              ParameterInputType.Check),
            ("Rubber Bush/ Steel washer",                                            ParameterInputType.Check),
            ("Circlip / Spring ring",                                                ParameterInputType.Check),
            ("Trigo packaging Sticker / Seal",                                       ParameterInputType.Check),
            ("Torque - Dot mark evidence for cap nut leakage",                       ParameterInputType.Check),
            ("Ball Presence & Caulking mark presence",                               ParameterInputType.Check),
            ("Loose bend/ CU washer packet with primary box",                        ParameterInputType.Check),
            ("QMM2 audit seal on box after inspection",                              ParameterInputType.Check),
            ("Check and mention QR code last 4 digit no. on primary box label",      ParameterInputType.Value),
        };

        public static ParameterInputType GetTypeFor(string parameterName) =>
            AllParameters.FirstOrDefault(p => p.Name == parameterName).Type;

        public CreatePartTypeView()
        {
            InitializeComponent();
            ParameterRowsControl.ItemsSource = _rows;
            _rows.CollectionChanged += (_, _) => RefreshRowNumbers();
            _statusTimer.Tick += (_, _) => { StatusBorder.Visibility = Visibility.Collapsed; _statusTimer.Stop(); };
            UpdateCurrentSavePathLabel();
            // Pre-populate all 25 parameters so every new part starts complete
            AutoAddAllParameters();
        }

        // ── Auto-fill all 25 parameters ───────────────────────────────────────────
        private void AutoAddAllParameters()
        {
            _rows.Clear();
            foreach (var (name, type) in AllParameters)
            {
                var row = new ParameterRowItem
                {
                    ParameterName = name,
                    InputType     = type,
                    IsAlternate   = _rows.Count % 2 != 0
                };
                row.RemoveCommand = new RelayCommand(_ => RemoveRow(row));
                _rows.Add(row);
            }
            RefreshRowNumbers();
            UpdatePlaceholder();
            UpdateRowCount();
        }

        // ── Save-path picker ──────────────────────────────────────────────────────
        private void UpdateCurrentSavePathLabel()
        {
            var current = Helpers.UserSettings.GetSavePath();
            CurrentSavePathLabel.Text = string.IsNullOrEmpty(current)
                ? "Save folder: (default — InspectionReports)"
                : $"Save folder: {current}";
        }

        private void SetSavePath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Pick a folder for inspection Excel files",
                InitialDirectory = Helpers.UserSettings.GetSavePath()
                                   ?? System.IO.Path.Combine(AppContext.BaseDirectory, "InspectionReports"),
            };
            if (dlg.ShowDialog() != true) return;

            var chosen = dlg.FolderName;
            bool confirm = Helpers.BigDialog.ShowWarning(
                "USE THIS FOLDER?",
                $"Save all future inspection Excel files to:\n\n{chosen}",
                Window.GetWindow(this));
            if (!confirm) return;

            Helpers.UserSettings.SetSavePath(chosen);
            UpdateCurrentSavePathLabel();
            ShowStatus($"Save folder set: {chosen}", true);
        }

        // ── Part Number length guard (handles paste / drag-drop bypassing MaxLength) ──
        private bool _loadingPartFromSuggestion = false;

        private void PartNumberBox_LengthGuard(object sender, TextChangedEventArgs e)
        {
            // Don't truncate while we are programmatically setting the text from
            // the suggestions list — the existing DB part number stays intact.
            if (_loadingPartFromSuggestion) return;

            if (PartNumberBox.Text.Length > 10)
            {
                int caret = PartNumberBox.CaretIndex;
                PartNumberBox.Text = PartNumberBox.Text.Substring(0, 10);
                PartNumberBox.CaretIndex = Math.Min(caret, 10);
            }
        }

        // ── Part Number Popup ─────────────────────────────────────────────────────

        private void PartNumberBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var parts = _db.GetAllPartNumbers();
            if (parts.Count == 0) return;
            PartSuggestionsList.ItemsSource = parts;
            PartSuggestionsPopup.Width = PartNumberBox.ActualWidth;
            PartSuggestionsPopup.IsOpen = true;
        }

        private void PartNumberBox_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var parts = _db.GetAllPartNumbers();
            if (parts.Count == 0) return;
            PartSuggestionsList.ItemsSource = parts;
            PartSuggestionsPopup.Width = PartNumberBox.ActualWidth;
            PartSuggestionsPopup.IsOpen = true;
        }

        private void PartNumberBox_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                if (!PartSuggestionsList.IsMouseOver && !PartNumberBox.IsFocused)
                    PartSuggestionsPopup.IsOpen = false;
            }));
        }

        private void PartNumberBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                if (!PartSuggestionsList.IsKeyboardFocusWithin)
                    PartSuggestionsPopup.IsOpen = false;
            }));
        }

        private void PartSuggestionsList_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                if (!PartNumberBox.IsFocused)
                    PartSuggestionsPopup.IsOpen = false;
            }));
        }

        private void PartSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PartSuggestionsList.SelectedItem is not string partNumber) return;
            PartSuggestionsPopup.IsOpen = false;
            _loadingPartFromSuggestion = true;
            PartNumberBox.Text = partNumber;
            _loadingPartFromSuggestion = false;
            LoadPartForEditing(partNumber);
        }

        private void LoadPartForEditing(string partNumber)
        {
            var parameters = _db.GetParametersForPart(partNumber);
            if (parameters.Count == 0)
            {
                ShowStatus($"No parameters found for '{partNumber}'.", false);
                return;
            }

            _rows.Clear();
            foreach (var p in parameters)
            {
                var row = new ParameterRowItem
                {
                    ParameterName = p.ParameterName,
                    Frequency     = p.Frequency,
                    SampleBox     = p.SampleBox,
                    InputType     = AllParameters.Any(a => a.Name == p.ParameterName)
                                    ? GetTypeFor(p.ParameterName)
                                    : p.InputType
                };
                row.RemoveCommand = new RelayCommand(_ => RemoveRow(row));
                _rows.Add(row);
            }

            RefreshRowNumbers();
            UpdatePlaceholder();
            UpdateRowCount();
            _originalPartNumber = partNumber;
            _isEditingExisting = true;
            DeleteBtn.Visibility = Visibility.Visible;
            ShowStatus($"Loaded '{partNumber}' — {parameters.Count} parameters. Modify and save to update.", true);
        }

        // ── Row management ────────────────────────────────────────────────────────

        private void RemoveRow(ParameterRowItem row)
        {
            _rows.Remove(row);
            UpdatePlaceholder();
            UpdateRowCount();
        }

        private void RefreshRowNumbers()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].SerialNumber = i + 1;
                _rows[i].IsAlternate  = i % 2 != 0;
            }
        }

        private void UpdatePlaceholder()
            => EmptyPlaceholder.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        private void UpdateRowCount()
            => RowCountLabel.Text = _rows.Count == 0 ? "" : $"{_rows.Count} parameter{(_rows.Count == 1 ? "" : "s")}";

        // ── Bottom action bar ─────────────────────────────────────────────────────

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Count == 0) return;
            bool result = Helpers.BigDialog.ShowWarning(
                "CLEAR ALL PARAMETERS?",
                "Remove all parameters from this configuration?",
                Window.GetWindow(this));
            if (!result) return;

            _rows.Clear();
            _isEditingExisting  = false;
            _originalPartNumber = null;
            DeleteBtn.Visibility = Visibility.Collapsed;
            UpdatePlaceholder();
            UpdateRowCount();
        }

        private void DeletePart_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditingExisting || string.IsNullOrWhiteSpace(_originalPartNumber))
            {
                ShowStatus("No saved part is loaded — load one from the suggestions to delete it.", false);
                return;
            }

            bool confirmed = Helpers.BigDialog.ShowWarning(
                "DELETE PART TYPE?",
                $"Permanently delete '{_originalPartNumber}' and all its parameters?\nThis cannot be undone.",
                Window.GetWindow(this));
            if (!confirmed) return;

            var (success, message) = _db.DeletePartType(_originalPartNumber);
            ShowStatus(message, success);
            if (!success) return;

            _isEditingExisting  = false;
            _originalPartNumber = null;
            PartNumberBox.Clear();
            _rows.Clear();
            DeleteBtn.Visibility = Visibility.Collapsed;
            UpdatePlaceholder();
            UpdateRowCount();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var partNumber = PartNumberBox.Text.Trim();

            // Part number must not be empty — alphanumeric, any format
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                ShowStatus("Part Number cannot be empty.", false);
                PartNumberBox.Focus();
                return;
            }

            // Sort: Check parameters first, then Value, preserving the user's order within each group.
            var parameters = _rows
                .Select((r, idx) => new { r, idx })
                .OrderBy(x => x.r.InputType == ParameterInputType.Check ? 0 : 1)
                .ThenBy(x => x.idx)
                .Select((x, newIdx) => new PartParameter
                {
                    SerialNumber  = newIdx + 1,
                    ParameterName = x.r.ParameterName,
                    Frequency     = x.r.Frequency,
                    SampleBox     = x.r.SampleBox,
                    InputType     = x.r.InputType
                }).ToList();

            bool success;
            string message;

            if (_isEditingExisting)
            {
                bool isRename = !string.Equals(_originalPartNumber, partNumber, StringComparison.OrdinalIgnoreCase);
                string title      = isRename ? "RENAME AND UPDATE?" : "UPDATE PART TYPE?";
                string confirmMsg = isRename
                    ? $"Rename '{_originalPartNumber}' to '{partNumber}' and update its parameters?"
                    : $"Update existing Part Type '{partNumber}'?\nThis will overwrite its parameter configuration.";
                bool confirm = Helpers.BigDialog.ShowWarning(title, confirmMsg, Window.GetWindow(this));
                if (!confirm) return;
                (success, message) = _db.RenameAndUpdatePartType(_originalPartNumber!, partNumber, parameters);
            }
            else
            {
                (success, message) = _db.SavePartType(partNumber, parameters);

                // Part already exists — offer to overwrite
                if (!success && message.Contains("already exists"))
                {
                    bool overwrite = Helpers.BigDialog.ShowWarning(
                        "PART ALREADY EXISTS",
                        $"'{partNumber}' already exists. Overwrite its configuration?",
                        Window.GetWindow(this));
                    if (overwrite)
                        (success, message) = _db.UpdatePartType(partNumber, parameters);
                    else
                        return;
                }
            }

            ShowStatus(message, success);
            if (success)
            {
                _isEditingExisting  = false;
                _originalPartNumber = null;
                PartNumberBox.Clear();
                _rows.Clear();
                DeleteBtn.Visibility = Visibility.Collapsed;
                UpdatePlaceholder();
                UpdateRowCount();
                // Re-populate with all 25 parameters ready for the next new part
                AutoAddAllParameters();
            }
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
    }

    // ──────────────────────────────────────────────────────────────────────────────
    public class ParameterRowItem : INotifyPropertyChanged
    {
        private int    _serialNumber;
        private string _frequency  = string.Empty;
        private string _sampleBox  = string.Empty;
        private bool   _isAlternate;

        public int SerialNumber
        {
            get => _serialNumber;
            set { _serialNumber = value; OnPropertyChanged(nameof(SerialNumber)); }
        }

        public string ParameterName { get; set; } = string.Empty;

        public string Frequency
        {
            get => _frequency;
            set { _frequency = value; OnPropertyChanged(nameof(Frequency)); }
        }

        public string SampleBox
        {
            get => _sampleBox;
            set { _sampleBox = value; OnPropertyChanged(nameof(SampleBox)); }
        }

        public ParameterInputType InputType { get; set; }
        public string InputTypeDisplay => InputType.ToString();

        public bool IsAlternate
        {
            get => _isAlternate;
            set { _isAlternate = value; OnPropertyChanged(nameof(IsAlternate)); }
        }

        public ICommand? RemoveCommand { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
