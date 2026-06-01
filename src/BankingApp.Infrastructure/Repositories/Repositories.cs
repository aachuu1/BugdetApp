using BankingApp.Core.DTOs;
using BankingApp.Core.Entities;
using BankingApp.Core.Interfaces;
using BankingApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankingApp.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;
    public Repository(AppDbContext context) { _context = context; _dbSet = context.Set<T>(); }
    public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);
    public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();
    public async Task<T> AddAsync(T entity) { await _dbSet.AddAsync(entity); await _context.SaveChangesAsync(); return entity; }
    public async Task UpdateAsync(T entity) { _dbSet.Update(entity); await _context.SaveChangesAsync(); }
    public async Task DeleteAsync(T entity) { _dbSet.Remove(entity); await _context.SaveChangesAsync(); }
}

public class BankAccountRepository : Repository<BankAccount>, IBankAccountRepository
{
    public BankAccountRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<BankAccount>> GetByUserAsync(string userId)
        => await _context.BankAccounts.Where(a => a.UserId == userId).OrderByDescending(a => a.IsDefault).ToListAsync();

    public async Task<BankAccount?> GetDefaultAccountAsync(string userId)
        => await _context.BankAccounts.FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault)
           ?? await _context.BankAccounts.FirstOrDefaultAsync(a => a.UserId == userId);

    public async Task<BankAccount?> GetByIdWithDetailsAsync(int id)
        => await _context.BankAccounts.Include(a => a.Transactions).FirstOrDefaultAsync(a => a.Id == id);
}

public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(AppDbContext context) : base(context) { }

    public async Task<PagedResult<Transaction>> GetPagedAsync(TransactionFilterDto filter)
    {
        var q = _context.Transactions.Include(t => t.BankAccount).AsQueryable();

        if (filter.BankAccountId.HasValue) q = q.Where(t => t.BankAccountId == filter.BankAccountId.Value);
        if (filter.Type.HasValue) q = q.Where(t => t.Type == filter.Type.Value);
        if (filter.Category.HasValue) q = q.Where(t => t.Category == filter.Category.Value);
        if (filter.StartDate.HasValue) q = q.Where(t => t.Date >= filter.StartDate.Value);
        if (filter.EndDate.HasValue) q = q.Where(t => t.Date <= filter.EndDate.Value);
        if (!string.IsNullOrEmpty(filter.Search)) q = q.Where(t => t.Description.Contains(filter.Search));

        q = q.OrderByDescending(t => t.Date);
        var total = await q.CountAsync();
        var items = await q.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return new PagedResult<Transaction> { Items = items, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public async Task<IEnumerable<Transaction>> GetByAccountAndMonthAsync(int accountId, int year, int month)
        => await _context.Transactions
            .Where(t => t.BankAccountId == accountId && t.Date.Year == year && t.Date.Month == month)
            .OrderByDescending(t => t.Date).ToListAsync();

    public async Task<decimal> GetMonthlyTotalAsync(int accountId, int year, int month, Core.Enums.TransactionType type)
        => await _context.Transactions
            .Where(t => t.BankAccountId == accountId && t.Date.Year == year && t.Date.Month == month && t.Type == type)
            .SumAsync(t => t.Amount);
}

public class RecurringPaymentRepository : Repository<RecurringPayment>, IRecurringPaymentRepository
{
    public RecurringPaymentRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<RecurringPayment>> GetByUserAsync(string userId)
        => await _context.RecurringPayments
            .Include(r => r.BankAccount)
            .Where(r => r.BankAccount.UserId == userId)
            .OrderByDescending(r => r.IsActive).ToListAsync();

    public async Task<IEnumerable<RecurringPayment>> GetDuePaymentsAsync(DateTime date)
        => await _context.RecurringPayments
            .Include(r => r.BankAccount)
            .Where(r => r.IsActive && r.NextExecutionDate.HasValue && r.NextExecutionDate.Value.Date <= date.Date)
            .ToListAsync();

    public async Task<IEnumerable<RecurringPayment>> GetByAccountAsync(int accountId)
        => await _context.RecurringPayments.Where(r => r.BankAccountId == accountId).ToListAsync();
}

public class BillRepository : Repository<Bill>, IBillRepository
{
    public BillRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Bill>> GetByUserAsync(string userId)
        => await _context.Bills.Include(b => b.BankAccount)
            .Where(b => b.BankAccount.UserId == userId)
            .OrderBy(b => b.DueDate).ToListAsync();

    public async Task<IEnumerable<Bill>> GetByAccountAsync(int accountId)
        => await _context.Bills.Where(b => b.BankAccountId == accountId).ToListAsync();

    public async Task<IEnumerable<Bill>> GetOverdueBillsAsync()
        => await _context.Bills.Include(b => b.BankAccount)
            .Where(b => b.Status == Core.Enums.BillStatus.Pending && b.DueDate < DateTime.UtcNow)
            .ToListAsync();

    public async Task<IEnumerable<Bill>> GetUpcomingBillsAsync(int days = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(days);
        return await _context.Bills.Include(b => b.BankAccount)
            .Where(b => b.Status == Core.Enums.BillStatus.Pending && b.DueDate <= cutoff)
            .OrderBy(b => b.DueDate).ToListAsync();
    }
}

public class MonthlyReportRepository : Repository<MonthlyReport>, IMonthlyReportRepository
{
    public MonthlyReportRepository(AppDbContext context) : base(context) { }

    public async Task<MonthlyReport?> GetByAccountAndMonthAsync(int accountId, int year, int month)
        => await _context.MonthlyReports
            .FirstOrDefaultAsync(r => r.BankAccountId == accountId && r.Year == year && r.Month == month);

    public async Task<IEnumerable<MonthlyReport>> GetByAccountAsync(int accountId)
        => await _context.MonthlyReports.Where(r => r.BankAccountId == accountId)
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).ToListAsync();
}
