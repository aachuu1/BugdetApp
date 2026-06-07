using BankingApp.Core.DTOs;
using BankingApp.Core.Entities;
using BankingApp.Core.Enums;
using BankingApp.Core.Exceptions;
using BankingApp.Core.Interfaces;
using BankingApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BankingApp.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _config;
    private readonly AppDbContext _context;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration config,
        AppDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _config = config;
        _context = context;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        // guard against mismatched passwords before hitting the database
        if (dto.Password != dto.ConfirmPassword)
            throw new ValidationException("Passwords do not match.");

        // email uniqueness check — identity would catch this too but this gives a cleaner error
        if (await _userManager.FindByEmailAsync(dto.Email) != null)
            throw new ValidationException("Email already in use.");

        var user = new ApplicationUser
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            UserName = dto.Email // username mirrors email for simplicity
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new ValidationException(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        // all new users get the basic User role by default
        await _userManager.AddToRoleAsync(user, "User");

        // create a default checking account so the user has something to work with immediately
        var account = new BankAccount
        {
            AccountNumber = GenerateAccountNumber(),
            AccountName = "Main Account",
            Balance = 0,
            MonthlyBudget = 0,
            IsDefault = true,
            UserId = user.Id
        };
        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        return await BuildResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        // use the same generic error message for both not-found and wrong password to avoid user enumeration
        var user = await _userManager.FindByEmailAsync(dto.Email)
            ?? throw new UnauthorizedException("Invalid credentials.");

        // lockoutOnFailure: true increments the access failed count and can lock the account
        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            throw new UnauthorizedException("Invalid credentials.");

        return await BuildResponse(user);
    }

    // builds the jwt token and assembles the auth response — shared between register and login
    private async Task<AuthResponseDto> BuildResponse(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        // claims embedded in the token — controllers read UserId via ClaimTypes.NameIdentifier
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            // jti gives each token a unique id — useful for revocation
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // add one claim per role so [Authorize(Roles = "Admin")] works out of the box
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AuthResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            UserId = user.Id,
            Email = user.Email!,
            FullName = $"{user.FirstName} {user.LastName}",
            Roles = roles.ToList(),
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
    }

    // generates a Romanian IBAN-style account number — not a real IBAN, just a display number
    private static string GenerateAccountNumber()
        => $"RO49BANK{new Random().Next(10000000, 99999999)}{new Random().Next(100000, 999999)}";
}

public class BankAccountService : IBankAccountService
{
    private readonly IBankAccountRepository _repo;
    private readonly ITransactionRepository _txRepo;

    public BankAccountService(IBankAccountRepository repo, ITransactionRepository txRepo)
    {
        _repo = repo;
        _txRepo = txRepo;
    }

    // fetches monthly income and expenses for each account to populate the dashboard stats
    public async Task<IEnumerable<BankAccountDto>> GetUserAccountsAsync(string userId)
    {
        var accounts = await _repo.GetByUserAsync(userId);
        var now = DateTime.UtcNow;
        var result = new List<BankAccountDto>();

        foreach (var a in accounts)
        {
            var expenses = await _txRepo.GetMonthlyTotalAsync(a.Id, now.Year, now.Month, TransactionType.Expense);
            var income = await _txRepo.GetMonthlyTotalAsync(a.Id, now.Year, now.Month, TransactionType.Income);
            result.Add(MapToDto(a, expenses, income));
        }

        return result;
    }

    // returns null if the account doesn't exist or belongs to a different user
    public async Task<BankAccountDto?> GetAccountByIdAsync(int id, string userId)
    {
        var a = await _repo.GetByIdAsync(id);
        if (a == null || a.UserId != userId) return null;

        var now = DateTime.UtcNow;
        var expenses = await _txRepo.GetMonthlyTotalAsync(a.Id, now.Year, now.Month, TransactionType.Expense);
        var income = await _txRepo.GetMonthlyTotalAsync(a.Id, now.Year, now.Month, TransactionType.Income);
        return MapToDto(a, expenses, income);
    }

