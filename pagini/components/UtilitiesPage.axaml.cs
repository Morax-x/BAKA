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
        private readonly List<string> _entryModes = new() { "Automat", "Manual" };
        private readonly List<string> _utilityNames = new() { "Apa", "Lumina", "Gaz", "Internet" };
        private readonly List<string> _utilityViews = new() { "Toate serviciile", "Apa", "Lumina", "Gaz", "Internet" };

        private List<AppUser> _users = new();
        private List<UtilityItem> _utilities = new();
        private UtilityItem? _selectedUtility;

        public UtilitiesPage()
        {
            InitializeComponent();

            _isAdmin = AppSession.CurrentUser?.IsAdmin == true;
            UserPickerBorder.IsVisible = _isAdmin;

            EntryModeCombo.ItemsSource = _entryModes;
            EntryModeCombo.SelectedItem = _entryModes[0];

            UtilityNameCombo.ItemsSource = _utilityNames;
            UtilityNameCombo.SelectedItem = _utilityNames[0];

            UtilityViewCombo.ItemsSource = _utilityViews;
            UtilityViewCombo.SelectedItem = _utilityViews[0];

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

            UpdateEntryModeState();
            UpdateSelectionState();
        }

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            if (_isAdmin)
            {
                _users = await DatabaseService.GetAllUsersAsync();
                UserCombo.ItemsSource = _users;

                if (AppSession.CurrentUser is not null)
                {
                    UserCombo.SelectedItem =
                        _users.FirstOrDefault(user => user.UserId == AppSession.CurrentUser.UserId)
                        ?? _users.FirstOrDefault();
                }
            }
            else
            {
                UserCombo.ItemsSource = new[] { AppSession.CurrentUser }
                    .Where(user => user is not null)
                    .Cast<AppUser>()
                    .ToList();

                UserCombo.SelectedItem = AppSession.CurrentUser;
            }
        }

        private async System.Threading.Tasks.Task LoadUtilitiesAsync()
        {
            var userId = _isAdmin ? null : AppSession.CurrentUser?.UserId;

            _utilities = await DatabaseService.GetUtilitiesAsync(userId);
            ApplyUtilityFilter();
        }

        private void ApplyUtilityFilter()
        {
            var selectedView = UtilityViewCombo.SelectedItem as string;
            var filteredUtilities = GetFilteredUtilities(selectedView);

            UtilitiesList.ItemsSource = null;
            UtilitiesList.ItemsSource = filteredUtilities;

            UtilitiesCountText.Text = $"Plati gasite: {filteredUtilities.Count}";

            var total = filteredUtilities.Sum(item => item.Amount);
            TotalText.Text = $"Total plati comunale inregistrate: {total:0.00} MDL";
        }

        private List<UtilityItem> GetFilteredUtilities(string? selectedView)
        {
            if (string.IsNullOrWhiteSpace(selectedView) ||
                string.Equals(selectedView, "Toate serviciile", StringComparison.OrdinalIgnoreCase))
            {
                return _utilities;
            }

            return _utilities
                .Where(item => string.Equals(item.UtilityName, selectedView, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void UtilitiesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _selectedUtility = UtilitiesList.SelectedItem as UtilityItem;
            FillFormFromSelectedUtility();
        }

        private void UtilityViewCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ApplyUtilityFilter();
        }

        private void FillFormFromSelectedUtility()
        {
            if (_selectedUtility is null)
            {
                UpdateSelectionState();
                StatusText.Text = "Selecteaza o plata comunala din lista.";
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

            EntryModeCombo.SelectedItem = _entryModes[1];
            PreviousReadingBox.Text = string.Empty;
            CurrentReadingBox.Text = string.Empty;
            TariffBox.Text = string.Empty;
            UpdateEntryModeState();
            UpdateSelectionState();

            StatusText.Text = $"Plata selectata: {_selectedUtility.UtilityName}";
        }

        private async void SaveUtility(object? sender, RoutedEventArgs e)
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

            OperationResult result;

            if (UtilitiesList.SelectedItem is UtilityItem utility)
            {
                result = await DatabaseService.UpdateUtilityAsync(
                    utility.UtilityId,
                    utilityName,
                    amount.Value,
                    utilityDate);
            }
            else
            {
                result = await DatabaseService.AddUtilityAsync(
                    userId.Value,
                    utilityName,
                    amount.Value,
                    utilityDate);
            }

            StatusText.Text = result.Message;

            if (result.Success)
            {
                ClearForm(false);
                await LoadUtilitiesAsync();
            }
        }

        private async void ApplyUtilityPayment(object? sender, RoutedEventArgs e)
        {
            if (UtilitiesList.SelectedItem is not UtilityItem utility)
            {
                StatusText.Text = "Selecteaza plata din lista pentru aplicarea platii.";
                return;
            }

            var paymentAmount = ReadPaymentAmount();
            if (paymentAmount is null)
            {
                StatusText.Text = "Introdu o suma valida pentru plata.";
                return;
            }

            var result = await DatabaseService.ApplyUtilityPaymentAsync(utility.UtilityId, paymentAmount.Value);

            StatusText.Text = result.Message;

            if (result.Success)
            {
                ClearForm(false);
                await LoadUtilitiesAsync();
            }
        }

        private async void DeleteUtility(object? sender, RoutedEventArgs e)
        {
            if (UtilitiesList.SelectedItem is not UtilityItem utility)
            {
                StatusText.Text = "Selecteaza plata din lista pentru stergere.";
                return;
            }

            var result = await DatabaseService.DeleteUtilityAsync(utility.UtilityId);
            StatusText.Text = result.Message;

            if (result.Success)
            {
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
            PaymentAmountBox.Text = string.Empty;
            PreviousReadingBox.Text = string.Empty;
            CurrentReadingBox.Text = string.Empty;
            TariffBox.Text = string.Empty;
            UtilityDatePicker.SelectedDate = DateTimeOffset.Now;
            EntryModeCombo.SelectedItem = _entryModes[0];
            UtilityNameCombo.SelectedItem = _utilityNames[0];
            UpdateEntryModeState();
            UpdateSelectionState();

            if (showStatus)
            {
                StatusText.Text = "Formular pregatit pentru o plata noua.";
            }
        }

        private void EntryModeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateEntryModeState();
        }

        private void AutoCalculationValueChanged(object? sender, TextChangedEventArgs e)
        {
            RecalculateAutomaticAmount();
        }

        private void UpdateEntryModeState()
        {
            var automaticMode = IsAutomaticMode();
            AutomaticCalculationBorder.IsVisible = automaticMode;
            AmountBox.IsReadOnly = automaticMode;
            AmountLabelText.Text = automaticMode ? "Suma calculata" : "Suma";

            if (automaticMode)
            {
                RecalculateAutomaticAmount();
            }
            else
            {
                AutoCalculationText.Text = "In modul manual, suma este introdusa direct de utilizator.";
            }
        }

        private bool IsAutomaticMode()
        {
            return string.Equals(EntryModeCombo.SelectedItem as string, "Automat", StringComparison.OrdinalIgnoreCase);
        }

        private void RecalculateAutomaticAmount()
        {
            if (!IsAutomaticMode())
            {
                return;
            }

            var previousReading = ReadDecimalValue(PreviousReadingBox.Text);
            var currentReading = ReadDecimalValue(CurrentReadingBox.Text);
            var tariff = ReadDecimalValue(TariffBox.Text);

            if (previousReading is null || currentReading is null || tariff is null)
            {
                AmountBox.Text = string.Empty;
                AutoCalculationText.Text = "Introdu contorul precedent, contorul curent si tariful pentru calcul automat.";
                return;
            }

            if (currentReading < previousReading)
            {
                AmountBox.Text = string.Empty;
                AutoCalculationText.Text = "Contorul curent nu poate fi mai mic decat contorul precedent.";
                return;
            }

            var consumption = currentReading.Value - previousReading.Value;
            var amount = Math.Round(consumption * tariff.Value, 2);

            AmountBox.Text = amount.ToString("0.00", CultureInfo.InvariantCulture);
            AutoCalculationText.Text = $"Consum calculat: {consumption:0.##} x {tariff.Value:0.##} = {amount:0.00} MDL";
        }

        private void UpdateSelectionState()
        {
            var hasSelection = _selectedUtility is not null;
            PaymentAmountBorder.IsVisible = hasSelection;
            ApplyPaymentButton.IsVisible = hasSelection;
            ClearSelectionButton.IsVisible = hasSelection;
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

        private static decimal? ReadDecimalValue(string? text)
        {
            var normalizedText = (text ?? string.Empty).Trim().Replace(',', '.');

            if (decimal.TryParse(normalizedText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0)
            {
                return value;
            }

            return null;
        }

        private decimal? ReadPaymentAmount()
        {
            var text = (PaymentAmountBox.Text ?? string.Empty).Trim().Replace(',', '.');

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
