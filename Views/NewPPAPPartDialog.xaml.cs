using System.Windows;
using System.Windows.Input;

namespace InspectionApp.Views
{
    public partial class NewPPAPPartDialog : Window
    {
        public string? PartNumber { get; private set; }
        private readonly List<string> _existingParts;

        public NewPPAPPartDialog(List<string> existingParts)
        {
            _existingParts = existingParts;
            InitializeComponent();
            Loaded += (_, _) => PartNumberBox.Focus();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var pn = PartNumberBox.Text.Trim().ToUpperInvariant();
            if (pn.Length != 10)
            {
                ShowError("Part number must be exactly 10 alphanumeric characters.");
                return;
            }
            if (_existingParts.Any(p => p.Equals(pn, StringComparison.OrdinalIgnoreCase)))
            {
                ShowError($"'{pn}' already exists. Select it from the search list instead.");
                return;
            }
            PartNumber = pn;
            DialogResult = true;
        }

        private void PartNumberBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsLetterOrDigit);
        }

        private void PartNumberBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Create_Click(sender, e);
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
