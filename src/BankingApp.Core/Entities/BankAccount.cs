using BankingApp.Core.Enums;

namespace BankingApp.Core.Entities;

public class BankAccount
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal MonthlyBudget { get; set; }
    public AccountType AccountType { get; set; } = AccountType.Checking;
    public string Currency { get; set; } = "RON";
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<RecurringPayment> RecurringPayments { get; set; } = new List<RecurringPayment>();
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
}
