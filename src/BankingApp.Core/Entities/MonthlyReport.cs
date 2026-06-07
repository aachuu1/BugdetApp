namespace BankingApp.Core.Entities;

// stores a snapshot of a month's financial summary for a specific account
// generated automatically by the background worker on day 1 of each month
// or on demand when a user requests a report for a past month
public class MonthlyReport
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetSavings { get; set; }          // totalincome - totalexpenses
    public decimal BudgetSet { get; set; }            // monthly budget at the time of generation
    public decimal BudgetUsedPercent { get; set; }

    // category breakdown is stored as json because it's a variable-length list
    // deserialized in reportservice when building the monthlydto
    public string CategoryBreakdownJson { get; set; } = "{}";

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // foreign key and navigation property for the account this report belongs to
    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
}