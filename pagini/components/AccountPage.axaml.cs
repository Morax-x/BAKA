using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BugalterProject.pagini.components
{
    public partial class AccountPage : UserControl
    {
        public AccountPage()
        {
            InitializeComponent();
        }

        private void Logout(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ShowRegisterPage();
        }
    }
}
