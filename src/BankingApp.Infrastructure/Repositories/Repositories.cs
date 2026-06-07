using BankingApp.Core.DTOs;
using BankingApp.Core.Entities;
using BankingApp.Core.Interfaces;
using BankingApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankingApp.Infrastructure.Repositories;

// generic base repository — provides basic CRUD for any entity type
// specific repositories extend this and add their own queries on top
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);
    public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();

    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }
}

public class BankAccountRepository : Repository<BankAccount>, IBankAccountRepository
{
    public BankAccountRepository(AppDbContext context) : base(context) { }

    // default account is listed first — makes it easy to pre-select in the UI
    public async Task<IEnumerable<BankAccount>> GetByUserAsync(string userId)
        => await _context.BankAccounts
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ToListAsync();

    // falls back to the first account found if no default is explicitly set
    public async Task<BankAccount?> GetDefaultAccountAsync(string userId)
        => await _context.BankAccounts.FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault)
           ?? await _context.BankAccounts.FirstOrDefaultAsync(a => a.UserId == userId);

    // eager-loads transactions so the caller can access them without extra queries
    public async Task<BankAccount?> GetByIdWithDetailsAsync(int id)
        => await _context.BankAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == id);
}

public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(AppDbContext context) : base(context) { }

    // builds the query dynamically — only applies filters that are actually provided
    public async Task<PagedResult<Transaction>> GetPagedAsync(TransactionFilterDto filter)
    {
        // include related data needed by the dto mapping
        var q = _context.Transactions
            .Include(t => t.BankAccount)
            .Include(t => t.Tags)
            .AsQueryable();

        // apply optional filters
        if (filter.BankAccountId.HasValue) q = q.Where(t => t.BankAccountId == filter.BankAccountId.Value);
        if (filter.Type.HasValue) q = q.Where(t => t.Type == filter.Type.Value);
        if (filter.Category.HasValue) q = q.Where(t => t.Category == filter.Category.Value);
        if (filter.StartDate.HasValue) q = q.Where(t => t.Date >= filter.StartDate.Value);
        if (filter.EndDate.HasValue) q = q.Where(t => t.Date <= filter.EndDate.Value);
        // search matches against the description field only
        if (!string.IsNullOrEmpty(filter.Search)) q = q.Where(t => t.Description.Contains(filter.Search));

        // most recent transactions first
        q = q.OrderByDescending(t => t.Date);

        // count before pagination so the client knows total pages
        var total = await q.CountAsync();
        var items = await q.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();

        return new PagedResult<Transaction>
        {
            Items = items,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    // eager-loads tags so they can be mapped to the dto without extra queries
    public async Task<Transaction?> GetByIdWithTagsAsync(int id)
        => await _context.Transactions
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == id);

    // used by the report service to retrieve all transactions for a given month
    public async Task<IEnumerable<Transaction>> GetByAccountAndMonthAsync(int accountId, int year, int month)
        => await _context.Transactions
            .Where(t => t.BankAccountId == accountId && t.Date.Year == year && t.Date.Month == month)
            .OrderByDescending(t => t.Date)
            .ToListAsync();

    // returns 0 instead of null when no transactions match — safe to use directly in calculations
    public async Task<decimal> GetMonthlyTotalAsync(int accountId, int year, int month, Core.Enums.TransactionType type)
        => await _context.Transactions
            .Where(t => t.BankAccountId == accountId && t.Date.Year == year && t.Date.Month == month && t.Type == type)
            .SumAsync(t => t.Amount);
}

public class RecurringPaymentRepository : Repository<RecurringPayment>, IRecurringPaymentRepository
{
    public RecurringPaymentRepository(AppDbContext context) : base(context) { }

    // filters by the account owner since recurring payments don't have a direct userId column
    // active payments are listed first so they appear at the top of the UI
    public async Task<IEnumerable<RecurringPayment>> GetByUserAsync(string userId)
        => await _context.RecurringPayments
            .Include(r => r.BankAccount)
            .Where(r => r.BankAccount.UserId == userId)
            .OrderByDescending(r => r.IsActive)
            .ToListAsync();

    // used by the background worker to find payments that need to be executed today
    // compares only the date part to avoid time-of-day mismatches
    public async Task<IEnumerable<RecurringPayment>> GetDuePaymentsAsync(DateTime date)
        => await _context.RecurringPayments
            .Include(r => r.BankAccount)
            .Where(r => r.IsActive && r.NextExecutionDate.HasValue && r.NextExecutionDate.Value.Date <= date.Date)
            .ToListAsync();

    public async Task<IEnumerable<RecurringPayment>> GetByAccountAsync(int accountId)
        => await _context.RecurringPayments
            .Where(r => r.BankAccountId == accountId)
            .ToListAsync();
}

public class BillRepository : Repository<Bill>, IBillRepository
{
    public BillRepository(AppDbContext context) : base(context) { }

    // filters by account owner and sorts by due date so the most urgent bills appear first
    public async Task<IEnumerable<Bill>> GetByUserAsync(string userId)
        => await _context.Bills
            .Include(b => b.BankAccount)
            .Where(b => b.BankAccount.UserId == userId)
            .OrderBy(b => b.DueDate)
            .ToListAsync();

    public async Task<IEnumerable<Bill>> GetByAccountAsync(int accountId)
        => await _context.Bills
            .Where(b => b.BankAccountId == accountId)
            .ToListAsync();

    // returns all pending bills that have already passed their due date
    public async Task<IEnumerable<Bill>> GetOverdueBillsAsync()
        => await _context.Bills
            .Include(b => b.BankAccount)
            .Where(b => b.Status == Core.Enums.BillStatus.Pending && b.DueDate < DateTime.UtcNow)
            .ToListAsync();

    // returns pending bills due within the next n days — used by the dashboard reminders section
    public async Task<IEnumerable<Bill>> GetUpcomingBillsAsync(int days = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(days);
        return await _context.Bills
            .Include(b => b.BankAccount)
            .Where(b => b.Status == Core.Enums.BillStatus.Pending && b.DueDate <= cutoff)
            .OrderBy(b => b.DueDate)
            .ToListAsync();
    }
}

public class MonthlyReportRepository : Repository<MonthlyReport>, IMonthlyReportRepository
{
    public MonthlyReportRepository(AppDbContext context) : base(context) { }

    // looks up a previously generated report — returns null if it hasn't been generated yet
    public async Task<MonthlyReport?> GetByAccountAndMonthAsync(int accountId, int year, int month)
        => await _context.MonthlyReports
            .FirstOrDefaultAsync(r => r.BankAccountId == accountId && r.Year == year && r.Month == month);

    // returns the full report history sorted newest first
    public async Task<IEnumerable<MonthlyReport>> GetByAccountAsync(int accountId)
        => await _context.MonthlyReports
            .Where(r => r.BankAccountId == accountId)
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .ToListAsync();
}

public class TagRepository : Repository<Tag>, ITagRepository
{
    public TagRepository(AppDbContext context) : base(context) { }

    // eager-loads transactions so the transaction count can be read from the navigation property
    public async Task<Tag?> GetByIdWithTransactionsAsync(int id)
        => await _context.Tags
            .Include(t => t.Transactions)
            .FirstOrDefaultAsync(t => t.Id == id);

    // loads all tags with their transactions so TransactionCount can be derived in the service layer
    public async Task<IEnumerable<Tag>> GetAllWithCountAsync()
        => await _context.Tags
            .Include(t => t.Transactions)
            .ToListAsync();
}