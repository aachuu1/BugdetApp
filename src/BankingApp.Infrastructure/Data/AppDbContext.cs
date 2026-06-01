using BankingApp.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BankingApp.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<RecurringPayment> RecurringPayments => Set<RecurringPayment>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<MonthlyReport> MonthlyReports => Set<MonthlyReport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BankAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Balance).HasColumnType("decimal(18,2)");
            e.Property(x => x.MonthlyBudget).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.User).WithMany(u => u.BankAccounts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Transaction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.BalanceAfter).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.BankAccount).WithMany(a => a.Transactions).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RecurringPayment).WithMany(r => r.Transactions).HasForeignKey(x => x.RecurringPaymentId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<RecurringPayment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.BankAccount).WithMany(a => a.RecurringPayments).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Bill>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
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
            e.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}