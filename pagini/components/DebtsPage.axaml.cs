using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.Data;

namespace BugalterProject.pagini.components
{
    public partial class DebtsPage : UserControl
    {
        private readonly bool _isAdmin;

        private readonly List<string> _debtTypes = new()
        {
            "Sunt dator eu",
            "Sunt datori mie"
        };

        private readonly List<string> _debtViews = new()
        {
            "Toate datoriile",
            "Sunt dator eu",
            "Sunt datori mie"
        };

        private List<AppUser> _users = new();
        private List<DebtItem> _debts = new();
        private DebtItem? _selectedDebt;

        public DebtsPage()
        {
            InitializeComponent();

            _isAdmin = AppSession.CurrentUser?.IsAdmin == true;
            UserPickerBorder.IsVisible = _isAdmin;

            DebtTypeCombo.ItemsSource = _debtTypes;
            DebtTypeCombo.SelectedItem = _debtTypes[0];
            DebtViewCombo.ItemsSource = _debtViews;
            DebtViewCombo.SelectedItem = _debtViews[0];

            _ = LoadPageAsync();
        }

        private async System.Threading.Tasks.Task LoadPageAsync()
        {
            await LoadUsersAsync();
            await LoadDebtsAsync();

            if (DueDatePicker.SelectedDate is null)
            {
                DueDatePicker.SelectedDate = DateTimeOffset.Now;
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

        private async System.Threading.Tasks.Task LoadDebtsAsync()
        {
            var userId = _isAdmin ? null : AppSession.CurrentUser?.UserId;

            _debts = await DatabaseService.GetDebtsAsync(userId);
            ApplyDebtFilter();
        }

        private void ApplyDebtFilter()
        {
            var filteredDebts = GetFilteredDebts();

            DebtsList.ItemsSource = null;
            DebtsList.ItemsSource = filteredDebts;

            DebtsCountText.Text = $"Datorii gasite: {filteredDebts.Count}";

            var oweTotal = _debts
                .Where(item => item.DebtType == "eu_datorez" && !item.IsSettled)
                .Sum(item => item.Amount);

            var receivableTotal = _debts
                .Where(item => item.DebtType == "mie_datoreaza" && !item.IsSettled)
                .Sum(item => item.Amount);

            SummaryText.Text =
                $"Total datorii personale: {oweTotal:0.00} MDL{Environment.NewLine}" +
                $"Total sume de recuperat: {receivableTotal:0.00} MDL";
        }

        private void DebtsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _selectedDebt = DebtsList.SelectedItem as DebtItem;
            FillFormFromSelectedDebt();
        }

        private void DebtViewCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ApplyDebtFilter();
        }

        private void FillFormFromSelectedDebt()
        {
            if (_selectedDebt is null)
            {
                StatusText.Text = "Selecteaza o datorie din lista.";
                return;
            }

            PersonNameBox.Text = _selectedDebt.PersonName;
            AmountBox.Text = _selectedDebt.Amount.ToString(CultureInfo.InvariantCulture);
            DueDatePicker.SelectedDate = new DateTimeOffset(_selectedDebt.DueDate.Date);

            DebtTypeCombo.SelectedItem =
                _selectedDebt.DebtType == "mie_datoreaza"
                    ? _debtTypes[1]
                    : _debtTypes[0];

            if (_isAdmin)
            {
                UserCombo.SelectedItem = _users.FirstOrDefault(user => user.UserId == _selectedDebt.UserId);
            }

            StatusText.Text = $"Datorie selectata: {_selectedDebt.PersonName}";
        }

