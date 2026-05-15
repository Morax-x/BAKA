using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.Data;

namespace BugalterProject.pagini.components
{
    public partial class ExpensesPage : UserControl
    {
        private readonly bool _isAdmin;
        private List<ExpenseCategory> _categories = new();
        private List<AppUser> _users = new();
        private ExpenseItem? _selectedExpense;

        public ExpensesPage()
        {
            InitializeComponent();
            _isAdmin = AppSession.CurrentUser?.IsAdmin == true;
            UserPickerBorder.IsVisible = _isAdmin;
            _ = LoadPageAsync();
        }

        private async System.Threading.Tasks.Task LoadPageAsync()
        {
            await LoadCategoriesAsync();
            await LoadUsersAsync();
            await LoadExpensesAsync();

            if (ExpenseDatePicker.SelectedDate is null)
            {
                ExpenseDatePicker.SelectedDate = DateTimeOffset.Now;
            }
        }

        private async System.Threading.Tasks.Task LoadCategoriesAsync()
        {
            _categories = await DatabaseService.GetExpenseCategoriesAsync();
            CategoryCombo.ItemsSource = _categories;
            if (_categories.Count > 0 && CategoryCombo.SelectedItem is null)
            {
                CategoryCombo.SelectedItem = _categories[0];
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
                _users = new List<AppUser>();
                UserCombo.ItemsSource = new[] { AppSession.CurrentUser }.Where(user => user is not null).Cast<AppUser>().ToList();
                UserCombo.SelectedItem = AppSession.CurrentUser;
            }
        }

        private async System.Threading.Tasks.Task LoadExpensesAsync()
        {
            var userId = _isAdmin ? null : AppSession.CurrentUser?.UserId;
            var expenses = await DatabaseService.GetExpensesAsync(userId);
            ExpensesList.ItemsSource = expenses;
            ExpensesCountText.Text = $"Cheltuieli gasite: {expenses.Count}";

            if (_selectedExpense is not null)
            {
                ExpensesList.SelectedItem = expenses.FirstOrDefault(expense => expense.ExpenseId == _selectedExpense.ExpenseId);
            }
        }

        private void ExpensesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _selectedExpense = ExpensesList.SelectedItem as ExpenseItem;
            FillFormFromSelectedExpense();
        }

        private void FillFormFromSelectedExpense()
        {
            if (_selectedExpense is null)
            {
                StatusText.Text = "Selecteaza o cheltuiala pentru editare.";
                return;
            }

            CategoryCombo.SelectedItem = _categories.FirstOrDefault(category => category.CategoryId == _selectedExpense.CategoryId);
            AmountBox.Text = _selectedExpense.Amount.ToString(CultureInfo.InvariantCulture);
            DescriptionBox.Text = _selectedExpense.Description;
            ExpenseDatePicker.SelectedDate = new DateTimeOffset(_selectedExpense.ExpenseDate.Date);

            if (_isAdmin)
            {
                UserCombo.SelectedItem = _users.FirstOrDefault(user => user.UserId == _selectedExpense.UserId);
            }

            StatusText.Text = $"Cheltuiala selectata: {_selectedExpense.CategoryName}";
        }

        private async void AddExpense(object? sender, RoutedEventArgs e)
        {
            var userId = GetTargetUserId();
            var category = CategoryCombo.SelectedItem as ExpenseCategory;
            var amount = ReadAmount();
            var date = ExpenseDatePicker.SelectedDate?.DateTime.Date ?? DateTime.Today;

            if (userId is null || category is null || amount is null)
            {
                StatusText.Text = "Completeaza utilizatorul, categoria si suma corect.";
                return;
            }

            var result = await DatabaseService.AddExpenseAsync(userId.Value, category.CategoryId, amount.Value, DescriptionBox.Text ?? string.Empty, date);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                ClearForm(false);
                await LoadExpensesAsync();
            }
        }

        private async void UpdateExpense(object? sender, RoutedEventArgs e)
        {
            if (_selectedExpense is null)
            {
                StatusText.Text = "Selecteaza o cheltuiala inainte de actualizare.";
                return;
            }

            var category = CategoryCombo.SelectedItem as ExpenseCategory;
            var amount = ReadAmount();
            var date = ExpenseDatePicker.SelectedDate?.DateTime.Date ?? DateTime.Today;

            if (category is null || amount is null)
            {
                StatusText.Text = "Completeaza categoria si suma corect.";
                return;
            }

            var result = await DatabaseService.UpdateExpenseAsync(
                _selectedExpense.ExpenseId,
                category.CategoryId,
                amount.Value,
                DescriptionBox.Text ?? string.Empty,
                date);

            StatusText.Text = result.Message;

            if (result.Success)
            {
                await LoadExpensesAsync();
            }
        }

        private async void DeleteExpense(object? sender, RoutedEventArgs e)
        {
            if (_selectedExpense is null)
            {
                StatusText.Text = "Selecteaza o cheltuiala inainte de stergere.";
                return;
            }

            var result = await DatabaseService.DeleteExpenseAsync(_selectedExpense.ExpenseId);
            StatusText.Text = result.Message;

            if (result.Success)
            {
                _selectedExpense = null;
                ClearForm(false);
                await LoadExpensesAsync();
            }
        }

        private void ClearForm(object? sender, RoutedEventArgs e)
        {
            ClearForm(true);
        }

        private void ClearForm(bool showStatus)
        {
            _selectedExpense = null;
            ExpensesList.SelectedItem = null;

            if (_categories.Count > 0)
            {
                CategoryCombo.SelectedItem = _categories[0];
            }

            if (_isAdmin && _users.Count > 0 && UserCombo.SelectedItem is null)
            {
                UserCombo.SelectedItem = _users[0];
            }

            AmountBox.Text = string.Empty;
            DescriptionBox.Text = string.Empty;
            ExpenseDatePicker.SelectedDate = DateTimeOffset.Now;

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
    }
}
