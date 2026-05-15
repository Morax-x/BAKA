using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.Data;

namespace BugalterProject.pagini.components
{
    public partial class UtilitiesPage : UserControl
    {
        private readonly bool _isAdmin;
        private readonly List<string> _utilityNames = new() { "Apa", "Lumina", "Gaz", "Internet" };
        private List<AppUser> _users = new();
        private List<UtilityItem> _utilities = new();
        private UtilityItem? _selectedUtility;

        public UtilitiesPage()
        {
            InitializeComponent();
            _isAdmin = AppSession.CurrentUser?.IsAdmin == true;
            UserPickerBorder.IsVisible = _isAdmin;
            UtilityNameCombo.ItemsSource = _utilityNames;
            UtilityNameCombo.SelectedItem = _utilityNames[0];
            _ = LoadPageAsync();
        }

        private async System.Threading.Tasks.Task LoadPageAsync()
        {
            await LoadUsersAsync();
            await LoadUtilitiesAsync();

            if (UtilityDatePicker.SelectedDate is null)
            {
                UtilityDatePicker.SelectedDate = DateTimeOffset.Now;
            }
        }

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            if (_isAdmin)
            {
                _users = await DatabaseService.GetAllUsersAsync();
                UserCombo.ItemsSource = _users;
                if (AppSession.CurrentUser is not null)
                {
                    UserCombo.SelectedItem = _users.FirstOrDefault(user => user.UserId == AppSession.CurrentUser.UserId) ?? _users.FirstOrDefault();
                }
            }
            else
            {
                UserCombo.ItemsSource = new[] { AppSession.CurrentUser }.Where(user => user is not null).Cast<AppUser>().ToList();
                UserCombo.SelectedItem = AppSession.CurrentUser;
            }
        }

        private async System.Threading.Tasks.Task LoadUtilitiesAsync()
        {
            var userId = _isAdmin ? null : AppSession.CurrentUser?.UserId;
            _utilities = await DatabaseService.GetUtilitiesAsync(userId);

            UtilitiesList.ItemsSource = _utilities;
            UtilitiesCountText.Text = $"Plati gasite: {_utilities.Count}";
            var total = _utilities.Sum(item => item.Amount);
            TotalText.Text = $"Total plati comunale inregistrate: {total:0.00} MDL";

            if (_selectedUtility is not null)
            {
                UtilitiesList.SelectedItem = _utilities.FirstOrDefault(item => item.UtilityId == _selectedUtility.UtilityId);
            }
        }

        private void UtilitiesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _selectedUtility = UtilitiesList.SelectedItem as UtilityItem;
            FillFormFromSelectedUtility();
        }

        private void FillFormFromSelectedUtility()
        {
            if (_selectedUtility is null)
            {
                StatusText.Text = "Selecteaza o plata comunala pentru editare.";
                return;
            }

            AmountBox.Text = _selectedUtility.Amount.ToString(CultureInfo.InvariantCulture);
            UtilityDatePicker.SelectedDate = new DateTimeOffset(_selectedUtility.UtilityDate.Date);
            UtilityNameCombo.SelectedItem = _utilityNames.FirstOrDefault(item =>
                string.Equals(item, _selectedUtility.UtilityName, StringComparison.OrdinalIgnoreCase));

            if (_isAdmin)
            {
                UserCombo.SelectedItem = _users.FirstOrDefault(user => user.UserId == _selectedUtility.UserId);
            }

            StatusText.Text = $"Plata selectata: {_selectedUtility.UtilityName}";
        }

        private async void AddUtility(object? sender, RoutedEventArgs e)
        {
            var userId = GetTargetUserId();
            var utilityName = NormalizeUtilityName(UtilityNameCombo.SelectedItem as string);
            var amount = ReadAmount();
            var utilityDate = UtilityDatePicker.SelectedDate?.DateTime.Date ?? DateTime.Today;

            if (userId is null || string.IsNullOrWhiteSpace(utilityName) || amount is null)
            {
                StatusText.Text = "Completeaza toate campurile corect.";
                return;
            }

            var result = await DatabaseService.AddUtilityAsync(userId.Value, utilityName, amount.Value, utilityDate);
            StatusText.Text = result.Message;
            if (result.Success)
            {
                ClearForm(false);
                await LoadUtilitiesAsync();
            }
        }

        private async void UpdateUtility(object? sender, RoutedEventArgs e)
        {
            if (_selectedUtility is null)
            {
                StatusText.Text = "Selecteaza o plata comunala inainte de actualizare.";
                return;
            }

            var utilityName = NormalizeUtilityName(UtilityNameCombo.SelectedItem as string);
            var amount = ReadAmount();
            var utilityDate = UtilityDatePicker.SelectedDate?.DateTime.Date ?? DateTime.Today;

            if (string.IsNullOrWhiteSpace(utilityName) || amount is null)
            {
                StatusText.Text = "Completeaza toate campurile corect.";
                return;
            }

            var result = await DatabaseService.UpdateUtilityAsync(
                _selectedUtility.UtilityId,
                utilityName,
                amount.Value,
                utilityDate);

            StatusText.Text = result.Message;
            if (result.Success)
            {
                await LoadUtilitiesAsync();
            }
        }

        private async void DeleteUtility(object? sender, RoutedEventArgs e)
        {
            if (_selectedUtility is null)
            {
                StatusText.Text = "Selecteaza o plata comunala inainte de stergere.";
                return;
            }

            var result = await DatabaseService.DeleteUtilityAsync(_selectedUtility.UtilityId);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                _selectedUtility = null;
                ClearForm(false);
                await LoadUtilitiesAsync();
            }
        }

        private async void MarkUtilityPaid(object? sender, RoutedEventArgs e)
        {
            if (_selectedUtility is null)
            {
                StatusText.Text = "Selecteaza o plata comunala inainte de confirmare.";
                return;
            }

            var result = await DatabaseService.MarkUtilityPaidAsync(_selectedUtility.UtilityId);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                _selectedUtility = null;
                ClearForm(false);
                await LoadUtilitiesAsync();
            }
        }

        private void ClearForm(object? sender, RoutedEventArgs e)
        {
            ClearForm(true);
        }

        private void ClearForm(bool showStatus)
        {
            _selectedUtility = null;
            UtilitiesList.SelectedItem = null;
            AmountBox.Text = string.Empty;
            UtilityDatePicker.SelectedDate = DateTimeOffset.Now;
            UtilityNameCombo.SelectedItem = _utilityNames[0];

            if (showStatus)
            {
                StatusText.Text = "Formular golit.";
            }
        }

        private int? GetTargetUserId()
        {
            if (_isAdmin)
            {
                return (UserCombo.SelectedItem as AppUser)?.UserId;
            }

            return AppSession.CurrentUser?.UserId;
        }

        private decimal? ReadAmount()
        {
            var text = (AmountBox.Text ?? string.Empty).Trim().Replace(',', '.');
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0)
            {
                return amount;
            }

            return null;
        }

        private static string? NormalizeUtilityName(string? utilityName)
        {
            return utilityName?.Trim().ToLowerInvariant();
        }
    }
}