    public async Task<BankAccountDto> CreateAccountAsync(CreateBankAccountDto dto, string userId)
    {
        var account = new BankAccount
        {
            // use a guid-based number for uniqueness — trimmed to 16 chars to fit the format
            AccountNumber = $"RO49BANK{Guid.NewGuid().ToString()[..16].Replace("-", "").ToUpper()}",
            AccountName = dto.AccountName,
            Balance = dto.InitialBalance,
            MonthlyBudget = dto.MonthlyBudget,
            AccountType = dto.AccountType,
            Currency = dto.Currency,
            UserId = userId
        };
        await _repo.AddAsync(account);

        // record an opening balance transaction if the account starts with funds
        if (dto.InitialBalance > 0)
        {
            await _txRepo.AddAsync(new Transaction
            {
                Description = "Opening balance",
                Amount = dto.InitialBalance,
                Type = TransactionType.Income,
                Category = TransactionCategory.Other,
                Date = DateTime.UtcNow,
                BankAccountId = account.Id,
                BalanceAfter = dto.InitialBalance
            });
        }

        return MapToDto(account, 0, dto.InitialBalance);
    }

    public async Task<BankAccountDto> UpdateAccountAsync(int id, UpdateBankAccountDto dto, string userId)
    {
        var a = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(BankAccount), id);
        if (a.UserId != userId) throw new UnauthorizedException();

        a.AccountName = dto.AccountName;
        a.MonthlyBudget = dto.MonthlyBudget;
        a.AccountType = dto.AccountType;
        a.Currency = dto.Currency;
        a.IsDefault = dto.IsDefault;
        await _repo.UpdateAsync(a);

