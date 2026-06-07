using BankingApp.Core.DTOs;
using BankingApp.Core.Entities;
using BankingApp.Core.Enums;
using BankingApp.Core.Exceptions;
using BankingApp.Core.Interfaces;
using BankingApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace BankingApp.Infrastructure.Services;

// handles monthly report generation with redis caching
// if redis is available: check cache first, calculate on miss, save to cache for 1 hour
// if redis is unavailable: calculate directly from the database (graceful fallback)
public class ReportService : IReportService
{
    private readonly ITransactionRepository _txRepo;
    private readonly IBankAccountRepository _accRepo;
    private readonly IMonthlyReportRepository _reportRepo;
    private readonly IConnectionMultiplexer? _redis; // nullable — redis is optional
    private readonly ILogger<ReportService> _logger;

    // one color per category for the breakdown chart, assigned by index
    private static readonly string[] CategoryColors = { "#e91e8c", "#7c3aed", "#06b6d4", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899", "#14b8a6", "#f97316", "#64748b", "#6366f1" };

    // redis is injected as optional (default null) so the app works without it
    public ReportService(ITransactionRepository txRepo, IBankAccountRepository accRepo, IMonthlyReportRepository reportRepo, ILogger<ReportService> logger, IConnectionMultiplexer? redis = null)
    {
        _txRepo = txRepo; _accRepo = accRepo; _reportRepo = reportRepo; _logger = logger; _redis = redis;
    }

    public async Task<MonthlyReportDto> GetMonthlyReportAsync(int accountId, int year, int month, string userId)
    {
        var account = await _accRepo.GetByIdAsync(accountId) ?? throw new NotFoundException(nameof(BankAccount), accountId);

        // make sure the user can only access their own accounts
        if (account.UserId != userId) throw new UnauthorizedException();

        // cache key format: report:{accountId}:{year}:{month} e.g. "report:1:2026:5"
        var cacheKey = $"report:{accountId}:{year}:{month}";

        // try to serve from redis cache first
        if (_redis != null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var cached = await db.StringGetAsync(cacheKey);
                if (cached.HasValue)
                {
                    _logger.LogInformation("Report cache HIT for key {Key}", cacheKey);
                    var dto = JsonSerializer.Deserialize<MonthlyReportDto>(cached!);
                    if (dto != null)
                    {
                        // set fromcache=true so the ui can show the green "from redis" banner
                        dto.FromCache = true;
                        return dto;
                    }
                }
                _logger.LogInformation("Report cache MISS for key {Key}", cacheKey);
            }
            catch (Exception ex)
            {
                // redis is down — log a warning and fall through to calculate from db
                _logger.LogWarning(ex, "Redis unavailable, falling back to DB");
            }
        }

        // cache miss or redis unavailable — calculate from transactions
        var report = await CalculateReportAsync(accountId, year, month, account.MonthlyBudget);
        report.FromCache = false;

        // save the calculated report to redis with a 1 hour expiry
        if (_redis != null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(report);
                await db.StringSetAsync(cacheKey, json, TimeSpan.FromHours(1));
                _logger.LogInformation("Report cached in Redis for key {Key}", cacheKey);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to cache report in Redis"); }
        }

        // also persist to the database if it doesn't already exist
        var existing = await _reportRepo.GetByAccountAndMonthAsync(accountId, year, month);
        if (existing == null)
        {
            await _reportRepo.AddAsync(new MonthlyReport
            {
                BankAccountId = accountId,
                Year = year,
                Month = month,
                TotalIncome = report.TotalIncome,
                TotalExpenses = report.TotalExpenses,
                NetSavings = report.NetSavings,
                BudgetSet = report.BudgetSet,
                BudgetUsedPercent = report.BudgetUsedPercent,
                CategoryBreakdownJson = JsonSerializer.Serialize(report.CategoryBreakdown), // stored as json in db
                GeneratedAt = DateTime.UtcNow
            });
        }

        return report;
    }

    // called by the background worker on day 1 of each month for all accounts
    public async Task GenerateMonthlyReportsAsync(int year, int month)
    {
        _logger.LogInformation("Generating monthly reports for {Year}-{Month}", year, month);
        var accounts = await _accRepo.GetAllAsync();

        foreach (var account in accounts)
        {
            try
            {
                // skip if a report already exists for this account and month
                var existing = await _reportRepo.GetByAccountAndMonthAsync(account.Id, year, month);
                if (existing != null) continue;

                var report = await CalculateReportAsync(account.Id, year, month, account.MonthlyBudget);
                await _reportRepo.AddAsync(new MonthlyReport
                {
                    BankAccountId = account.Id,
                    Year = year,
                    Month = month,
                    TotalIncome = report.TotalIncome,
                    TotalExpenses = report.TotalExpenses,
                    NetSavings = report.NetSavings,
                    BudgetSet = report.BudgetSet,
                    BudgetUsedPercent = report.BudgetUsedPercent,
                    CategoryBreakdownJson = JsonSerializer.Serialize(report.CategoryBreakdown),
                    GeneratedAt = DateTime.UtcNow
                });

                // invalidate the redis cache so the next request recalculates from the fresh db data
                if (_redis != null)
                {
                    try { var db = _redis.GetDatabase(); await db.KeyDeleteAsync($"report:{account.Id}:{year}:{month}"); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                // log and continue — one failed account shouldn't stop the others
                _logger.LogError(ex, "Error generating report for account {Id}", account.Id);
            }
        }
    }

    // calculates a monthly report from raw transactions — used on cache miss and by the worker
    private async Task<MonthlyReportDto> CalculateReportAsync(int accountId, int year, int month, decimal budget)
    {
        var transactions = await _txRepo.GetByAccountAndMonthAsync(accountId, year, month);
        var txList = transactions.ToList();

        var income = txList.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expenses = txList.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

        // group expenses by category and calculate each category's percentage of total expenses
        var categoryBreakdown = txList
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Category)
            .Select((g, i) => new CategoryExpenseDto
            {
                Category = g.Key.ToString(),
                Amount = g.Sum(t => t.Amount),
                Percentage = expenses > 0 ? Math.Round(g.Sum(t => t.Amount) / expenses * 100, 1) : 0,
                Color = CategoryColors[i % CategoryColors.Length] // cycle through colors if more categories than colors
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        // group expenses by day for the bar chart on the reports page
        var dailyBreakdown = txList
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Date.Day)
            .Select(g => new DailyExpenseDto { Day = g.Key, Amount = g.Sum(t => t.Amount) })
            .OrderBy(d => d.Day)
            .ToList();

        // romanian month names indexed from 1 — index 0 is empty to match month numbers directly
        var monthNames = new[] { "", "Ianuarie", "Februarie", "Martie", "Aprilie", "Mai", "Iunie", "Iulie", "August", "Septembrie", "Octombrie", "Noiembrie", "Decembrie" };

        return new MonthlyReportDto
        {
            Year = year,
            Month = month,
            MonthName = monthNames[month],
            TotalIncome = income,
            TotalExpenses = expenses,
            NetSavings = income - expenses,
            BudgetSet = budget,
            BudgetUsedPercent = budget > 0 ? Math.Round(expenses / budget * 100, 1) : 0,
            GeneratedAt = DateTime.UtcNow,
            CategoryBreakdown = categoryBreakdown,
            DailyBreakdown = dailyBreakdown
        };
    }
}