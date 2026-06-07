using BankingApp.Core.DTOs;
using BankingApp.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankingApp.API.Controllers;

// public endpoints, no authorization required
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

// all endpoints require authentication
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IBankAccountService _service;

    // helper property that extracts the user id from the jwt token on every request
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public AccountsController(IBankAccountService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BankAccountDto>>> GetAll()
        => Ok(await _service.GetUserAccountsAsync(UserId));

    [HttpGet("{id}")]
    public async Task<ActionResult<BankAccountDto>> GetById(int id)
    {
        // service returns null if the account doesn't exist or belongs to another user
        var acc = await _service.GetAccountByIdAsync(id, UserId);
        return acc == null ? NotFound() : Ok(acc);
    }

    [HttpPost]
    public async Task<ActionResult<BankAccountDto>> Create([FromBody] CreateBankAccountDto dto)
        => Ok(await _service.CreateAccountAsync(dto, UserId));

    // patch instead of put because we're only updating one field
    [HttpPatch("{id}/budget")]
    public async Task<ActionResult<BankAccountDto>> UpdateBudget(int id, [FromBody] decimal budget)
        => Ok(await _service.UpdateBudgetAsync(id, budget, UserId));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAccountAsync(id, UserId);
        return NoContent(); // 204 - success with no body
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;

    // helper property that extracts the user id from the jwt token on every request
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public TransactionsController(ITransactionService service) => _service = service;

    // fromquery means filters come from the url e.g. /api/transactions?page=1&type=expense
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
        return NoContent(); // 204 - success with no body
    }

    // dashboard endpoint aggregates data from multiple sources into one response
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

    // helper property that extracts the user id from the jwt token on every request
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public RecurringController(IRecurringPaymentService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecurringPaymentDto>>> GetAll()
        => Ok(await _service.GetByUserAsync(UserId));

    [HttpPost]
    public async Task<ActionResult<RecurringPaymentDto>> Create([FromBody] CreateRecurringPaymentDto dto)
        => Ok(await _service.CreateAsync(dto, UserId));

    // patch used to toggle a single boolean field (isactive) without sending the full object
    [HttpPatch("{id}/toggle")]
    public async Task<ActionResult<RecurringPaymentDto>> Toggle(int id)
        => Ok(await _service.ToggleActiveAsync(id, UserId));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id, UserId);
        return NoContent(); // 204 - success with no body
    }

    // admin only endpoint to manually trigger payments that are normally run by the background worker
    [HttpPost("execute-now")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExecuteNow()
    {
        await _service.ExecuteDuePaymentsAsync();
        return Ok(new { message = "Recurring payments executed." });
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillsController : ControllerBase
{
    private readonly IBillService _service;

    // helper property that extracts the user id from the jwt token on every request
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public BillsController(IBillService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BillDto>>> GetAll()
        => Ok(await _service.GetByUserAsync(UserId));

    [HttpPost]
    public async Task<ActionResult<BillDto>> Create([FromBody] CreateBillDto dto)
        => Ok(await _service.CreateAsync(dto, UserId));

    // paying a bill deducts the amount from the account balance and marks it as paid
    [HttpPost("{id}/pay")]
    public async Task<ActionResult<BillDto>> Pay(int id)
        => Ok(await _service.PayBillAsync(id, UserId));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id, UserId);
        return NoContent(); // 204 - success with no body
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _service;

    // helper property that extracts the user id from the jwt token on every request
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public ReportsController(IReportService service) => _service = service;

    // year and month come from the url e.g. /api/reports/1/2026/5
    [HttpGet("{accountId}/{year}/{month}")]
    public async Task<ActionResult<MonthlyReportDto>> GetMonthlyReport(int accountId, int year, int month)
        => Ok(await _service.GetMonthlyReportAsync(accountId, year, month, UserId));

    // admin only endpoint to manually generate reports, normally done by the background worker on day 1 of each month
    [HttpPost("generate/{year}/{month}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Generate(int year, int month)
    {
        await _service.GenerateMonthlyReportsAsync(year, month);
        return Ok(new { message = "Reports generated." });
    }
}

// tags are shared across all users but only admins can delete them
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly ITagService _service;

    // helper property that extracts the user id from the jwt token on every request
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public TagsController(ITagService service) => _service = service;

    // returns all tags regardless of user — tags are global, not user-scoped
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TagDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<TagDto>> GetById(int id)
    {
        var tag = await _service.GetByIdAsync(id);
        return tag == null ? NotFound() : Ok(tag);
    }

    // returns 201 Created with a location header pointing to the new tag
    [HttpPost]
    public async Task<ActionResult<TagDto>> Create([FromBody] CreateTagDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // put used here because we're replacing the full tag object
    [HttpPut("{id}")]
    public async Task<ActionResult<TagDto>> Update(int id, [FromBody] CreateTagDto dto)
        => Ok(await _service.UpdateAsync(id, dto));

    // only admins can delete tags since they are shared across all users
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent(); // 204 - success with no body
    }

    // links an existing tag to a transaction owned by the current user
    [HttpPost("{tagId}/transactions/{transactionId}")]
    public async Task<IActionResult> AddToTransaction(int tagId, int transactionId)
    {
        await _service.AddToTransactionAsync(tagId, transactionId, UserId);
        return NoContent(); // 204 - success with no body
    }

    // removes the link between a tag and a transaction owned by the current user
    [HttpDelete("{tagId}/transactions/{transactionId}")]
    public async Task<IActionResult> RemoveFromTransaction(int tagId, int transactionId)
    {
        await _service.RemoveFromTransactionAsync(tagId, transactionId, UserId);
        return NoContent(); // 204 - success with no body
    }
}