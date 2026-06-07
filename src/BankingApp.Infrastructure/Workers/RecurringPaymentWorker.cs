using BankingApp.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankingApp.Infrastructure.Workers;

// background service that runs automatically without any http request triggering it
// registered in program.cs with AddHostedService<RecurringPaymentWorker>()
public class RecurringPaymentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringPaymentWorker> _logger;
    // daily execution time — can be moved to appsettings.json if it needs to be configurable
    private readonly TimeOnly _runAt = new(8, 0);

    // IServiceScopeFactory is used instead of injecting scoped services directly
    // because BackgroundService is a singleton — injecting scoped services into it
    // would cause a lifetime mismatch error at startup
    public RecurringPaymentWorker(IServiceScopeFactory scopeFactory, ILogger<RecurringPaymentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecurringPaymentWorker started. Will run daily at {RunAt}", _runAt);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;

            // calculate the next 08:00 run time
            var nextRun = DateTime.Today.Add(_runAt.ToTimeSpan());

            // if 08:00 has already passed today, push the next run to tomorrow
            if (now > nextRun)
                nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;
            _logger.LogInformation("Worker will run at {NextRun} (in {Delay})", nextRun, delay);

            try
            {
                // sleep until the scheduled time — cancelled immediately if the app shuts down
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // app is shutting down — exit the loop cleanly without logging an error
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            await RunDailyJobsAsync();
        }

        _logger.LogInformation("RecurringPaymentWorker stopped.");
    }

    private async Task RunDailyJobsAsync()
    {
        _logger.LogInformation("=== Running daily jobs: {Date} ===", DateTime.Now);

        // create a fresh scope for this execution so scoped services (DbContext, repositories)
        // are properly instantiated and disposed after the jobs complete
        using var scope = _scopeFactory.CreateScope();

        // job 1: process all recurring payments due today — runs every day
        try
        {
            var recurringService = scope.ServiceProvider.GetRequiredService<IRecurringPaymentService>();
            await recurringService.ExecuteDuePaymentsAsync();
            _logger.LogInformation("Recurring payments executed successfully.");
        }
        catch (Exception ex)
        {
            // catch per-job so a failure here doesn't prevent job 2 from running
            _logger.LogError(ex, "Error executing recurring payments.");
        }

        // job 2: generate monthly reports for last month — only runs on the 1st of each month
        if (DateTime.Today.Day == 1)
        {
            try
            {
                var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
                var lastMonth = DateTime.Today.AddMonths(-1);
                await reportService.GenerateMonthlyReportsAsync(lastMonth.Year, lastMonth.Month);
                _logger.LogInformation("Monthly reports generated for {Year}-{Month}", lastMonth.Year, lastMonth.Month);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly reports.");
            }
        }

        _logger.LogInformation("=== Daily jobs completed ===");
    }
}