using BankingApp.Core.DTOs;
using BankingApp.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
        => Ok(await _auth.RegisterAsync(dto));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
        => Ok(await _auth.LoginAsync(dto));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IBankAccountService _service;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public AccountsController(IBankAccountService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BankAccountDto>>> GetAll()
        => Ok(await _service.GetUserAccountsAsync(UserId));

    [HttpGet("{id}")]
    public async Task<ActionResult<BankAccountDto>> GetById(int id)
    {
        var acc = await _service.GetAccountByIdAsync(id, UserId);
        return acc == null ? NotFound() : Ok(acc);
    }

    [HttpPost]
    public async Task<ActionResult<BankAccountDto>> Create([FromBody] CreateBankAccountDto dto)
        => Ok(await _service.CreateAccountAsync(dto, UserId));

    [HttpPatch("{id}/budget")]
    public async Task<ActionResult<BankAccountDto>> UpdateBudget(int id, [FromBody] decimal budget)
        => Ok(await _service.UpdateBudgetAsync(id, budget, UserId));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAccountAsync(id, UserId);
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public TransactionsController(ITransactionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PagedResult<TransactionDto>>> GetAll([FromQuery] TransactionFilterDto filter)
        => Ok(await _service.GetTransactionsAsync(filter, UserId));

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create([FromBody] CreateTransactionDto dto)
        => Ok(await _service.CreateTransactionAsync(dto, UserId));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteTransactionAsync(id, UserId);
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboard()
        => Ok(await _service.GetDashboardAsync(UserId));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecurringController : ControllerBase
{
    private readonly IRecurringPaymentService _service;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public RecurringController(IRecurringPaymentService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecurringPaymentDto>>> GetAll()
        => Ok(await _service.GetByUserAsync(UserId));

    [HttpPost]
    public async Task<ActionResult<RecurringPaymentDto>> Create([FromBody] CreateRecurringPaymentDto dto)
        => Ok(await _service.CreateAsync(dto, UserId));

    [HttpPatch("{id}/toggle")]
    public async Task<ActionResult<RecurringPaymentDto>> Toggle(int id)
        => Ok(await _service.ToggleActiveAsync(id, UserId));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id, UserId);
        return NoContent();
    }

    [HttpPost("execute-now")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExecuteNow()
    {
        await _service.ExecuteDuePaymentsAsync();
        return Ok(new { message = "Plăți recurente executate." });
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillsController : ControllerBase
{
    private readonly IBillService _service;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public BillsController(IBillService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BillDto>>> GetAll()
        => Ok(await _service.GetByUserAsync(UserId));

    [HttpPost]
    public async Task<ActionResult<BillDto>> Create([FromBody] CreateBillDto dto)
        => Ok(await _service.CreateAsync(dto, UserId));

    [HttpPost("{id}/pay")]
    public async Task<ActionResult<BillDto>> Pay(int id)
        => Ok(await _service.PayBillAsync(id, UserId));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id, UserId);
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _service;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public ReportsController(IReportService service) => _service = service;

    [HttpGet("{accountId}/{year}/{month}")]
    public async Task<ActionResult<MonthlyReportDto>> GetMonthlyReport(int accountId, int year, int month)
        => Ok(await _service.GetMonthlyReportAsync(accountId, year, month, UserId));

    [HttpPost("generate/{year}/{month}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Generate(int year, int month)
    {
        await _service.GenerateMonthlyReportsAsync(year, month);
        return Ok(new { message = "Rapoarte generate." });
    }
}
