using Avalonia.Controls;
using BugalterProject.Data;

namespace BugalterProject.pagini.components
{
    public partial class AdminPage : UserControl
    {
        public AdminPage()
        {
            InitializeComponent();
            LoadUsers();
        }

        private async void LoadUsers()
        {
            var users = await DatabaseService.GetAllUsersAsync();
            UsersList.ItemsSource = users;
            UsersCountText.Text = $"Utilizatori gasiti: {users.Count}";
        }
    }
}
