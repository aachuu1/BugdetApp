using BankingApp.Core.Enums;

namespace BankingApp.Core.Entities;

// represents a bill that needs to be paid by a due date
// when paid, billservice deducts the amount from the account balance and creates a transaction
public class Bill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;       // e.g. "Factură Electricitate"
    public string? Provider { get; set; }                  // e.g. "Electrica" — optional
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }                  // used to calculate isoverdue in the dto
    public BillStatus Status { get; set; } = BillStatus.Pending; // new bills start as pending
    public BillCategory Category { get; set; }             // electricity, water, internet etc.
    public string? Notes { get; set; }
    public DateTime? PaidAt { get; set; }                  // set when paybillasync is called
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // foreign key and navigation property for the account this bill belongs to
    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
}