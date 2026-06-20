using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using InspectionApp.Helpers;
using InspectionApp.Views;

namespace InspectionApp
{
    public partial class MainWindow : Window
    {
        // Drop ANY image (.png/.jpg/.jpeg/.bmp) into this folder next to the EXE.
        // The first one found is used as the window background.
        private static readonly string[] SupportedExt = { ".png", ".jpg", ".jpeg", ".bmp" };
        private static string BackgroundsFolder =>
            Path.Combine(AppContext.BaseDirectory, "Backgrounds");

        public MainWindow()
        {
            InitializeComponent();
            NavigationService.Navigate += view => MainContent.Content = view;
            MainContent.Content = new MainMenuView();
            LoadBackgroundImage();
            StateChanged += (_, _) => MaxBtn.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        }

        // A WindowChrome window set to Maximized overflows the monitor edges by the resize-border
        // thickness, clipping the top toolbar text off-screen. Constrain the maximized bounds to the
        // monitor work area so the window fills the screen exactly (and still respects the taskbar).
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                const int MONITOR_DEFAULTTONEAREST = 0x00000002;
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(monitor, ref mi))
                    {
                        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                        RECT work = mi.rcWork, full = mi.rcMonitor;
                        mmi.ptMaxPosition.X = work.Left - full.Left;
                        mmi.ptMaxPosition.Y = work.Top - full.Top;
                        mmi.ptMaxSize.X     = work.Right - work.Left;
                        mmi.ptMaxSize.Y     = work.Bottom - work.Top;
                        Marshal.StructureToPtr(mmi, lParam, true);
                    }
                }
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor, rcWork;
            public int dwFlags;
        }

        private void MinBtn_Click(object sender, RoutedEventArgs e)   => WindowState = WindowState.Minimized;
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
        private void MaxBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        // Public so SettingsDialog can ask us to reload after the user picks a new image.
        public void ReloadBackground() => LoadBackgroundImage();

        private void LoadBackgroundImage()
        {
            try
            {
                Directory.CreateDirectory(BackgroundsFolder);
                var file = Directory.EnumerateFiles(BackgroundsFolder)
                    .Where(f => SupportedExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => f)
                    .FirstOrDefault();

                if (file == null)
                {
                    BackgroundImage.Source = null;
                    return;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;        // load fully, release file handle
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.UriSource = new Uri(file);
                bmp.EndInit();
                bmp.Freeze();
                BackgroundImage.Source = bmp;
            }
            catch
            {
                BackgroundImage.Source = null;
            }
        }
    }
}
