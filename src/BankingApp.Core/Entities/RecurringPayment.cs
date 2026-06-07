using BankingApp.Core.Enums;

namespace BankingApp.Core.Entities;

// represents a scheduled payment that runs automatically via the background worker
// the worker checks nextexecutiondate daily at 08:00 and executes any due payments
public class RecurringPayment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // e.g. "Chirie lunară"
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurringFrequency Frequency { get; set; }        // daily, weekly, monthly, yearly
    public TransactionCategory Category { get; set; }
    public int DayOfMonth { get; set; } = 1;                 // which day of the month to execute (max 28 to avoid february issues)
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }                   // null means it runs indefinitely
    public DateTime? LastExecutedAt { get; set; }            // updated after each execution by the worker
    public DateTime? NextExecutionDate { get; set; }         // recalculated after each execution using computenextdate
    public bool IsActive { get; set; } = true;               // can be toggled on/off without deleting
    public string? PayeeName { get; set; }                   // optional recipient name e.g. "Proprietar"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // foreign key and navigation property for the account payments are deducted from
    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;

    // navigation property: each execution creates a transaction linked back to this payment
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}