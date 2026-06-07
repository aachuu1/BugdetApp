using BankingApp.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BankingApp.Infrastructure.Data;

// extends IdentityDbContext so asp.net identity tables (users, roles, claims) are managed here too
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // DbSet properties use the expression-bodied Set<T>() form — equivalent to a standard getter
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<RecurringPayment> RecurringPayments => Set<RecurringPayment>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<MonthlyReport> MonthlyReports => Set<MonthlyReport>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // must call base first so identity tables are configured before our own entities
        base.OnModelCreating(builder);

        builder.Entity<BankAccount>(e =>
        {
            e.HasKey(x => x.Id);
            // decimal(18,2) gives us up to 16 digits before and 2 after the decimal point
            e.Property(x => x.Balance).HasColumnType("decimal(18,2)");
            e.Property(x => x.MonthlyBudget).HasColumnType("decimal(18,2)");
            // deleting a user cascades and removes all their accounts
            e.HasOne(x => x.User).WithMany(u => u.BankAccounts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Transaction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.BalanceAfter).HasColumnType("decimal(18,2)");
            // deleting an account cascades and removes all its transactions
            e.HasOne(x => x.BankAccount).WithMany(a => a.Transactions).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
            // no action on recurring payment delete — transactions are kept as a historical record
            e.HasOne(x => x.RecurringPayment).WithMany(r => r.Transactions).HasForeignKey(x => x.RecurringPaymentId).OnDelete(DeleteBehavior.NoAction);
            // many-to-many: EF Core auto-generates the join table named TransactionTags
            e.HasMany(x => x.Tags)
             .WithMany(t => t.Transactions)
             .UsingEntity(j => j.ToTable("TransactionTags"));
        });

        builder.Entity<RecurringPayment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            // deleting an account cascades and removes all its recurring payments
            e.HasOne(x => x.BankAccount).WithMany(a => a.RecurringPayments).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Bill>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            // deleting an account cascades and removes all its bills
            e.HasOne(x => x.BankAccount).WithMany(a => a.Bills).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MonthlyReport>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TotalIncome).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalExpenses).HasColumnType("decimal(18,2)");
            e.Property(x => x.NetSavings).HasColumnType("decimal(18,2)");
            e.Property(x => x.BudgetSet).HasColumnType("decimal(18,2)");
            e.Property(x => x.BudgetUsedPercent).HasColumnType("decimal(18,2)");
            // no WithMany collection on BankAccount — reports are accessed via the repository, not navigation
            e.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Tag>(e =>
        {
            e.HasKey(x => x.Id);
            // name is required and capped at 50 chars — matches the CreateTagDto validation
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            // color stores a hex string e.g. #FF5733 — 7 chars but 20 gives room for future formats
            e.Property(x => x.Color).HasMaxLength(20);
        });
    }
}