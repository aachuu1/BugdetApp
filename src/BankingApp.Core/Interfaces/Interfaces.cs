using BankingApp.Core.DTOs;
using BankingApp.Core.Entities;

namespace BankingApp.Core.Interfaces;

// generic repository with basic CRUD operations — all specific repositories extend this
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}

// extends the generic repository with account-specific queries
public interface IBankAccountRepository : IRepository<BankAccount>
{
    // returns all accounts belonging to a specific user
    Task<IEnumerable<BankAccount>> GetByUserAsync(string userId);
    // returns the account marked as default for the user, or null if none is set
    Task<BankAccount?> GetDefaultAccountAsync(string userId);
    // eager-loads related data needed for detailed views
    Task<BankAccount?> GetByIdWithDetailsAsync(int id);
}

// extends the generic repository with transaction-specific queries
public interface ITransactionRepository : IRepository<Transaction>
{
    // returns a filtered and paginated list of transactions
    Task<PagedResult<Transaction>> GetPagedAsync(TransactionFilterDto filter);
    // used by the report service to calculate monthly totals
    Task<IEnumerable<Transaction>> GetByAccountAndMonthAsync(int accountId, int year, int month);
    // returns the sum of all transactions of a given type for a specific account and month
    Task<decimal> GetMonthlyTotalAsync(int accountId, int year, int month, Core.Enums.TransactionType type);
    // eager-loads tags so they can be mapped to the dto without extra queries
    Task<Transaction?> GetByIdWithTagsAsync(int id);
}

// extends the generic repository with recurring payment-specific queries
public interface IRecurringPaymentRepository : IRepository<RecurringPayment>
{
    // returns all recurring payments belonging to a specific user
    Task<IEnumerable<RecurringPayment>> GetByUserAsync(string userId);
    // returns active payments whose next execution date is on or before the given date
    Task<IEnumerable<RecurringPayment>> GetDuePaymentsAsync(DateTime date);
    // returns all recurring payments linked to a specific account
    Task<IEnumerable<RecurringPayment>> GetByAccountAsync(int accountId);
}

// extends the generic repository with bill-specific queries
public interface IBillRepository : IRepository<Bill>
{
    // returns all bills belonging to a specific user across all their accounts
    Task<IEnumerable<Bill>> GetByUserAsync(string userId);
    // returns all bills linked to a specific account
    Task<IEnumerable<Bill>> GetByAccountAsync(int accountId);
    // returns pending bills whose due date has already passed
    Task<IEnumerable<Bill>> GetOverdueBillsAsync();
    // returns pending bills due within the next n days — defaults to 7
    Task<IEnumerable<Bill>> GetUpcomingBillsAsync(int days = 7);
}

// extends the generic repository with monthly report-specific queries
public interface IMonthlyReportRepository : IRepository<MonthlyReport>
{
    // looks up a previously generated report for a specific account and month
    Task<MonthlyReport?> GetByAccountAndMonthAsync(int accountId, int year, int month);
    // returns the full report history for an account
    Task<IEnumerable<MonthlyReport>> GetByAccountAsync(int accountId);
}

// extends the generic repository with tag-specific queries
public interface ITagRepository : IRepository<Tag>
{
    // eager-loads transactions so the transaction count can be calculated
    Task<Tag?> GetByIdWithTransactionsAsync(int id);
    // returns all tags with their transaction counts — used by the tags list page
    Task<IEnumerable<Tag>> GetAllWithCountAsync();
}

// ─── Service interfaces ───────────────────────────────────────

// handles user registration and login — returns a jwt token on success
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
}

