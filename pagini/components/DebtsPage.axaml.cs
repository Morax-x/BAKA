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
                    UserCombo.SelectedItem = _users.FirstOrDefault(user => user.UserId == AppSession.CurrentUser.UserId) ?? _users.FirstOrDefault();
                }
            }
            else
            {
                UserCombo.ItemsSource = new[] { AppSession.CurrentUser }.Where(user => user is not null).Cast<AppUser>().ToList();
                UserCombo.SelectedItem = AppSession.CurrentUser;
            }
        }

        private async System.Threading.Tasks.Task LoadDebtsAsync()
        {
            var userId = _isAdmin ? null : AppSession.CurrentUser?.UserId;
            _debts = await DatabaseService.GetDebtsAsync(userId);

            DebtsList.ItemsSource = _debts;
            DebtsCountText.Text = $"Datorii gasite: {_debts.Count}";

            var oweTotal = _debts.Where(item => item.DebtType == "eu_datorez").Sum(item => item.Amount);
            var receivableTotal = _debts.Where(item => item.DebtType == "mie_datoreaza").Sum(item => item.Amount);
            SummaryText.Text =
                $"Total datorii personale: {oweTotal:0.00} MDL{Environment.NewLine}" +
                $"Total sume de recuperat: {receivableTotal:0.00} MDL";

            OweListText.Text = FormatDebtsList(_debts.Where(item => item.DebtType == "eu_datorez").ToList(), "Nu exista datorii active.");
            ReceivableListText.Text = FormatDebtsList(_debts.Where(item => item.DebtType == "mie_datoreaza").ToList(), "Nu exista sume de recuperat.");

            if (_selectedDebt is not null)
            {
                DebtsList.SelectedItem = _debts.FirstOrDefault(item => item.DebtId == _selectedDebt.DebtId);
            }
        }

        private void DebtsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _selectedDebt = DebtsList.SelectedItem as DebtItem;
            FillFormFromSelectedDebt();
        }

        private void FillFormFromSelectedDebt()
        {
            if (_selectedDebt is null)
            {
                StatusText.Text = "Selecteaza o datorie pentru editare.";
                return;
            }

            PersonNameBox.Text = _selectedDebt.PersonName;
            AmountBox.Text = _selectedDebt.Amount.ToString(CultureInfo.InvariantCulture);
            DueDatePicker.SelectedDate = new DateTimeOffset(_selectedDebt.DueDate.Date);
            DebtTypeCombo.SelectedItem = _selectedDebt.DebtType == "mie_datoreaza" ? _debtTypes[1] : _debtTypes[0];

            if (_isAdmin)
            {
                UserCombo.SelectedItem = _users.FirstOrDefault(user => user.UserId == _selectedDebt.UserId);
            }

            StatusText.Text = $"Datorie selectata: {_selectedDebt.PersonName}";
        }

        private async void AddDebt(object? sender, RoutedEventArgs e)
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

            var result = await DatabaseService.AddDebtAsync(
                userId.Value,
                PersonNameBox.Text ?? string.Empty,
                debtType,
                amount.Value,
                dueDate);

            StatusText.Text = result.Message;
            if (result.Success)
            {
                ClearForm(false);
                await LoadDebtsAsync();
            }
        }

        private async void UpdateDebt(object? sender, RoutedEventArgs e)
        {
            if (_selectedDebt is null)
            {
                StatusText.Text = "Selecteaza o datorie inainte de actualizare.";
                return;
            }

            var debtType = GetSelectedDebtType();
            var amount = ReadAmount();
            var dueDate = DueDatePicker.SelectedDate?.DateTime.Date ?? DateTime.Today;

            if (debtType is null || amount is null || string.IsNullOrWhiteSpace(PersonNameBox.Text))
            {
                StatusText.Text = "Completeaza toate campurile corect.";
                return;
            }

            var result = await DatabaseService.UpdateDebtAsync(
                _selectedDebt.DebtId,
                PersonNameBox.Text ?? string.Empty,
                debtType,
                amount.Value,
                dueDate);

            StatusText.Text = result.Message;
            if (result.Success)
            {
                await LoadDebtsAsync();
            }
        }

        private async void DeleteDebt(object? sender, RoutedEventArgs e)
        {
            if (_selectedDebt is null)
            {
                StatusText.Text = "Selecteaza o datorie inainte de stergere.";
                return;
            }

            var result = await DatabaseService.DeleteDebtAsync(_selectedDebt.DebtId);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                _selectedDebt = null;
                ClearForm(false);
                await LoadDebtsAsync();
            }
        }

        private async void SettleDebt(object? sender, RoutedEventArgs e)
        {
            if (_selectedDebt is null)
            {
                StatusText.Text = "Selecteaza o datorie inainte de inchidere.";
                return;
            }

            var result = await DatabaseService.MarkDebtSettledAsync(_selectedDebt.DebtId);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                _selectedDebt = null;
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

        private static string FormatDebtsList(IReadOnlyList<DebtItem> debts, string emptyText)
        {
            if (debts.Count == 0)
            {
                return emptyText;
            }

            return string.Join(
                Environment.NewLine,
                debts.OrderBy(item => item.DueDate).Select(item =>
                    $"{item.PersonName} - {item.Amount:0.00} MDL ({item.DueDate:dd.MM.yyyy})"));
        }
    }
}
