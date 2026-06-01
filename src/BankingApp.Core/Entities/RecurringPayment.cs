using BankingApp.Core.Enums;

namespace BankingApp.Core.Entities;

public class RecurringPayment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurringFrequency Frequency { get; set; }
    public TransactionCategory Category { get; set; }
    public int DayOfMonth { get; set; } = 1; // ziua lunii cand se executa
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public DateTime? NextExecutionDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PayeeName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
