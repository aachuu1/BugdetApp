using BankingApp.Core.Enums;

namespace BankingApp.Core.Entities;

public class Bill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public BillStatus Status { get; set; } = BillStatus.Pending;
    public BillCategory Category { get; set; }
    public string? Notes { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
}