        private async void SaveDebt(object? sender, RoutedEventArgs e)
        {
            var userId = GetTargetUserId();
            var debtType = GetSelectedDebtType();
            var amount = ReadAmount();
            var dueDate = DueDatePicker.SelectedDate?.DateTime.Date ?? DateTime.Today;

            if (userId is null || debtType is null || amount is null || string.IsNullOrWhiteSpace(PersonNameBox.Text))
            {
                StatusText.Text = "Completeaza toate campurile corect.";
                return;
            }

            OperationResult result;

            if (DebtsList.SelectedItem is DebtItem debt)
            {
                result = await DatabaseService.UpdateDebtAsync(
                    debt.DebtId,
                    PersonNameBox.Text,
                    debtType,
                    amount.Value,
                    dueDate);
            }
            else
            {
                result = await DatabaseService.AddDebtAsync(
                    userId.Value,
                    PersonNameBox.Text,
                    debtType,
                    amount.Value,
                    dueDate);
            }

            StatusText.Text = result.Message;

            if (result.Success)
            {
                ClearForm(false);
                await LoadDebtsAsync();
            }
        }

        private async void ApplyDebtPayment(object? sender, RoutedEventArgs e)
        {
            if (DebtsList.SelectedItem is not DebtItem debt)
            {
                StatusText.Text = "Selecteaza datoria din lista pentru plata.";
                return;
            }

            var paymentAmount = ReadPaymentAmount();
            if (paymentAmount is null)
            {
                StatusText.Text = "Introdu o suma valida pentru plata.";
                return;
            }

            var result = await DatabaseService.ApplyDebtPaymentAsync(debt.DebtId, paymentAmount.Value);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                ClearForm(false);
                await LoadDebtsAsync();
            }
        }

        private async void DeleteDebt(object? sender, RoutedEventArgs e)
{
    var debt = DebtsList.SelectedItem as DebtItem ?? _selectedDebt;

    if (debt is null)
    {
        StatusText.Text = "Selecteaza datoria din lista.";
        return;
    }

    var result = await DatabaseService.DeleteDebtAsync(debt.DebtId);
    StatusText.Text = result.Message;

    if (result.Success)
    {
        _selectedDebt = null;
        DebtsList.SelectedItem = null;
        ClearForm(false);
        await LoadDebtsAsync();
    }
}

        private void ClearForm(object? sender, RoutedEventArgs e)
        {
            ClearForm(true);
        }

        private void ClearForm(bool showStatus)
        {
            _selectedDebt = null;
            DebtsList.SelectedItem = null;

            PersonNameBox.Text = string.Empty;
            AmountBox.Text = string.Empty;
            PaymentAmountBox.Text = string.Empty;
            DueDatePicker.SelectedDate = DateTimeOffset.Now;
            DebtTypeCombo.SelectedItem = _debtTypes[0];

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

        private string? GetSelectedDebtType()
        {
            return (DebtTypeCombo.SelectedItem as string) == "Sunt datori mie"
                ? "mie_datoreaza"
                : "eu_datorez";
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

        private decimal? ReadPaymentAmount()
        {
            var text = (PaymentAmountBox.Text ?? string.Empty).Trim().Replace(',', '.');

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0)
            {
                return amount;
            }

            return null;
        }

        private static string FormatDebtsList(IReadOnlyList<DebtItem> debts, string emptyText)
        {
            if (debts.Count == 0)
            {
                return emptyText;
            }

            return string.Join(
                Environment.NewLine,
                debts.OrderBy(item => item.DueDate)
                     .Select(item => $"{item.PersonName} - {item.Amount:0.00} MDL ({item.DueDate:dd.MM.yyyy})"));
        }

        private List<DebtItem> GetFilteredDebts()
        {
            var selectedView = DebtViewCombo.SelectedItem as string;

            return selectedView switch
            {
                "Sunt dator eu" => _debts.Where(item => item.DebtType == "eu_datorez" && !item.IsSettled).ToList(),
                "Sunt datori mie" => _debts.Where(item => item.DebtType == "mie_datoreaza" && !item.IsSettled).ToList(),
                _ => _debts.Where(item => !item.IsSettled).ToList()
            };
        }
    }
}
