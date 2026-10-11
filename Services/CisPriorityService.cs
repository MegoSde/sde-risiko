using AlpacaMedic.Data;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Services;
public class CisPriorityService(RiskDb db)
{
    public async Task<object> Calculate()
    {
        var controls = await db.CisControls.AsNoTracking().ToListAsync();
        var selected = await db.ComponentControls.AsNoTracking().Where(c => c.IsRecommended).ToListAsync();
        return controls.Select(c => new { Control = c, Findings = selected.Count(s => s.CisControlId == c.Id) }).OrderByDescending(c => c.Findings);
    }
}
