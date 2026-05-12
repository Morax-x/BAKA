using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.Data;

namespace BugalterProject.pagini.components
{
    public partial class AccountPage : UserControl
    {
        public AccountPage()
        {
            InitializeComponent();
            LoadUser();
        }

        private void Logout(object? sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.ShowRegisterPage();
        }

        private async void DeleteAccount(object? sender, RoutedEventArgs e)
        {
            var user = AppSession.CurrentUser;
            if (user is null)
            {
                StatusText.Text = "Nu exista un utilizator conectat.";
                return;
            }

            if (DeleteConfirmCheck.IsChecked != true)
            {
                StatusText.Text = "Confirma stergerea contului inainte de actiune.";
                return;
            }

            var result = await DatabaseService.DeleteCurrentUserAsync(user.UserId);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                AppSession.CurrentUser = null;
                MainWindow.Instance?.ShowRegisterPage();
            }
        }

        private void LoadUser()
        {
            var user = AppSession.CurrentUser;
            if (user is null)
            {
                UserNameText.Text = "Utilizator curent";
                UserEmailText.Text = "Email";
                UserRoleText.Text = "Rol: necunoscut";
                return;
            }

            UserNameText.Text = $"Nume: {user.FirstName} {user.LastName}";
            UserEmailText.Text = $"Email: {user.Email}";
            UserRoleText.Text = $"Rol: {user.Role}";
            StatusText.Text = $"Utilizator incarcat: {user.Email}";
        }
    }
}