        // monthly stats are not recalculated here — caller can request them separately
        return MapToDto(a, 0, 0);
    }

    // dedicated method for the PATCH /budget endpoint — only touches MonthlyBudget
    public async Task<BankAccountDto> UpdateBudgetAsync(int id, decimal budget, string userId)
    {
        var a = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(BankAccount), id);
        if (a.UserId != userId) throw new UnauthorizedException();

        a.MonthlyBudget = budget;
        await _repo.UpdateAsync(a);
        return MapToDto(a, 0, 0);
    }

    public async Task DeleteAccountAsync(int id, string userId)
    {
        var a = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(BankAccount), id);
        if (a.UserId != userId) throw new UnauthorizedException();
        await _repo.DeleteAsync(a);
    }

    private static BankAccountDto MapToDto(BankAccount a, decimal expenses, decimal income) => new()
    {
        Id = a.Id,
        AccountNumber = a.AccountNumber,
        AccountName = a.AccountName,
        Balance = a.Balance,
        MonthlyBudget = a.MonthlyBudget,
        AccountType = a.AccountType,
        Currency = a.Currency,
        IsDefault = a.IsDefault,
        MonthlyExpenses = expenses,
        MonthlyIncome = income
    };
}

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _txRepo;
    private readonly IBankAccountRepository _accRepo;
    private readonly IBillRepository _billRepo;
    private readonly IRecurringPaymentRepository _recurRepo;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository txRepo,
        IBankAccountRepository accRepo,
        IBillRepository billRepo,
        IRecurringPaymentRepository recurRepo,
        ILogger<TransactionService> logger)
    {
        _txRepo = txRepo;
        _accRepo = accRepo;
        _billRepo = billRepo;
        _recurRepo = recurRepo;
        _logger = logger;
    }

    public async Task<PagedResult<TransactionDto>> GetTransactionsAsync(TransactionFilterDto filter, string userId)
    {
        var accounts = await _accRepo.GetByUserAsync(userId);
        var accountIds = accounts.Select(a => a.Id).ToList();

        // reject requests for an account the user doesn't own
        if (filter.BankAccountId.HasValue && !accountIds.Contains(filter.BankAccountId.Value))
            return new PagedResult<TransactionDto>();

        // default to the first account when no filter is specified
        if (!filter.BankAccountId.HasValue && accountIds.Any())
            filter.BankAccountId = accountIds.First();

        var result = await _txRepo.GetPagedAsync(filter);
        return new PagedResult<TransactionDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    // returns null if the transaction doesn't exist or belongs to a different user
    public async Task<TransactionDto?> GetByIdAsync(int id, string userId)
    {
        var tx = await _txRepo.GetByIdWithTagsAsync(id);
        if (tx == null) return null;

        var account = await _accRepo.GetByIdAsync(tx.BankAccountId);
        if (account?.UserId != userId) return null;

        return MapToDto(tx);
    }

    public async Task<TransactionDto> CreateTransactionAsync(CreateTransactionDto dto, string userId)
    {
        var account = await _accRepo.GetByIdAsync(dto.BankAccountId)
            ?? throw new NotFoundException(nameof(BankAccount), dto.BankAccountId);
        if (account.UserId != userId) throw new UnauthorizedException();

        // block expenses that would take the balance negative
        if (dto.Type == TransactionType.Expense && account.Balance < dto.Amount)
            throw new InsufficientFundsException(account.Balance, dto.Amount);

        // update balance before persisting the transaction so BalanceAfter is accurate
        account.Balance = dto.Type == TransactionType.Income
            ? account.Balance + dto.Amount
            : account.Balance - dto.Amount;
        await _accRepo.UpdateAsync(account);

        var tx = new Transaction
        {
            Description = dto.Description,
            Amount = dto.Amount,
            Type = dto.Type,
            Category = dto.Category,
            Date = dto.Date,
            Notes = dto.Notes,
            BankAccountId = dto.BankAccountId,
            BalanceAfter = account.Balance,
            // reference number format: TXN + timestamp + random suffix for uniqueness
            ReferenceNumber = $"TXN{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(100, 999)}"
        };
        await _txRepo.AddAsync(tx);

        _logger.LogInformation("Transaction created: {Amount} {Type} for account {AccountId}",
            dto.Amount, dto.Type, dto.BankAccountId);

        return MapToDto(tx);
    }

    // only non-financial fields can be edited — amount and type are immutable after creation
    public async Task<TransactionDto> UpdateTransactionAsync(int id, UpdateTransactionDto dto, string userId)
    {
        var tx = await _txRepo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(Transaction), id);
        var account = await _accRepo.GetByIdAsync(tx.BankAccountId);
        if (account?.UserId != userId) throw new UnauthorizedException();

        tx.Description = dto.Description;
        tx.Category = dto.Category;
        tx.Notes = dto.Notes;
        await _txRepo.UpdateAsync(tx);
        return MapToDto(tx);
    }

    // reversing the transaction restores the balance before deleting
    public async Task DeleteTransactionAsync(int id, string userId)
    {
        var tx = await _txRepo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(Transaction), id);
        var account = await _accRepo.GetByIdAsync(tx.BankAccountId)!;
        if (account!.UserId != userId) throw new UnauthorizedException();

        // reverse the balance effect: income gets subtracted, expense gets added back
        account.Balance = tx.Type == TransactionType.Income
            ? account.Balance - tx.Amount
            : account.Balance + tx.Amount;

        await _accRepo.UpdateAsync(account);
        await _txRepo.DeleteAsync(tx);
    }

    // aggregates data from multiple repositories into a single dashboard response
    public async Task<DashboardDto> GetDashboardAsync(string userId)
    {
        var accounts = await _accRepo.GetByUserAsync(userId);
        var now = DateTime.UtcNow;
        decimal totalIncome = 0, totalExpenses = 0, totalBudget = 0;
        var accountDtos = new List<BankAccountDto>();

        // calculate monthly totals across all accounts
        foreach (var acc in accounts)
        {
            var income = await _txRepo.GetMonthlyTotalAsync(acc.Id, now.Year, now.Month, TransactionType.Income);
            var expenses = await _txRepo.GetMonthlyTotalAsync(acc.Id, now.Year, now.Month, TransactionType.Expense);
            totalIncome += income;
            totalExpenses += expenses;
            totalBudget += acc.MonthlyBudget;
            accountDtos.Add(new BankAccountDto
            {
                Id = acc.Id,
                AccountNumber = acc.AccountNumber,
                AccountName = acc.AccountName,
                Balance = acc.Balance,
                MonthlyBudget = acc.MonthlyBudget,
                AccountType = acc.AccountType,
                Currency = acc.Currency,
                IsDefault = acc.IsDefault,
                MonthlyExpenses = expenses,
                MonthlyIncome = income
            });
        }

        // fetch only the 5 most recent transactions for the activity feed
        var recentFilter = new TransactionFilterDto { Page = 1, PageSize = 5 };
        if (accounts.Any()) recentFilter.BankAccountId = accounts.First().Id;
        var recentTx = await _txRepo.GetPagedAsync(recentFilter);

        // look 14 days ahead for upcoming bills — wider window than the default 7
        var upcomingBills = await _billRepo.GetUpcomingBillsAsync(14);
        var pendingBills = upcomingBills.Where(b => b.Status == BillStatus.Pending).ToList();
        var overdue = await _billRepo.GetOverdueBillsAsync();
        var recurring = await _recurRepo.GetByUserAsync(userId);

        return new DashboardDto
        {
            TotalBalance = accounts.Sum(a => a.Balance),
            MonthlyIncome = totalIncome,
            MonthlyExpenses = totalExpenses,
            MonthlySavings = totalIncome - totalExpenses,
            // returns 0 when no budget is set to avoid division by zero
            BudgetUsedPercent = totalBudget > 0 ? Math.Round(totalExpenses / totalBudget * 100, 1) : 0,
            PendingBillsCount = pendingBills.Count,
            PendingBillsAmount = pendingBills.Sum(b => b.Amount),
            OverdueBillsCount = overdue.Count(),
            ActiveRecurringPayments = recurring.Count(r => r.IsActive),
            RecentTransactions = recentTx.Items.Select(MapToDto).ToList(),
            UpcomingBills = upcomingBills.Select(MapBillToDto).ToList(),
            Accounts = accountDtos
        };
    }

    private static TransactionDto MapToDto(Transaction t) => new()
    {
        Id = t.Id,
        Description = t.Description,
        Amount = t.Amount,
        Type = t.Type,
        Category = t.Category,
        CategoryLabel = t.Category.ToString(),
        Date = t.Date,
        Notes = t.Notes,
        ReferenceNumber = t.ReferenceNumber,
        IsRecurring = t.IsRecurring,
        BalanceAfter = t.BalanceAfter,
        // Tags may be null if the transaction was loaded without Include(t => t.Tags)
        Tags = t.Tags.Select(tag => new TagDto { Id = tag.Id, Name = tag.Name, Color = tag.Color }).ToList()
    };

    private static BillDto MapBillToDto(Bill b) => new()
    {
        Id = b.Id,
        Name = b.Name,
        Provider = b.Provider,
        Amount = b.Amount,
        DueDate = b.DueDate,
        Status = b.Status,
        Category = b.Category,
        Notes = b.Notes,
        PaidAt = b.PaidAt
    };
}

