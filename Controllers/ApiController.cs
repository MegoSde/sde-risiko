using AlpacaMedic.Models;
using AlpacaMedic.Services;
using Microsoft.AspNetCore.Mvc;

namespace AlpacaMedic.Controllers;
[ApiController, Route("api"), ServiceFilter(typeof(AnalysisExceptionFilter))]
public class ApiController(AnalysisService analysis, RiskService risks, CisPriorityService priorities) : ControllerBase
{
    [HttpGet("business-processes")]
    public async Task<object> Processes() => await analysis.Processes();
    [HttpGet("business-assets")]
    public async Task<object> Assets() => await analysis.Assets();
    [HttpGet("system-assets")]
    public async Task<object> Systems() => await risks.Systems();
    [HttpGet("asset-mappings")]
    public async Task<object> Mappings() => await analysis.Mappings();
    [HttpGet("system-access")]
    public async Task<object> Access() => await analysis.Access();
    [HttpGet("system-threats")]
    public async Task<object> Threats() => await analysis.Threats();
    [HttpGet("attack-techniques")]
    public async Task<object> Techniques() => await analysis.Techniques();
    [HttpGet("cis-controls")]
    public async Task<object> Controls() => await analysis.Controls();
    [HttpGet("technique-controls")]
    public async Task<object> Recommended() => await analysis.Recommended();
    [HttpGet("threat-controls")]
    public async Task<object> Selected() => await analysis.Selected();
    [HttpGet("risk-levels")]
    public object RiskLevels() => risks.Thresholds;
    [HttpGet("risks")]
    public async Task<object> Risks() => await risks.Risks();
    [HttpGet("cis-priorities")]
    public async Task<object> Calculate() => await priorities.Calculate();
    [HttpGet("dashboard")]
    public async Task<object> Dashboard() => await analysis.Dashboard();
    [HttpGet("risks/{id:int}")]
    public async Task<IActionResult> Risk(int id)
    {
        var row = (await risks.Risks()).FirstOrDefault(x => x.Id == id);
        return row == null ? NotFound() : Ok(row);
    }

    [HttpPost("business-processes")]
    public async Task<IActionResult> CreateProcess(BusinessProcess input)
    {
        var row = await analysis.SaveProcess(null, input);
        return Created($"/api/business-processes/{row.Id}", row);
    }

    [HttpPut("business-processes/{id:int}")]
    public async Task<IActionResult> UpdateProcess(int id, BusinessProcess input) => Ok(await analysis.SaveProcess(id, input));
    [HttpPost("business-assets")]
    public async Task<IActionResult> CreateAsset(BusinessAsset input)
    {
        var row = await analysis.SaveAsset(null, input);
        return Created($"/api/business-assets/{row.Id}", row);
    }

    [HttpPut("business-assets/{id:int}")]
    public async Task<IActionResult> UpdateAsset(int id, BusinessAsset input) => Ok(await analysis.SaveAsset(id, input));
    [HttpPost("system-assets")]
    public async Task<IActionResult> CreateSystem(SystemAsset input)
    {
        var row = await analysis.SaveSystem(null, input);
        return Created($"/api/system-assets/{row.Id}", row);
    }

    [HttpPut("system-assets/{id:int}")]
    public async Task<IActionResult> UpdateSystem(int id, SystemAsset input) => Ok(await analysis.SaveSystem(id, input));
    [HttpPost("system-threats")]
    public async Task<IActionResult> CreateThreat(SystemThreat input)
    {
        var row = await analysis.SaveThreat(null, input);
        return Created($"/api/system-threats/{row.Id}", row);
    }

    [HttpPut("system-threats/{id:int}")]
    public async Task<IActionResult> UpdateThreat(int id, SystemThreat input) => Ok(await analysis.SaveThreat(id, input));
    [HttpPost("asset-mappings")]
    public async Task<IActionResult> AddMapping(AnalysisService.MappingInput input)
    {
        var result = await analysis.AddMapping(input);
        return result.Created ? Created($"/api/asset-mappings/{result.Value.Id}", result.Value) : Ok(result.Value);
    }

    [HttpPost("system-access")]
    public async Task<IActionResult> AddAccess(SystemAssetAccess input)
    {
        var result = await analysis.AddAccess(input);
        return result.Created ? Created($"/api/system-access/{result.Value.Id}", result.Value) : Ok(result.Value);
    }

    [HttpPut("threat-controls/{threatId:int}/{controlId:int}")]
    public async Task<IActionResult> SetControl(int threatId, int controlId, ThreatCisControl input) => Ok(await analysis.SetControl(threatId, controlId, input));
    [HttpDelete("{resource}/{id:int}")]
    public async Task<IActionResult> Delete(string resource, int id)
    {
        await analysis.Delete(resource, id);
        return NoContent();
    }
}
