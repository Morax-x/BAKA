using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.Data;

namespace BugalterProject.pagini.components
{
    public partial class RegisterPage : UserControl
    {
        private readonly Random _random = new();
        private string _captcha = string.Empty;
        private bool _registerMode = true;

        public RegisterPage()
        {
            InitializeComponent();
            LoadCaptcha();
            SetMode(true);
        }

        private void ShowRegisterMode(object? sender, RoutedEventArgs e)
        {
            SetMode(true);
        }

        private void ShowLoginMode(object? sender, RoutedEventArgs e)
        {
            SetMode(false);
        }

        private void RefreshCaptcha(object? sender, RoutedEventArgs e)
        {
            LoadCaptcha();
        }

        private async void SubmitAction(object? sender, RoutedEventArgs e)
        {
            if (_registerMode)
            {
                var result = await DatabaseService.RegisterUserAsync(
                    FirstNameBox.Text ?? string.Empty,
                    LastNameBox.Text ?? string.Empty,
                    EmailRegisterBox.Text ?? string.Empty,
                    PasswordRegisterBox.Text ?? string.Empty,
                    CaptchaRegisterBox.Text ?? string.Empty,
                    _captcha,
                    PolicyCheck.IsChecked == true);

                StatusText.Text = result.Message;

                if (result.Success)
                {
                    if (result.User is not null)
                    {
                        MainWindow.Instance?.ShowApplication(result.User);
                    }
                    else
                    {
                        MainWindow.Instance?.ShowApplication();
                    }
                }
            }
            else
            {
                var result = await DatabaseService.LoginUserAsync(
                    EmailLoginBox.Text ?? string.Empty,
                    PasswordLoginBox.Text ?? string.Empty);

                StatusText.Text = result.Message;

                if (result.Success)
                {
                    if (result.User is not null)
                    {
                        MainWindow.Instance?.ShowApplication(result.User);
                    }
                    else
                    {
                        MainWindow.Instance?.ShowApplication();
                    }
                }
            }
        }

        private void SetMode(bool registerMode)
        {
            _registerMode = registerMode;

            RegisterPanel.IsVisible = registerMode;
            LoginPanel.IsVisible = !registerMode;

            RegisterTabButton.Background = registerMode ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4A")) : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3A3A3A"));
            LoginTabButton.Background = registerMode ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3A3A3A")) : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4A"));

            ModeTitle.Text = registerMode ? "Inregistrarea" : "Autentificarea";
            ModeDescription.Text = registerMode
                ? "Creeaza un cont nou pentru a accesa aplicatia."
                : "Conecteaza-te cu datele existente pentru a accesa contul.";

            ActionButton.Content = registerMode ? "Continue" : "Autentificare";
            StatusText.Text = registerMode
                ? "Completeaza campurile si apasa Continue."
                : "Introdu datele si apasa Autentificare.";
        }

        private void LoadCaptcha()
        {
            _captcha = GenerateCaptcha();
            RegisterCaptchaText.Text = _captcha;
        }

        private string GenerateCaptcha()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            return new string(Enumerable.Range(0, 5)
                .Select(_ => chars[_random.Next(chars.Length)])
                .ToArray());
        }
    }
}
