using Microsoft.AspNetCore.Mvc;

namespace AlpacaMedic.Controllers;
public class HomeController : Controller
{
    public static readonly (string Slug, string Title)[] Pages = [("dashboard", "Dashboard"), ("business-analysis", "Forretningsanalyse"), ("asset-mapping", "Asset Mapping"), ("system-analysis", "Systemanalyse"), ("threat-analysis", "Threat Analysis"), ("controls", "Controls"), ("risk-analysis", "Risk Analysis"), ("risk-matrix", "Risk Matrix"), ("cis-priorities", "CIS Priorities"), ("report", "Report")];
    [HttpGet("/"), HttpGet("/{page}")]
    public IActionResult Index(string page = "dashboard")
    {
        var selected = Pages.FirstOrDefault(x => x.Slug == page);
        if (selected == default)
            return NotFound();
        ViewData["Page"] = selected.Slug;
        ViewData["Title"] = selected.Title;
        return View(selected.Slug);
    }

    [Route("/error")]
    public IActionResult Error() => Problem("Handlingen kunne ikke gennemføres. Prøv igen.");
}
