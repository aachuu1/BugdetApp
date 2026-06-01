using BankingApp.Core.Enums;

namespace BankingApp.Core.DTOs;

// Auth
public class RegisterDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

// BankAccount
public class BankAccountDto
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal MonthlyBudget { get; set; }
    public AccountType AccountType { get; set; }
    public string Currency { get; set; } = "RON";
    public bool IsDefault { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal BudgetUsedPercent => MonthlyBudget > 0 ? Math.Round(MonthlyExpenses / MonthlyBudget * 100, 1) : 0;
}

public class CreateBankAccountDto
{
    public string AccountName { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    public decimal MonthlyBudget { get; set; }
    public AccountType AccountType { get; set; } = AccountType.Checking;
    public string Currency { get; set; } = "RON";
}

// Transaction
public class TransactionDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionCategory Category { get; set; }
    public string CategoryLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
    public string? ReferenceNumber { get; set; }
    public bool IsRecurring { get; set; }
    public decimal BalanceAfter { get; set; }
}

public class CreateTransactionDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionCategory Category { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public int BankAccountId { get; set; }
}

public class TransactionFilterDto
{
    public int? BankAccountId { get; set; }
    public TransactionType? Type { get; set; }
    public TransactionCategory? Category { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

// RecurringPayment
public class RecurringPaymentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurringFrequency Frequency { get; set; }
    public TransactionCategory Category { get; set; }
    public int DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public DateTime? NextExecutionDate { get; set; }
    public bool IsActive { get; set; }
    public string? PayeeName { get; set; }
    public string AccountName { get; set; } = string.Empty;
}

public class CreateRecurringPaymentDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurringFrequency Frequency { get; set; } = RecurringFrequency.Monthly;
    public TransactionCategory Category { get; set; }
    public int DayOfMonth { get; set; } = 1;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public string? PayeeName { get; set; }
    public int BankAccountId { get; set; }
}

// Bill
public class BillDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public BillStatus Status { get; set; }
    public BillCategory Category { get; set; }
    public string? Notes { get; set; }
    public DateTime? PaidAt { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public bool IsOverdue => Status == BillStatus.Pending && DueDate < DateTime.UtcNow;
    public int DaysUntilDue => (int)(DueDate - DateTime.UtcNow).TotalDays;
}

public class CreateBillDto
{
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public BillCategory Category { get; set; }
    public string? Notes { get; set; }
    public int BankAccountId { get; set; }
}

// Reports
public class MonthlyReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetSavings { get; set; }
    public decimal BudgetSet { get; set; }
    public decimal BudgetUsedPercent { get; set; }
    public bool FromCache { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<CategoryExpenseDto> CategoryBreakdown { get; set; } = new();
    public List<DailyExpenseDto> DailyBreakdown { get; set; } = new();
}

public class CategoryExpenseDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class DailyExpenseDto
{
    public int Day { get; set; }
    public decimal Amount { get; set; }
}

// Dashboard
public class DashboardDto
{
    public decimal TotalBalance { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlySavings { get; set; }
    public decimal BudgetUsedPercent { get; set; }
    public int PendingBillsCount { get; set; }
    public decimal PendingBillsAmount { get; set; }
    public int OverdueBillsCount { get; set; }
    public int ActiveRecurringPayments { get; set; }
    public List<TransactionDto> RecentTransactions { get; set; } = new();
    public List<BillDto> UpcomingBills { get; set; } = new();
    public List<BankAccountDto> Accounts { get; set; } = new();
}
