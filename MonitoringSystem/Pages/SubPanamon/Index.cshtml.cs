using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MonitoringSystem.Pages.SubPanamon
{
    // /subpanamon langsung ke PWK Actual
    public class IndexModel : PageModel
    {
        public IActionResult OnGet() => RedirectToPage("/SubPanamon/PWKActual/Index");
    }
}