// handles all bank account operations — userId is always passed to enforce ownership
public interface IBankAccountService
{
    Task<IEnumerable<BankAccountDto>> GetUserAccountsAsync(string userId);
    // returns null if the account doesn't exist or belongs to another user
    Task<BankAccountDto?> GetAccountByIdAsync(int id, string userId);
    Task<BankAccountDto> CreateAccountAsync(CreateBankAccountDto dto, string userId);
    Task<BankAccountDto> UpdateAccountAsync(int id, UpdateBankAccountDto dto, string userId);
    // dedicated method for the PATCH /budget endpoint — updates only the monthly budget
    Task<BankAccountDto> UpdateBudgetAsync(int id, decimal budget, string userId);
    Task DeleteAccountAsync(int id, string userId);
}

// handles all transaction operations — userId is always passed to enforce ownership
public interface ITransactionService
{
    Task<PagedResult<TransactionDto>> GetTransactionsAsync(TransactionFilterDto filter, string userId);
    // returns null if the transaction doesn't exist or belongs to another user
    Task<TransactionDto?> GetByIdAsync(int id, string userId);
    // creates the transaction and updates the account balance atomically
    Task<TransactionDto> CreateTransactionAsync(CreateTransactionDto dto, string userId);
    Task<TransactionDto> UpdateTransactionAsync(int id, UpdateTransactionDto dto, string userId);
    // reverses the balance change before deleting the transaction
    Task DeleteTransactionAsync(int id, string userId);
    // aggregates data from accounts, transactions, and bills into a single dashboard response
    Task<DashboardDto> GetDashboardAsync(string userId);
}

// handles recurring payment setup and execution — userId enforces ownership
public interface IRecurringPaymentService
{
    Task<IEnumerable<RecurringPaymentDto>> GetByUserAsync(string userId);
    // returns null if the payment doesn't exist or belongs to another user
    Task<RecurringPaymentDto?> GetByIdAsync(int id, string userId);
    Task<RecurringPaymentDto> CreateAsync(CreateRecurringPaymentDto dto, string userId);
    Task<RecurringPaymentDto> UpdateAsync(int id, CreateRecurringPaymentDto dto, string userId);
    // flips the IsActive flag — used by the PATCH /toggle endpoint
    Task<RecurringPaymentDto> ToggleActiveAsync(int id, string userId);
    Task DeleteAsync(int id, string userId);
    // processes all due payments — called by the background worker or the admin endpoint
    Task ExecuteDuePaymentsAsync();
}

// handles bill tracking and payment — userId enforces ownership
public interface IBillService
{
    Task<IEnumerable<BillDto>> GetByUserAsync(string userId);
    // returns null if the bill doesn't exist or belongs to another user
    Task<BillDto?> GetByIdAsync(int id, string userId);
    Task<BillDto> CreateAsync(CreateBillDto dto, string userId);
    Task<BillDto> UpdateAsync(int id, CreateBillDto dto, string userId);
    // deducts the bill amount from the account balance and marks the bill as paid
    Task<BillDto> PayBillAsync(int id, string userId);
    Task DeleteAsync(int id, string userId);
}

// handles monthly report generation and retrieval — reports may be served from redis cache
public interface IReportService
{
    // returns a cached report if available, otherwise calculates it from transactions
    Task<MonthlyReportDto> GetMonthlyReportAsync(int accountId, int year, int month, string userId);
    // generates and persists reports for all accounts — called by the background worker on day 1
    Task GenerateMonthlyReportsAsync(int year, int month);
}

// handles tag management and linking tags to transactions
public interface ITagService
{
    // tags are global — no userId filter needed for read operations
    Task<IEnumerable<TagDto>> GetAllAsync();
    Task<TagDto?> GetByIdAsync(int id);
    Task<TagDto> CreateAsync(CreateTagDto dto);
    Task<TagDto> UpdateAsync(int id, CreateTagDto dto);
    // only admins can delete tags — enforced at the controller level via [Authorize(Roles = "Admin")]
    Task DeleteAsync(int id);
    // userId is used to verify the transaction belongs to the current user before linking
    Task AddToTransactionAsync(int tagId, int transactionId, string userId);
    Task RemoveFromTransactionAsync(int tagId, int transactionId, string userId);
}