public class RecurringPaymentService : IRecurringPaymentService
{
    private readonly IRecurringPaymentRepository _repo;
    private readonly IBankAccountRepository _accRepo;
    private readonly ITransactionRepository _txRepo;
    private readonly ILogger<RecurringPaymentService> _logger;

    public RecurringPaymentService(
        IRecurringPaymentRepository repo,
        IBankAccountRepository accRepo,
        ITransactionRepository txRepo,
        ILogger<RecurringPaymentService> logger)
    {
        _repo = repo;
        _accRepo = accRepo;
        _txRepo = txRepo;
        _logger = logger;
    }

    public async Task<IEnumerable<RecurringPaymentDto>> GetByUserAsync(string userId)
        => (await _repo.GetByUserAsync(userId)).Select(MapToDto);

    // returns null if the payment doesn't exist or belongs to a different user
    public async Task<RecurringPaymentDto?> GetByIdAsync(int id, string userId)
    {
        var p = await _repo.GetByIdAsync(id);
        if (p == null) return null;

        var account = await _accRepo.GetByIdAsync(p.BankAccountId);
        if (account?.UserId != userId) return null;

        return MapToDto(p);
    }

    public async Task<RecurringPaymentDto> CreateAsync(CreateRecurringPaymentDto dto, string userId)
    {
        var account = await _accRepo.GetByIdAsync(dto.BankAccountId)
            ?? throw new NotFoundException(nameof(BankAccount), dto.BankAccountId);
        if (account.UserId != userId) throw new UnauthorizedException();

        // calculate the first execution date based on frequency and start date
        var next = ComputeNextDate(dto.Frequency, dto.DayOfMonth, dto.StartDate);
        var payment = new RecurringPayment
        {
            Name = dto.Name,
            Description = dto.Description,
            Amount = dto.Amount,
            Frequency = dto.Frequency,
            Category = dto.Category,
            DayOfMonth = dto.DayOfMonth,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            PayeeName = dto.PayeeName,
            BankAccountId = dto.BankAccountId,
            IsActive = true,
            NextExecutionDate = next
        };
        await _repo.AddAsync(payment);
        return MapToDto(payment);
    }

