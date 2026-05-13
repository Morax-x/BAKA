using System;
using System.Linq;
using Avalonia.Controls;
using BugalterProject;
using BugalterProject.Data;

namespace BugalterProject.pagini.components
{
    public partial class AdminPage : UserControl
    {
        private AppUser? _selectedUser;

        public AdminPage()
        {
            InitializeComponent();
            LoadUsers();
            LoadStats();
        }

        private async void LoadUsers()
        {
            var users = await DatabaseService.GetAllUsersAsync();
            UsersList.ItemsSource = users;
            UsersCountText.Text = $"Utilizatori gasiti: {users.Count}";

            if (_selectedUser is not null)
            {
                UsersList.SelectedItem = users.FirstOrDefault(user => user.UserId == _selectedUser.UserId);
            }
            else if (users.Count > 0 && UsersList.SelectedItem is null)
            {
                UsersList.SelectedIndex = 0;
            }

            UpdateSelectedUserDetails();
        }

        private async void LoadStats()
        {
            var stats = await DatabaseService.GetAdminStatsAsync();
            AdminStatsText.Text =
                $"Total utilizatori: {stats.TotalUsers}\n" +
                $"Conturi admin: {stats.AdminUsers}\n" +
                $"Cheltuieli inregistrate: {stats.TotalExpenses}\n" +
                $"Datorii inregistrate: {stats.TotalDebts}\n" +
                $"Plati comunale: {stats.TotalUtilities}";
        }

        private void UsersList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _selectedUser = UsersList.SelectedItem as AppUser;
            UpdateSelectedUserDetails();
        }

        private void UpdateSelectedUserDetails()
        {
            if (_selectedUser is null)
            {
                SelectedUserText.Text = "Niciun utilizator selectat";
                return;
            }

            SelectedUserText.Text =
                $"{_selectedUser.FirstName} {_selectedUser.LastName}\n" +
                $"{_selectedUser.Email}\n" +
                $"Rol curent: {_selectedUser.Role}";
        }

        private async void SetUserRole(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await UpdateRoleAsync("user");
        }

        private async void SetAdminRole(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await UpdateRoleAsync("admin");
        }

        private async System.Threading.Tasks.Task UpdateRoleAsync(string role)
        {
            if (_selectedUser is null)
            {
                return;
            }

            var result = await DatabaseService.UpdateUserRoleAsync(_selectedUser.UserId, role);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                LoadUsers();
                LoadStats();
            }
        }

        private async void DeleteSelectedUser(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_selectedUser is null)
            {
                StatusText.Text = "Selecteaza un utilizator inainte de stergere.";
                return;
            }

            var result = await DatabaseService.DeleteUserByIdAsync(_selectedUser.UserId);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                if (AppSession.CurrentUser?.UserId == _selectedUser.UserId)
                {
                    AppSession.CurrentUser = null;
                    MainWindow.Instance?.ShowRegisterPage();
                    return;
                }

                _selectedUser = null;
                LoadUsers();
                LoadStats();
            }
        }
    }
}
