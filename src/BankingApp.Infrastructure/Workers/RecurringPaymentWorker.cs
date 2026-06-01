using BankingApp.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankingApp.Infrastructure.Workers;

public class RecurringPaymentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringPaymentWorker> _logger;
    private readonly TimeOnly _runAt = new(8, 0);

    public RecurringPaymentWorker(IServiceScopeFactory scopeFactory, ILogger<RecurringPaymentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecurringPaymentWorker pornit. Va rula zilnic la {RunAt}", _runAt);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = DateTime.Today.Add(_runAt.ToTimeSpan());

            if (now > nextRun)
                nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;
            _logger.LogInformation("Worker va rula la {NextRun} (peste {Delay})", nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            await RunDailyJobsAsync();
        }

        _logger.LogInformation("RecurringPaymentWorker oprit.");
    }

    private async Task RunDailyJobsAsync()
    {
        _logger.LogInformation("=== Rulare job-uri zilnice: {Date} ===", DateTime.Now);

        using var scope = _scopeFactory.CreateScope();

        try
        {
            var recurringService = scope.ServiceProvider.GetRequiredService<IRecurringPaymentService>();
            await recurringService.ExecuteDuePaymentsAsync();
            _logger.LogInformation("Plăți recurente executate cu succes.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eroare la executarea plăților recurente.");
        }

        if (DateTime.Today.Day == 1)
        {
            try
            {
                var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
                var lastMonth = DateTime.Today.AddMonths(-1);
                await reportService.GenerateMonthlyReportsAsync(lastMonth.Year, lastMonth.Month);
                _logger.LogInformation("Rapoarte lunare generate pentru {Year}-{Month}", lastMonth.Year, lastMonth.Month);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la generarea rapoartelor lunare.");
            }
        }

        _logger.LogInformation("=== Job-uri zilnice finalizate ===");
    }
}