    public async Task<RecurringPaymentDto> UpdateAsync(int id, CreateRecurringPaymentDto dto, string userId)
    {
        var p = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(RecurringPayment), id);
        var account = await _accRepo.GetByIdAsync(p.BankAccountId);
        if (account?.UserId != userId) throw new UnauthorizedException();

        p.Name = dto.Name;
        p.Description = dto.Description;
        p.Amount = dto.Amount;
        p.Frequency = dto.Frequency;
        p.Category = dto.Category;
        p.DayOfMonth = dto.DayOfMonth;
        p.EndDate = dto.EndDate;
        p.PayeeName = dto.PayeeName;
        // recalculate next execution date from now since the schedule may have changed
        p.NextExecutionDate = ComputeNextDate(dto.Frequency, dto.DayOfMonth, DateTime.UtcNow);
        await _repo.UpdateAsync(p);
        return MapToDto(p);
    }

    // flips the IsActive flag — used by the PATCH /toggle endpoint
    public async Task<RecurringPaymentDto> ToggleActiveAsync(int id, string userId)
    {
        var p = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(RecurringPayment), id);
        var account = await _accRepo.GetByIdAsync(p.BankAccountId);
        if (account?.UserId != userId) throw new UnauthorizedException();

        p.IsActive = !p.IsActive;
        await _repo.UpdateAsync(p);
        return MapToDto(p);
    }

    public async Task DeleteAsync(int id, string userId)
    {
        var p = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(RecurringPayment), id);
        var account = await _accRepo.GetByIdAsync(p.BankAccountId);
        if (account?.UserId != userId) throw new UnauthorizedException();
        await _repo.DeleteAsync(p);
    }

    // called by the background worker — processes all payments due on or before today
    public async Task ExecuteDuePaymentsAsync()
    {
        var due = await _repo.GetDuePaymentsAsync(DateTime.UtcNow);

        foreach (var payment in due)
        {
            try
            {
                var account = await _accRepo.GetByIdAsync(payment.BankAccountId);

                // skip if account was deleted or payment was deactivated since the query ran
                if (account == null || !payment.IsActive) continue;

                // skip rather than throw — log the warning and continue processing other payments
                if (account.Balance < payment.Amount)
                {
                    _logger.LogWarning("Insufficient funds for recurring payment {Id}", payment.Id);
                    continue;
                }

                // deduct the amount and record the transaction
                account.Balance -= payment.Amount;
                await _accRepo.UpdateAsync(account);

                await _txRepo.AddAsync(new Transaction
                {
                    Description = $"Automatic payment: {payment.Name}",
                    Amount = payment.Amount,
                    Type = TransactionType.Expense,
                    Category = payment.Category,
                    Date = DateTime.UtcNow,
                    BankAccountId = payment.BankAccountId,
                    IsRecurring = true,
                    RecurringPaymentId = payment.Id,
                    BalanceAfter = account.Balance,
                    // reference format: AUTO + date + payment id for traceability
                    ReferenceNumber = $"AUTO{DateTime.UtcNow:yyyyMMdd}{payment.Id}"
                });

                // advance the schedule and deactivate if past the end date
                payment.LastExecutedAt = DateTime.UtcNow;
                payment.NextExecutionDate = ComputeNextDate(payment.Frequency, payment.DayOfMonth, DateTime.UtcNow);
                if (payment.EndDate.HasValue && payment.NextExecutionDate > payment.EndDate)
                    payment.IsActive = false;

                await _repo.UpdateAsync(payment);
                _logger.LogInformation("Executed recurring payment {Name} for {Amount}", payment.Name, payment.Amount);
            }
            catch (Exception ex)
            {
                // catch per-payment so one failure doesn't abort the rest of the batch
                _logger.LogError(ex, "Error executing recurring payment {Id}", payment.Id);
            }
        }
    }

    // calculates the next execution date based on the payment frequency
    // for monthly payments, clamps dayOfMonth to the last valid day of the target month
    private static DateTime ComputeNextDate(RecurringFrequency freq, int dayOfMonth, DateTime from) => freq switch
    {
        RecurringFrequency.Daily => from.AddDays(1),
        RecurringFrequency.Weekly => from.AddDays(7),
        RecurringFrequency.Monthly => new DateTime(from.Year, from.Month, 1)
            .AddMonths(1)
            .AddDays(Math.Min(dayOfMonth, DateTime.DaysInMonth(from.AddMonths(1).Year, from.AddMonths(1).Month)) - 1),
        RecurringFrequency.Yearly => from.AddYears(1),
        _ => from.AddMonths(1)
    };

    private static RecurringPaymentDto MapToDto(RecurringPayment p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Amount = p.Amount,
        Frequency = p.Frequency,
        Category = p.Category,
        DayOfMonth = p.DayOfMonth,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        LastExecutedAt = p.LastExecutedAt,
        NextExecutionDate = p.NextExecutionDate,
        IsActive = p.IsActive,
        PayeeName = p.PayeeName,
        // BankAccount may be null if the entity was loaded without Include
        AccountName = p.BankAccount?.AccountName ?? ""
    };
}

