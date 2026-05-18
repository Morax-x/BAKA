using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BugalterProject.Data;

namespace BugalterProject.pagini
{
    public partial class Dashboard : UserControl
    {
        public Dashboard()
        {
            InitializeComponent();
            _ = LoadDashboardAsync();
        }

        private async System.Threading.Tasks.Task LoadDashboardAsync()
        {
            var user = AppSession.CurrentUser;
            if (user is null)
            {
                return;
            }

            var data = await DatabaseService.GetDashboardDataAsync(user.UserId);

            BudgetValueText.Text = $"{data.CurrentBudget:0.00} MDL";
            PayableDebtsValueText.Text = $"{data.TotalPayableDebts:0.00} MDL";
            ReceivableDebtsValueText.Text = $"{data.TotalReceivableDebts:0.00} MDL";
            MonthlyExpensesText.Text = $"{data.CurrentMonthExpenses:0.00} MDL";
            BudgetInputBox.Text = data.CurrentBudget.ToString("0.##", CultureInfo.InvariantCulture);

            ApplyObligationCard(1, data.UpcomingObligations.Count > 0 ? data.UpcomingObligations[0] : null);
            ApplyObligationCard(2, data.UpcomingObligations.Count > 1 ? data.UpcomingObligations[1] : null);
            ApplyObligationCard(3, data.UpcomingObligations.Count > 2 ? data.UpcomingObligations[2] : null);
            ApplyObligationCard(4, data.UpcomingObligations.Count > 3 ? data.UpcomingObligations[3] : null);

            CategoriesSummaryText.Text = FormatCategoryTotals(data.CategoryTotals);
        }

        private void ApplyObligationCard(int index, DashboardObligation? obligation)
        {
            var title = obligation?.Title ?? "Nu exista date";
            var subtitle = obligation?.Subtitle ?? "Introdu obligatii pentru a le vedea aici";
            var amount = obligation is null ? "0.00 MDL" : $"{obligation.Amount:0.00} MDL";
            var date = obligation is null ? "-" : obligation.DueDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            switch (index)
            {
                case 1:
                    ObligationTitle1.Text = title;
                    ObligationSubtitle1.Text = subtitle;
                    ObligationAmount1.Text = amount;
                    ObligationDate1.Text = date;
                    break;
                case 2:
                    ObligationTitle2.Text = title;
                    ObligationSubtitle2.Text = subtitle;
                    ObligationAmount2.Text = amount;
                    ObligationDate2.Text = date;
                    break;
                case 3:
                    ObligationTitle3.Text = title;
                    ObligationSubtitle3.Text = subtitle;
                    ObligationAmount3.Text = amount;
                    ObligationDate3.Text = date;
                    break;
                case 4:
                    ObligationTitle4.Text = title;
                    ObligationSubtitle4.Text = subtitle;
                    ObligationAmount4.Text = amount;
                    ObligationDate4.Text = date;
                    break;
            }
        }

        private static string FormatCategoryTotals(System.Collections.Generic.IReadOnlyList<DashboardCategoryTotal> categories)
        {
            if (categories.Count == 0)
            {
                return "Nu exista cheltuieli inregistrate pentru luna curenta.";
            }

            return string.Join(
                Environment.NewLine,
                categories.Select(category => $"{category.CategoryName} - {category.Amount:0.00} MDL"));
        }

        private async void SaveBudget(object? sender, RoutedEventArgs e)
        {
            var user = AppSession.CurrentUser;
            if (user is null)
            {
                return;
            }

            var text = (BudgetInputBox.Text ?? string.Empty).Trim().Replace(',', '.');
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var budget) || budget < 0)
            {
                BudgetStatusText.Text = "Introdu o valoare valida pentru buget.";
                return;
            }

            var result = await DatabaseService.UpdateCurrentBudgetAsync(user.UserId, budget);
            BudgetStatusText.Text = result.Message;

            if (result.Success)
            {
                await LoadDashboardAsync();
            }
        }
    }
}
