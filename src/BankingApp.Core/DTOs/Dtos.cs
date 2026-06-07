using BankingApp.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace BankingApp.Core.DTOs;

// sent by the client when creating a new user account
public class RegisterDto
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    // must match the Password field — validated server-side via [Compare]
    [Required(ErrorMessage = "Password confirmation is required.")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// sent by the client when logging in
public class LoginDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

// returned after a successful register or login — contains the jwt token and basic user info
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

// read model for a bank account — includes computed monthly stats
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
    // computed client-side friendly percentage — returns 0 if no budget is set
    public decimal BudgetUsedPercent => MonthlyBudget > 0 ? Math.Round(MonthlyExpenses / MonthlyBudget * 100, 1) : 0;
}

// sent by the client when creating a new bank account
public class CreateBankAccountDto
{
    [Required(ErrorMessage = "Account name is required.")]
    [StringLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Initial balance cannot be negative.")]
    public decimal InitialBalance { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Budget cannot be negative.")]
    public decimal MonthlyBudget { get; set; }

    public AccountType AccountType { get; set; } = AccountType.Checking;

    // must be a standard 3-letter ISO currency code e.g. RON, EUR, USD
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be exactly 3 characters.")]
    public string Currency { get; set; } = "RON";
}

// sent by the client when updating an existing bank account
public class UpdateBankAccountDto
{
    [Required(ErrorMessage = "Account name is required.")]
    [StringLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Budget cannot be negative.")]
    public decimal MonthlyBudget { get; set; }

    public AccountType AccountType { get; set; }
    public string Currency { get; set; } = "RON";
    public bool IsDefault { get; set; }
}

// read model for a single transaction — includes the balance snapshot after it was applied
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
    // account balance at the moment this transaction was recorded
    public decimal BalanceAfter { get; set; }
    public List<TagDto> Tags { get; set; } = new();
}

// sent by the client when recording a new transaction
public class CreateTransactionDto
{
    [Required(ErrorMessage = "Description is required.")]
    [StringLength(200, ErrorMessage = "Description cannot exceed 200 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    public TransactionType Type { get; set; }
    public TransactionCategory Category { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    [Required(ErrorMessage = "Bank account is required.")]
    public int BankAccountId { get; set; }
}

// sent by the client when editing a transaction — only non-financial fields can be changed
public class UpdateTransactionDto
{
    [Required(ErrorMessage = "Description is required.")]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    public TransactionCategory Category { get; set; }
    public string? Notes { get; set; }
}

// query parameters for the GET /api/transactions endpoint — all fields are optional
public class TransactionFilterDto
{
    public int? BankAccountId { get; set; }
    public TransactionType? Type { get; set; }
    public TransactionCategory? Category { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    // searches against description and notes
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// generic wrapper for paginated list responses
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    // computed from TotalCount and PageSize — no need to set manually
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

// read model for a recurring payment — includes scheduling and execution info
public class RecurringPaymentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurringFrequency Frequency { get; set; }
    public TransactionCategory Category { get; set; }
    // day of the month on which the payment is executed e.g. 1 = first of every month
    public int DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public DateTime? NextExecutionDate { get; set; }
    public bool IsActive { get; set; }
    public string? PayeeName { get; set; }
    public string AccountName { get; set; } = string.Empty;
}

// sent by the client when setting up a new recurring payment
public class CreateRecurringPaymentDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    public RecurringFrequency Frequency { get; set; } = RecurringFrequency.Monthly;
    public TransactionCategory Category { get; set; }

    [Range(1, 31, ErrorMessage = "Day must be between 1 and 31.")]
    public int DayOfMonth { get; set; } = 1;

    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    // null means the payment runs indefinitely
    public DateTime? EndDate { get; set; }
    public string? PayeeName { get; set; }

    [Required(ErrorMessage = "Bank account is required.")]
    public int BankAccountId { get; set; }
}

// read model for a bill — includes computed overdue status and days until due
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
    // true if the bill is still pending and the due date has already passed
    public bool IsOverdue => Status == BillStatus.Pending && DueDate < DateTime.UtcNow;
    // negative value means the bill is already overdue
    public int DaysUntilDue => (int)(DueDate - DateTime.UtcNow).TotalDays;
}

// sent by the client when adding a new bill
public class CreateBillDto
{
    [Required(ErrorMessage = "Bill name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Provider { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Due date is required.")]
    public DateTime DueDate { get; set; }

    public BillCategory Category { get; set; }
    public string? Notes { get; set; }

    [Required(ErrorMessage = "Bank account is required.")]
    public int BankAccountId { get; set; }
}

// read model for a monthly report — may be served from cache (see FromCache flag)
public class MonthlyReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    // income minus expenses for the month
    public decimal NetSavings { get; set; }
    public decimal BudgetSet { get; set; }
    public decimal BudgetUsedPercent { get; set; }
    // true when the data was served from redis cache rather than recalculated
    public bool FromCache { get; set; }
    public DateTime GeneratedAt { get; set; }
    // spending broken down by category — used for pie/donut charts
    public List<CategoryExpenseDto> CategoryBreakdown { get; set; } = new();
    // spending broken down by day of month — used for bar/line charts
    public List<DailyExpenseDto> DailyBreakdown { get; set; } = new();
}

// one slice of the category breakdown in a monthly report
public class CategoryExpenseDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    // percentage of total expenses for that month
    public decimal Percentage { get; set; }
    // hex color assigned to this category for consistent chart coloring
    public string Color { get; set; } = string.Empty;
}

// one data point in the daily expense breakdown of a monthly report
public class DailyExpenseDto
{
    public int Day { get; set; }
    public decimal Amount { get; set; }
}

// aggregated overview returned by the dashboard endpoint — combines data from multiple services
public class DashboardDto
{
    // sum of balances across all accounts
    public decimal TotalBalance { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlySavings { get; set; }
    public decimal BudgetUsedPercent { get; set; }
    public int PendingBillsCount { get; set; }
    public decimal PendingBillsAmount { get; set; }
    public int OverdueBillsCount { get; set; }
    public int ActiveRecurringPayments { get; set; }
    // last few transactions shown in the activity feed
    public List<TransactionDto> RecentTransactions { get; set; } = new();
    // bills due soon shown in the reminders section
    public List<BillDto> UpcomingBills { get; set; } = new();
    public List<BankAccountDto> Accounts { get; set; } = new();
}

// read model for a tag — tags are global and shared across all users
public class TagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // hex color used to render the badge in the UI
    public string Color { get; set; } = "#6B7280";
    // how many transactions currently have this tag attached
    public int TransactionCount { get; set; }
}

// sent by the client when creating or updating a tag
public class CreateTagDto
{
    [Required(ErrorMessage = "Tag name is required.")]
    [StringLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
    public string Name { get; set; } = string.Empty;

    // must be a valid 6-digit hex color e.g. #FF5733
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex value (e.g. #FF5733).")]
    public string Color { get; set; } = "#6B7280";
}