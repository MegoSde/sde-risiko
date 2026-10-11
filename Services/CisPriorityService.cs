using AlpacaMedic.Data;
using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Services;
public class CisPriorityService(RiskDb db, RiskService risks)
{
    public async Task<object> Calculate()
    {
        var rows = await risks.Risks();
        var controls = await db.CisControls.AsNoTracking().ToListAsync();
        var data = controls.Select(c =>
        {
            var relevant = rows.Where(r => r.CisControls.Any(x => x.Id == c.Id)).ToArray();
            return new
            {
                Control = c,
                Findings = relevant.Length,
                WeightedRiskScore = relevant.Sum(r => r.RiskScore),
                AverageRiskScore = relevant.Length == 0 ? 0 : relevant.Average(r => r.RiskScore)
            };
        }).ToArray();
        var total = data.Sum(x => x.WeightedRiskScore);
        return data.OrderByDescending(x => x.WeightedRiskScore).Select(x => new { x.Control, x.Findings, x.WeightedRiskScore, x.AverageRiskScore, Share = total == 0 ? 0 : x.WeightedRiskScore / total });
    }
}
