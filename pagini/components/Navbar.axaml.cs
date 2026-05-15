using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.Data;
using BugalterProject.pagini;

namespace BugalterProject.pagini.components
{
    public partial class Navbar : UserControl
    {
        public Navbar()
        {
            InitializeComponent();
        }

        public void RefreshForUser(AppUser? user)
        {
            AdminButton.IsVisible = user?.IsAdmin == true;
        }

        private void OpenDashboard(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ChangePage(new Dashboard());
        }

        private void OpenExpenses(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ChangePage(new ExpensesPage());
        }

        private void OpenDebts(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ChangePage(new DebtsPage());
        }

        private void OpenUtilities(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ChangePage(new UtilitiesPage());
        }

        private void OpenHistory(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ChangePage(new HistoryPage());
        }

        private void OpenAccount(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ChangePage(new AccountPage());
        }

        private void OpenAdmin(object? sender, RoutedEventArgs e)
        {
            if (AppSession.CurrentUser?.IsAdmin == true)
            {
                MainWindow.Instance?.ChangePage(new AdminPage());
            }
        }
    }
}
