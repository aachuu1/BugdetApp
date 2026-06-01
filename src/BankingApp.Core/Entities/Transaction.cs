using BankingApp.Core.Enums;

namespace BankingApp.Core.Entities;

public class Transaction
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionCategory Category { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public string? ReferenceNumber { get; set; }
    public bool IsRecurring { get; set; }
    public decimal BalanceAfter { get; set; }

    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;

    public int? RecurringPaymentId { get; set; }
    public RecurringPayment? RecurringPayment { get; set; }
}
