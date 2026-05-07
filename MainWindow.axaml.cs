using Avalonia.Controls;
using BugalterProject.pagini.components;

namespace BugalterProject
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainContent.Content = new RegisterPage();
            TopNavbar.IsVisible = false;
        }
    }
}
