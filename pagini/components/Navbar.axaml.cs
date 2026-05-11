using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.pagini;

namespace BugalterProject.pagini.components
{
    public partial class Navbar : UserControl
    {
        public Navbar()
        {
            InitializeComponent();
        }

        private void OpenDashboard(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ChangePage(new Dashboard());
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
    }
}
