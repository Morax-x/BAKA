using System;
using System.Globalization;
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
            DebtsValueText.Text = $"{data.TotalDebts:0.00} MDL";
            MonthlyExpensesText.Text = $"{data.CurrentMonthExpenses:0.00} MDL";
            BudgetInputBox.Text = data.CurrentBudget.ToString("0.##", CultureInfo.InvariantCulture);

            ApplyObligationCard(1, data.UpcomingObligations.Count > 0 ? data.UpcomingObligations[0] : null);
            ApplyObligationCard(2, data.UpcomingObligations.Count > 1 ? data.UpcomingObligations[1] : null);

            ApplyCategoryCard(1, data.CategoryTotals.Count > 0 ? data.CategoryTotals[0] : null);
            ApplyCategoryCard(2, data.CategoryTotals.Count > 1 ? data.CategoryTotals[1] : null);
            ApplyCategoryCard(3, data.CategoryTotals.Count > 2 ? data.CategoryTotals[2] : null);
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
            }
        }

        private void ApplyCategoryCard(int index, DashboardCategoryTotal? category)
        {
            var categoryName = category?.CategoryName ?? "Nu exista date";
            var amount = category is null ? "0.00 MDL" : $"{category.Amount:0.00} MDL";

            switch (index)
            {
                case 1:
                    CategoryName1.Text = categoryName;
                    CategoryAmount1.Text = amount;
                    break;
                case 2:
                    CategoryName2.Text = categoryName;
                    CategoryAmount2.Text = amount;
                    break;
                case 3:
                    CategoryName3.Text = categoryName;
                    CategoryAmount3.Text = amount;
                    break;
            }
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
