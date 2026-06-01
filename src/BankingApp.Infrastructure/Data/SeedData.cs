using BankingApp.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BankingApp.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var context = services.GetRequiredService<AppDbContext>();

        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var adminEmail = "admin@bankingapp.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser { FirstName = "Admin", LastName = "System", Email = adminEmail, UserName = adminEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                context.BankAccounts.Add(new BankAccount
                {
                    AccountNumber = "RO49AAAA1B31007593840000",
                    AccountName = "Cont Principal",
                    Balance = 5000,
                    MonthlyBudget = 3000,
                    IsDefault = true,
                    UserId = admin.Id
                });
                await context.SaveChangesAsync();
            }
        }

        var demoEmail = "demo@bankingapp.com";
        if (await userManager.FindByEmailAsync(demoEmail) == null)
        {
            var demo = new ApplicationUser { FirstName = "Maria", LastName = "Ionescu", Email = demoEmail, UserName = demoEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(demo, "Demo@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(demo, "User");
                var acc = new BankAccount
                {
                    AccountNumber = "RO49BBBB1B31007593840001",
                    AccountName = "Cont Curent",
                    Balance = 8500,
                    MonthlyBudget = 4000,
                    IsDefault = true,
                    UserId = demo.Id
                };
                context.BankAccounts.Add(acc);
                await context.SaveChangesAsync();

                // Seed some transactions
                var now = DateTime.UtcNow;
                var transactions = new List<Transaction>
                {
                    new() { Description = "Salariu", Amount = 5000, Type = Core.Enums.TransactionType.Income, Category = Core.Enums.TransactionCategory.Salary, Date = now.AddDays(-25), BankAccountId = acc.Id, BalanceAfter = 5000 },
                    new() { Description = "Chirie", Amount = 1200, Type = Core.Enums.TransactionType.Expense, Category = Core.Enums.TransactionCategory.Housing, Date = now.AddDays(-20), BankAccountId = acc.Id, BalanceAfter = 3800 },
                    new() { Description = "Cumpărături", Amount = 350, Type = Core.Enums.TransactionType.Expense, Category = Core.Enums.TransactionCategory.Food, Date = now.AddDays(-15), BankAccountId = acc.Id, BalanceAfter = 3450 },
                    new() { Description = "Abonament Netflix", Amount = 45, Type = Core.Enums.TransactionType.Expense, Category = Core.Enums.TransactionCategory.Entertainment, Date = now.AddDays(-10), BankAccountId = acc.Id, BalanceAfter = 3405 },
                    new() { Description = "Benzină", Amount = 200, Type = Core.Enums.TransactionType.Expense, Category = Core.Enums.TransactionCategory.Transport, Date = now.AddDays(-5), BankAccountId = acc.Id, BalanceAfter = 3205 },
                    new() { Description = "Freelance", Amount = 1500, Type = Core.Enums.TransactionType.Income, Category = Core.Enums.TransactionCategory.Other, Date = now.AddDays(-3), BankAccountId = acc.Id, BalanceAfter = 4705 },
                    new() { Description = "Restaurant", Amount = 120, Type = Core.Enums.TransactionType.Expense, Category = Core.Enums.TransactionCategory.Food, Date = now.AddDays(-2), BankAccountId = acc.Id, BalanceAfter = 4585 },
                    new() { Description = "Farmacie", Amount = 85, Type = Core.Enums.TransactionType.Expense, Category = Core.Enums.TransactionCategory.Healthcare, Date = now.AddDays(-1), BankAccountId = acc.Id, BalanceAfter = 4500 },
                };
                context.Transactions.AddRange(transactions);

                // Seed recurring payments
                context.RecurringPayments.Add(new RecurringPayment
                {
                    Name = "Chirie lunară",
                    Description = "Plată chirie apartament",
                    Amount = 1200,
                    Frequency = Core.Enums.RecurringFrequency.Monthly,
                    Category = Core.Enums.TransactionCategory.Housing,
                    DayOfMonth = 1,
                    StartDate = now.AddMonths(-3),
                    IsActive = true,
                    PayeeName = "Proprietar",
                    BankAccountId = acc.Id,
                    NextExecutionDate = new DateTime(now.Year, now.Month, 1).AddMonths(1)
                });

                // Seed bills
                context.Bills.AddRange(new List<Bill>
                {
                    new() { Name = "Factură Electricitate", Provider = "Electrica", Amount = 150, DueDate = now.AddDays(5), Category = Core.Enums.BillCategory.Electricity, BankAccountId = acc.Id },
                    new() { Name = "Factură Internet", Provider = "RCS", Amount = 45, DueDate = now.AddDays(10), Category = Core.Enums.BillCategory.Internet, BankAccountId = acc.Id },
                    new() { Name = "Factură Apă", Provider = "Apa Nova", Amount = 80, DueDate = now.AddDays(-2), Category = Core.Enums.BillCategory.Water, Status = Core.Enums.BillStatus.Overdue, BankAccountId = acc.Id },
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
