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

            CurrentMonthTotalText.Text = $"{currentTotal:0.00} MDL";
            PreviousMonthTotalText.Text = $"{previousTotal:0.00} MDL";

            var settledDebts = _debts.Where(item => item.IsSettled).OrderByDescending(item => item.SettledDate ?? item.DueDate).ToList();
            var paidUtilities = _utilities.Where(item => item.IsPaid).OrderByDescending(item => item.PaidDate ?? item.UtilityDate).ToList();
            var totalPaidUtilities = paidUtilities.Sum(item => item.Amount);

            SettledDebtsCountText.Text = settledDebts.Count.ToString(CultureInfo.InvariantCulture);
            PaidUtilitiesCountText.Text = paidUtilities.Count.ToString(CultureInfo.InvariantCulture);

            DifferenceText.Text =
                $"Luna aceasta au fost inregistrate {currentMonthExpenses.Count} cheltuieli, " +
                $"iar luna trecuta {previousMonthExpenses.Count}. Diferenta valorica este {difference:+0.00;-0.00;0.00} MDL. " +
                $"In istoric mai apar {settledDebts.Count} datorii stinse si {paidUtilities.Count} plati comunale achitate ({totalPaidUtilities:0.00} MDL).";

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
                    $"{expense.ExpenseDate:dd.MM.yyyy} | {expense.CategoryName} | {expense.Amount:0.00} MDL | {expense.OwnerName}" +
                    $"{FormatOptionalDescription(expense.Description)}"));
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
                    return $"{settledDate:dd.MM.yyyy} | {debt.PersonName} | {debt.OriginalAmount:0.00} MDL | {debt.OwnerName}";
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
                    return $"{paidDate:dd.MM.yyyy} | {utility.UtilityName} | {utility.Amount:0.00} MDL | {utility.OwnerName}";
                }));
        }

        private async void ExportHistory(object? sender, RoutedEventArgs e)
        {
            if (_expenses.Count == 0 && _debts.Count == 0 && _utilities.Count == 0)
            {
                await LoadHistoryAsync();
            }

            if (_expenses.Count == 0 && _debts.Count == 0 && _utilities.Count == 0)
            {
                ExportStatusText.Text = "Nu exista date in istoric pentru export.";
                return;
            }

            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var exportPath = Path.Combine(desktopPath, $"baka_history_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                var csv = new StringBuilder();
                csv.AppendLine("Type,RecordId,User,CategoryOrName,Amount,Details,PrimaryDate,StatusDate");

                foreach (var expense in _expenses.OrderByDescending(item => item.ExpenseDate))
                {
                    csv.AppendLine(string.Join(",",
                        "Expense",
                        expense.ExpenseId,
                        EscapeCsv(expense.OwnerName),
                        EscapeCsv(expense.CategoryName),
                        expense.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                        EscapeCsv(expense.Description),
                        expense.ExpenseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        string.Empty));
                }

                foreach (var debt in _debts.OrderByDescending(item => item.SettledDate ?? item.DueDate))
                {
                    csv.AppendLine(string.Join(",",
                        "Debt",
                        debt.DebtId,
                        EscapeCsv(debt.OwnerName),
                        EscapeCsv(debt.PersonName),
                        debt.OriginalAmount.ToString("0.00", CultureInfo.InvariantCulture),
                        EscapeCsv(debt.DebtTypeLabel),
                        debt.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        debt.SettledDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty));
                }

                foreach (var utility in _utilities.OrderByDescending(item => item.PaidDate ?? item.UtilityDate))
                {
                    csv.AppendLine(string.Join(",",
                        "Utility",
                        utility.UtilityId,
                        EscapeCsv(utility.OwnerName),
                        EscapeCsv(utility.UtilityName),
                        utility.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                        EscapeCsv(utility.IsPaid ? "Achitata" : "Activa"),
                        utility.UtilityDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        utility.PaidDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty));
                }

                await File.WriteAllTextAsync(exportPath, csv.ToString(), Encoding.UTF8);
                ExportStatusText.Text = $"Export realizat cu succes: {exportPath}";
            }
            catch (Exception ex)
            {
                ExportStatusText.Text = $"Eroare la export: {ex.Message}";
            }
        }

        private async void ClearHistory(object? sender, RoutedEventArgs e)
        {
            if (ClearHistoryConfirmCheck.IsChecked != true)
            {
                ClearHistoryStatusText.Text = "Confirma stergerea istoricului inainte de continuare.";
                return;
            }

            var userId = AppSession.CurrentUser?.IsAdmin == true ? null : AppSession.CurrentUser?.UserId;
            var result = await DatabaseService.ClearHistoryAsync(userId);
            ClearHistoryStatusText.Text = result.Message;

            if (!result.Success)
            {
                return;
            }

            ClearHistoryConfirmCheck.IsChecked = false;
            ExportStatusText.Text = string.Empty;
            await LoadHistoryAsync();
        }

        private static string EscapeCsv(string value)
        {
            var safeValue = value.Replace("\"", "\"\"");
            return $"\"{safeValue}\"";
        }

        private static string FormatOptionalDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return string.Empty;
            }

            return $" | {description.Trim()}";
        }
    }
}