public class BillService : IBillService
{
    private readonly IBillRepository _repo;
    private readonly IBankAccountRepository _accRepo;
    private readonly ITransactionRepository _txRepo;

    public BillService(IBillRepository repo, IBankAccountRepository accRepo, ITransactionRepository txRepo)
    {
        _repo = repo;
        _accRepo = accRepo;
        _txRepo = txRepo;
    }

    public async Task<IEnumerable<BillDto>> GetByUserAsync(string userId)
        => (await _repo.GetByUserAsync(userId)).Select(MapToDto);

    // returns null if the bill doesn't exist or belongs to a different user
    public async Task<BillDto?> GetByIdAsync(int id, string userId)
    {
        var bill = await _repo.GetByIdAsync(id);
        if (bill == null) return null;

        var account = await _accRepo.GetByIdAsync(bill.BankAccountId);
        if (account?.UserId != userId) return null;

        return MapToDto(bill);
    }

    public async Task<BillDto> CreateAsync(CreateBillDto dto, string userId)
    {
        var account = await _accRepo.GetByIdAsync(dto.BankAccountId)
            ?? throw new NotFoundException(nameof(BankAccount), dto.BankAccountId);
        if (account.UserId != userId) throw new UnauthorizedException();

        var bill = new Bill
        {
            Name = dto.Name,
            Provider = dto.Provider,
            Amount = dto.Amount,
            DueDate = dto.DueDate,
            Category = dto.Category,
            Notes = dto.Notes,
            BankAccountId = dto.BankAccountId
        };
        await _repo.AddAsync(bill);
        return MapToDto(bill);
    }

    public async Task<BillDto> UpdateAsync(int id, CreateBillDto dto, string userId)
    {
        var bill = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(Bill), id);
        var account = await _accRepo.GetByIdAsync(bill.BankAccountId);
        if (account?.UserId != userId) throw new UnauthorizedException();

        bill.Name = dto.Name;
        bill.Provider = dto.Provider;
        bill.Amount = dto.Amount;
        bill.DueDate = dto.DueDate;
        bill.Category = dto.Category;
        bill.Notes = dto.Notes;
        await _repo.UpdateAsync(bill);
        return MapToDto(bill);
    }

    // paying a bill deducts the amount from the account balance and records a transaction
    public async Task<BillDto> PayBillAsync(int id, string userId)
    {
        var bill = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(Bill), id);
        var account = await _accRepo.GetByIdAsync(bill.BankAccountId);
        if (account?.UserId != userId) throw new UnauthorizedException();
        if (account.Balance < bill.Amount) throw new InsufficientFundsException(account.Balance, bill.Amount);

        account.Balance -= bill.Amount;
        await _accRepo.UpdateAsync(account);

        // record the payment as a utilities expense so it appears in the transaction history
        await _txRepo.AddAsync(new Transaction
        {
            Description = $"Bill: {bill.Name}",
            Amount = bill.Amount,
            Type = TransactionType.Expense,
            Category = TransactionCategory.Utilities,
            Date = DateTime.UtcNow,
            BankAccountId = bill.BankAccountId,
            BalanceAfter = account.Balance
        });

        // mark the bill as paid with a timestamp
        bill.Status = BillStatus.Paid;
        bill.PaidAt = DateTime.UtcNow;
        await _repo.UpdateAsync(bill);
        return MapToDto(bill);
    }

    public async Task DeleteAsync(int id, string userId)
    {
        var bill = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(Bill), id);
        var account = await _accRepo.GetByIdAsync(bill.BankAccountId);
        if (account?.UserId != userId) throw new UnauthorizedException();
        await _repo.DeleteAsync(bill);
    }

    private static BillDto MapToDto(Bill b) => new()
    {
        Id = b.Id,
        Name = b.Name,
        Provider = b.Provider,
        Amount = b.Amount,
        DueDate = b.DueDate,
        Status = b.Status,
        Category = b.Category,
        Notes = b.Notes,
        PaidAt = b.PaidAt,
        // BankAccount may be null if the entity was loaded without Include
        AccountName = b.BankAccount?.AccountName ?? ""
    };
}

