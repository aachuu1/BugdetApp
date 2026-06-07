using Microsoft.AspNetCore.Identity;

namespace BankingApp.Core.Entities;

// extends the default identity user with banking-specific fields
// identityuser already provides: email, passwordhash, username, phonenumber, lockout, two-factor etc.
public class ApplicationUser : IdentityUser
{
    // extra fields added on top of identityuser
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true; // can soft-disable accounts without deleting them

    // navigation property: one user can have multiple bank accounts
    // when a user is deleted, all their accounts are deleted too (cascade in appdbcontext)
    public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
}