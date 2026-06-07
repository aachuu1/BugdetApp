using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BankingApp.API.Pages.Tags;

// page model for the Tags razor page — no server-side logic needed
// since the page is a shell for the react SPA mounted in Index.cshtml
public class IndexModel : PageModel
{
    // OnGet is empty because all data fetching is handled client-side via the /api/tags endpoint
    public void OnGet() { }
}