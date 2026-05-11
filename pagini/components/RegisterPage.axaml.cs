using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BugalterProject.pagini.components
{
    public partial class RegisterPage : UserControl
    {
        public RegisterPage()
        {
            InitializeComponent();
        }

        private void ContinueToApp(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ShowApplication();
        }
    }
}