public class TagService : ITagService
{
    private readonly ITagRepository _repo;
    private readonly ITransactionRepository _txRepo;
    private readonly IBankAccountRepository _accRepo;
    private readonly AppDbContext _context;

    public TagService(
        ITagRepository repo,
        ITransactionRepository txRepo,
        IBankAccountRepository accRepo,
        AppDbContext context)
    {
        _repo = repo;
        _txRepo = txRepo;
        _accRepo = accRepo;
        _context = context;
    }

    // tags are global — no userId filter, all users see the same tag list
    public async Task<IEnumerable<TagDto>> GetAllAsync()
        => (await _repo.GetAllWithCountAsync()).Select(MapToDto);

    public async Task<TagDto?> GetByIdAsync(int id)
    {
        var tag = await _repo.GetByIdAsync(id);
        return tag == null ? null : MapToDto(tag);
    }

    public async Task<TagDto> CreateAsync(CreateTagDto dto)
    {
        var tag = new Tag { Name = dto.Name, Color = dto.Color };
        await _repo.AddAsync(tag);
        return MapToDto(tag);
    }

    public async Task<TagDto> UpdateAsync(int id, CreateTagDto dto)
    {
        var tag = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(Tag), id);
        tag.Name = dto.Name;
        tag.Color = dto.Color;
        await _repo.UpdateAsync(tag);
        return MapToDto(tag);
    }

    // only admins can delete tags — enforced at the controller level via [Authorize(Roles = "Admin")]
    public async Task DeleteAsync(int id)
    {
        var tag = await _repo.GetByIdAsync(id) ?? throw new NotFoundException(nameof(Tag), id);
        await _repo.DeleteAsync(tag);
    }

    // verifies the transaction belongs to the current user before linking the tag
    public async Task AddToTransactionAsync(int tagId, int transactionId, string userId)
    {
        var tag = await _repo.GetByIdAsync(tagId) ?? throw new NotFoundException(nameof(Tag), tagId);
        var tx = await _txRepo.GetByIdWithTagsAsync(transactionId) ?? throw new NotFoundException(nameof(Transaction), transactionId);
        var account = await _accRepo.GetByIdAsync(tx.BankAccountId);
        if (account?.UserId != userId) throw new UnauthorizedException();

        // skip silently if the tag is already linked — idempotent behavior
        if (!tx.Tags.Any(t => t.Id == tagId))
        {
            tx.Tags.Add(tag);
            await _context.SaveChangesAsync();
        }
    }

    // verifies the transaction belongs to the current user before removing the tag
    public async Task RemoveFromTransactionAsync(int tagId, int transactionId, string userId)
    {
        var tx = await _txRepo.GetByIdWithTagsAsync(transactionId) ?? throw new NotFoundException(nameof(Transaction), transactionId);
        var account = await _accRepo.GetByIdAsync(tx.BankAccountId);
        if (account?.UserId != userId) throw new UnauthorizedException();

        var tag = tx.Tags.FirstOrDefault(t => t.Id == tagId);
        // skip silently if the tag isn't linked — idempotent behavior
        if (tag != null)
        {
            tx.Tags.Remove(tag);
            await _context.SaveChangesAsync();
        }
    }

    private static TagDto MapToDto(Tag t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Color = t.Color,
        // Transactions may be null if the tag was loaded without Include
        TransactionCount = t.Transactions?.Count ?? 0
    };
}