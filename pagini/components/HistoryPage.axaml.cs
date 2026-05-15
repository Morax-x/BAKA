using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.Data;

namespace BugalterProject.pagini.components
{
    public partial class HistoryPage : UserControl
    {
        private System.Collections.Generic.List<ExpenseItem> _expenses = new();
        private System.Collections.Generic.List<DebtItem> _debts = new();
        private System.Collections.Generic.List<UtilityItem> _utilities = new();

        public HistoryPage()
        {
            InitializeComponent();
            _ = LoadHistoryAsync();
        }

        private async System.Threading.Tasks.Task LoadHistoryAsync()
        {
            var userId = AppSession.CurrentUser?.IsAdmin == true ? null : AppSession.CurrentUser?.UserId;
            _expenses = await DatabaseService.GetExpensesAsync(userId);
            _debts = await DatabaseService.GetDebtsAsync(userId, includeSettled: true);
            _utilities = await DatabaseService.GetUtilitiesAsync(userId, includePaid: true);

            var now = DateTime.Today;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);
            var previousMonthStart = currentMonthStart.AddMonths(-1);
            var nextMonthStart = currentMonthStart.AddMonths(1);

            var currentMonthExpenses = _expenses
                .Where(expense => expense.ExpenseDate >= currentMonthStart && expense.ExpenseDate < nextMonthStart)
                .OrderByDescending(expense => expense.ExpenseDate)
                .ToList();

            var previousMonthExpenses = _expenses
                .Where(expense => expense.ExpenseDate >= previousMonthStart && expense.ExpenseDate < currentMonthStart)
                .OrderByDescending(expense => expense.ExpenseDate)
                .ToList();

            CurrentMonthItemsText.Text = FormatExpenses(currentMonthExpenses, "Nu exista cheltuieli pentru luna aceasta.");
            PreviousMonthItemsText.Text = FormatExpenses(previousMonthExpenses, "Nu exista cheltuieli pentru luna trecuta.");

            var currentTotal = currentMonthExpenses.Sum(expense => expense.Amount);
            var previousTotal = previousMonthExpenses.Sum(expense => expense.Amount);
            var difference = currentTotal - previousTotal;

            CurrentMonthTotalText.Text = $"Luna aceasta: {currentTotal:0.00} MDL";
            PreviousMonthTotalText.Text = $"Luna trecuta: {previousTotal:0.00} MDL";
            DifferenceText.Text = $"Diferenta dintre luni: {difference:+0.00;-0.00;0.00} MDL";

            var settledDebts = _debts.Where(item => item.IsSettled).OrderByDescending(item => item.SettledDate ?? item.DueDate).ToList();
            var paidUtilities = _utilities.Where(item => item.IsPaid).OrderByDescending(item => item.PaidDate ?? item.UtilityDate).ToList();

            SettledDebtsText.Text = FormatSettledDebts(settledDebts, "Nu exista datorii stinse.");
            PaidUtilitiesText.Text = FormatPaidUtilities(paidUtilities, "Nu exista plati achitate.");
        }

        private static string FormatExpenses(System.Collections.Generic.IReadOnlyList<ExpenseItem> expenses, string emptyText)
        {
            if (expenses.Count == 0)
            {
                return emptyText;
            }

            return string.Join(
                Environment.NewLine,
                expenses.Select(expense =>
                    $"{expense.CategoryName} - {expense.Amount:0.00} MDL ({expense.ExpenseDate:dd.MM.yyyy})"));
        }

        private static string FormatSettledDebts(System.Collections.Generic.IReadOnlyList<DebtItem> debts, string emptyText)
        {
            if (debts.Count == 0)
            {
                return emptyText;
            }

            return string.Join(
                Environment.NewLine,
                debts.Select(debt =>
                {
                    var settledDate = debt.SettledDate ?? debt.DueDate;
                    return $"{debt.PersonName} - {debt.Amount:0.00} MDL (stinsa la {settledDate:dd.MM.yyyy})";
                }));
        }

        private static string FormatPaidUtilities(System.Collections.Generic.IReadOnlyList<UtilityItem> utilities, string emptyText)
        {
            if (utilities.Count == 0)
            {
                return emptyText;
            }

            return string.Join(
                Environment.NewLine,
                utilities.Select(utility =>
                {
                    var paidDate = utility.PaidDate ?? utility.UtilityDate;
                    return $"{utility.UtilityName} - {utility.Amount:0.00} MDL (achitata la {paidDate:dd.MM.yyyy})";
                }));
        }

        private async void ExportExpenses(object? sender, RoutedEventArgs e)
        {
            if (ExportConfirmCheck.IsChecked != true)
            {
                ExportStatusText.Text = "Confirma exportul inainte de generarea fisierului.";
                return;
            }

            if (_expenses.Count == 0)
            {
                await LoadHistoryAsync();
            }

            if (_expenses.Count == 0)
            {
                ExportStatusText.Text = "Nu exista cheltuieli pentru export.";
                return;
            }

            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var exportPath = Path.Combine(desktopPath, $"baka_expenses_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                var csv = new StringBuilder();
                csv.AppendLine("ExpenseId,User,Category,Amount,Description,ExpenseDate");

                foreach (var expense in _expenses.OrderByDescending(item => item.ExpenseDate))
                {
                    csv.AppendLine(string.Join(",",
                        expense.ExpenseId,
                        EscapeCsv(expense.OwnerName),
                        EscapeCsv(expense.CategoryName),
                        expense.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                        EscapeCsv(expense.Description),
                        expense.ExpenseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                }

                await File.WriteAllTextAsync(exportPath, csv.ToString(), Encoding.UTF8);
                ExportStatusText.Text = $"Export realizat cu succes: {exportPath}";
            }
            catch (Exception ex)
            {
                ExportStatusText.Text = $"Eroare la export: {ex.Message}";
            }
        }

        private static string EscapeCsv(string value)
        {
            var safeValue = value.Replace("\"", "\"\"");
            return $"\"{safeValue}\"";
        }
    }
}
