using BankingApp.Core.Enums;

namespace BankingApp.Core.Entities;

// represents a bank account owned by a user
// one user can have multiple accounts (checking, savings, credit)
public class BankAccount
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty; // generated automatically in authservice e.g. "RO49BANK12345678"
    public string AccountName { get; set; } = string.Empty;   // user-defined name e.g. "Cont Curent BRD"
    public decimal Balance { get; set; }                       // updated every time a transaction is created or deleted
    public decimal MonthlyBudget { get; set; }                 // used to calculate budgetusedpercent in the dto
    public AccountType AccountType { get; set; } = AccountType.Checking;
    public string Currency { get; set; } = "RON";
    public bool IsDefault { get; set; } // the default account is shown first in the dashboard hero card
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // foreign key and navigation property for the owner
    public string UserId { get; set; } = string.Empty; // string because identityuser uses string ids
    public ApplicationUser User { get; set; } = null!;

    // navigation properties: one account has many transactions, recurring payments and bills
    // all configured with cascade delete in appdbcontext
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<RecurringPayment> RecurringPayments { get; set; } = new List<RecurringPayment>();
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
}