using BankingApp.Core.DTOs;
using BankingApp.Core.Entities;

namespace BankingApp.Core.Interfaces;

// Repositories
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}

public interface IBankAccountRepository : IRepository<BankAccount>
{
    Task<IEnumerable<BankAccount>> GetByUserAsync(string userId);
    Task<BankAccount?> GetDefaultAccountAsync(string userId);
    Task<BankAccount?> GetByIdWithDetailsAsync(int id);
}

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<PagedResult<Transaction>> GetPagedAsync(TransactionFilterDto filter);
    Task<IEnumerable<Transaction>> GetByAccountAndMonthAsync(int accountId, int year, int month);
    Task<decimal> GetMonthlyTotalAsync(int accountId, int year, int month, Core.Enums.TransactionType type);
}

public interface IRecurringPaymentRepository : IRepository<RecurringPayment>
{
    Task<IEnumerable<RecurringPayment>> GetByUserAsync(string userId);
    Task<IEnumerable<RecurringPayment>> GetDuePaymentsAsync(DateTime date);
    Task<IEnumerable<RecurringPayment>> GetByAccountAsync(int accountId);
}

public interface IBillRepository : IRepository<Bill>
{
    Task<IEnumerable<Bill>> GetByUserAsync(string userId);
    Task<IEnumerable<Bill>> GetByAccountAsync(int accountId);
    Task<IEnumerable<Bill>> GetOverdueBillsAsync();
    Task<IEnumerable<Bill>> GetUpcomingBillsAsync(int days = 7);
}

public interface IMonthlyReportRepository : IRepository<MonthlyReport>
{
    Task<MonthlyReport?> GetByAccountAndMonthAsync(int accountId, int year, int month);
    Task<IEnumerable<MonthlyReport>> GetByAccountAsync(int accountId);
}

// Services
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
}

public interface IBankAccountService
{
    Task<IEnumerable<BankAccountDto>> GetUserAccountsAsync(string userId);
    Task<BankAccountDto?> GetAccountByIdAsync(int id, string userId);
    Task<BankAccountDto> CreateAccountAsync(CreateBankAccountDto dto, string userId);
    Task<BankAccountDto> UpdateBudgetAsync(int id, decimal budget, string userId);
    Task DeleteAccountAsync(int id, string userId);
}

public interface ITransactionService
{
    Task<PagedResult<TransactionDto>> GetTransactionsAsync(TransactionFilterDto filter, string userId);
    Task<TransactionDto> CreateTransactionAsync(CreateTransactionDto dto, string userId);
    Task DeleteTransactionAsync(int id, string userId);
    Task<DashboardDto> GetDashboardAsync(string userId);
}

public interface IRecurringPaymentService
{
    Task<IEnumerable<RecurringPaymentDto>> GetByUserAsync(string userId);
    Task<RecurringPaymentDto> CreateAsync(CreateRecurringPaymentDto dto, string userId);
    Task<RecurringPaymentDto> ToggleActiveAsync(int id, string userId);
    Task DeleteAsync(int id, string userId);
    Task ExecuteDuePaymentsAsync();
}

public interface IBillService
{
    Task<IEnumerable<BillDto>> GetByUserAsync(string userId);
    Task<BillDto> CreateAsync(CreateBillDto dto, string userId);
    Task<BillDto> PayBillAsync(int id, string userId);
    Task DeleteAsync(int id, string userId);
}

public interface IReportService
{
    Task<MonthlyReportDto> GetMonthlyReportAsync(int accountId, int year, int month, string userId);
    Task GenerateMonthlyReportsAsync(int year, int month);
}
