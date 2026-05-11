using Avalonia.Controls;
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
            TopNavbar.IsVisible = false;
            MainContent.Content = new RegisterPage();
        }

        public void ShowApplication()
        {
            TopNavbar.IsVisible = true;
            MainContent.Content = new Dashboard();
        }

        public void ChangePage(UserControl page)
        {
            MainContent.Content = page;
        }
    }
}
