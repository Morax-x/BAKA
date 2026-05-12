using Avalonia.Controls;
using BugalterProject.Data;
using BugalterProject.pagini;
using BugalterProject.pagini.components;

namespace BugalterProject
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            ShowRegisterPage();
        }

        public void ShowRegisterPage()
        {
            AppSession.CurrentUser = null;
            TopNavbar.IsVisible = false;
            MainContent.Content = new RegisterPage();
        }

        public void ShowApplication(AppUser user)
        {
            AppSession.CurrentUser = user;
            TopNavbar.IsVisible = true;
            TopNavbar.RefreshForUser(user);
            MainContent.Content = new Dashboard();
        }

        public void ShowApplication()
        {
            TopNavbar.IsVisible = true;
            TopNavbar.RefreshForUser(AppSession.CurrentUser);
            MainContent.Content = new Dashboard();
        }

        public void ChangePage(UserControl page)
        {
            MainContent.Content = page;
        }
    }
}
