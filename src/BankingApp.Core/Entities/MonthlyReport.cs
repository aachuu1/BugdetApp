namespace BankingApp.Core.Entities;

public class MonthlyReport
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetSavings { get; set; }
    public decimal BudgetSet { get; set; }
    public decimal BudgetUsedPercent { get; set; }
    public string CategoryBreakdownJson { get; set; } = "{}";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
}
