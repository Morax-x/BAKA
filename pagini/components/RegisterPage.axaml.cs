using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.Data;
using System;

namespace BugalterProject.pagini.components
{
    public partial class RegisterPage : UserControl
    {
        public RegisterPage()
        {
            InitializeComponent();
        }

        private async void ContinueToApp(object? sender, RoutedEventArgs e)
        {
            try
            {
                var result = await DatabaseService.RegisterUserAsync(
                    FirstNameBox.Text ?? string.Empty,
                    LastNameBox.Text ?? string.Empty,
                    EmailBox.Text ?? string.Empty,
                    PasswordBox.Text ?? string.Empty,
                    CaptchaBox.Text ?? string.Empty,
                    PolicyCheck.IsChecked == true);

                StatusText.Text = result.Message;

                if (result.Success)
                {
                    MainWindow.Instance?.ShowApplication();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Eroare neasteptata: {ex.Message}";
            }
        }
    }
}
