using System.Windows.Controls;

namespace InspectionApp.Helpers
{
    public static class NavigationService
    {
        public static event Action<UserControl>? Navigate;

        public static void GoTo(UserControl view) => Navigate?.Invoke(view);
    }
}
