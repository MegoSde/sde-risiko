using AlpacaMedic.Models;
using AlpacaMedic.Services;
using Microsoft.AspNetCore.Mvc;

namespace AlpacaMedic.Controllers;
[ApiController, Route("api"), ServiceFilter(typeof(AnalysisExceptionFilter))]
public class ApiController(AnalysisService analysis, AttackAnalysisService attacks, RiskAnalysisService risks, CisPriorityService priorities) : ControllerBase
{
    [HttpGet("business-processes")]
    public async Task<object> Processes() => await analysis.Processes();
    [HttpGet("business-assets")]
    public async Task<object> Assets() => await analysis.Assets();
    [HttpGet("system-assets")]
    public async Task<object> Systems() => await analysis.Systems();
    [HttpGet("business-asset-relations")]
    public async Task<object> BusinessRelations([FromQuery] int? businessAssetId) => await analysis.BusinessRelations(businessAssetId);
    [HttpGet("system-asset-relations")]
    public async Task<object> SystemRelations([FromQuery] int? sourceSystemAssetId) => await analysis.SystemRelations(sourceSystemAssetId);
    [HttpGet("attack-surface")]
    public async Task<object> AttackSurface([FromQuery] int? systemAssetId) => await analysis.AttackSurface(systemAssetId);
    [HttpPost("attack-surface")]
    public async Task<IActionResult> CreateAttackSurface(AttackSurfaceEntry input)
    {
        var result = await analysis.SaveAttackSurface(null, input);
        return result.Created ? Created($"/api/attack-surface/{result.Value.Id}", result.Value) : Ok(result.Value);
    }
    [HttpPut("attack-surface/{id:int}")]
    public async Task<IActionResult> UpdateAttackSurface(int id, AttackSurfaceEntry input) => Ok((await analysis.SaveAttackSurface(id, input)).Value);

    [HttpGet("attack-components")]
    public async Task<object> Components() => await attacks.Components();
    [HttpPost("attack-components")]
    public async Task<IActionResult> StartComponent(AttackComponent input)
    {
        var result = await attacks.Start(input);
        return result.ComponentCreated ? Created($"/api/attack-components/{result.Component.Id}", result) : Ok(result);
    }
    [HttpPut("attack-components/{id:int}")]
    public async Task<object> UpdateComponent(int id, AttackAnalysisService.AssessmentInput input) => await attacks.Update(id, input);
    [HttpGet("attack-components/{id:int}/next-assets")]
    public async Task<object> NextAssets(int id) => await attacks.NextAssets(id);
    [HttpPost("attack-components/{id:int}/next")]
    public async Task<IActionResult> NextComponent(int id, AttackAnalysisService.NextInput input)
    {
        var result = await attacks.Next(id, input);
        return result.ComponentCreated || result.ConnectionCreated ? Created($"/api/attack-components/{result.Component.Id}", result) : Ok(result);
    }
    [HttpGet("attack-connections")]
    public async Task<object> Connections([FromQuery] int? fromAttackComponentId, [FromQuery] int? toAttackComponentId) => await attacks.Connections(fromAttackComponentId, toAttackComponentId);
    [HttpPost("attack-connections")]
    public async Task<IActionResult> Connect(AttackConnection input)
    {
        var result = await attacks.AddConnection(input);
        return result.Created ? Created($"/api/attack-connections/{result.Connection.Id}", result.Connection) : Ok(result.Connection);
    }

    [HttpGet("attack-techniques")]
    public async Task<object> Techniques([FromQuery] string? search) => await analysis.Techniques(search);
    [HttpGet("attack-techniques/{id:int}")]
    public async Task<object> Technique(int id) => await analysis.Technique(id);
    [HttpGet("cis-controls")]
    public async Task<object> Controls() => await analysis.Controls();
    [HttpGet("technique-controls")]
    public async Task<object> Recommended() => await analysis.Recommended();
    [HttpGet("component-controls")]
    public async Task<object> Selected() => await analysis.Selected();
    [HttpGet("risks")]
    public async Task<object> Risks(CancellationToken cancellationToken) => await risks.Calculate(cancellationToken);
    [HttpGet("risk-levels")]
    public object RiskLevels() => risks.Thresholds;
    [HttpGet("cis-priorities")]
    public async Task<object> Calculate() => await priorities.Calculate();
    [HttpGet("dashboard")]
    public async Task<object> Dashboard() => await analysis.Dashboard();
    [HttpGet("risks/{id:int}")]
    public async Task<IActionResult> Risk(int id, CancellationToken cancellationToken)
    {
        var row = (await risks.Calculate(cancellationToken)).FirstOrDefault(r => r.Id == id);
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
    public async Task<IActionResult> CreateAsset(AnalysisService.AssetInput input)
    {
        var row = await analysis.SaveAsset(null, input);
        return Created($"/api/business-assets/{row.Id}", row);
    }

    [HttpPut("business-assets/{id:int}")]
    public async Task<IActionResult> UpdateAsset(int id, AnalysisService.AssetInput input) => Ok(await analysis.SaveAsset(id, input));
    [HttpDelete("business-assets/{assetId:int}/processes/{processId:int}")]
    public async Task<IActionResult> RemoveAssetFromProcess(int assetId, int processId)
    {
        await analysis.RemoveAssetFromProcess(assetId, processId);
        return NoContent();
    }

    [HttpPost("system-assets")]
    public async Task<IActionResult> CreateSystem(SystemAsset input)
    {
        var row = await analysis.SaveSystem(null, input);
        return Created($"/api/system-assets/{row.Id}", row);
    }

    [HttpPut("system-assets/{id:int}")]
    public async Task<IActionResult> UpdateSystem(int id, SystemAsset input) => Ok(await analysis.SaveSystem(id, input));
    [HttpPost("business-asset-relations")]
    public async Task<IActionResult> CreateBusinessRelation(BusinessAssetSystemRelation input)
    {
        var row = await analysis.SaveBusinessRelation(null, input);
        return Created($"/api/business-asset-relations/{row.Id}", row);
    }

    [HttpPut("business-asset-relations/{id:int}")]
    public async Task<IActionResult> UpdateBusinessRelation(int id, BusinessAssetSystemRelation input) => Ok(await analysis.SaveBusinessRelation(id, input));
    [HttpPost("system-asset-relations")]
    public async Task<IActionResult> CreateSystemRelation(SystemAssetRelation input)
    {
        var row = await analysis.SaveSystemRelation(null, input);
        return Created($"/api/system-asset-relations/{row.Id}", row);
    }

    [HttpPut("system-asset-relations/{id:int}")]
    public async Task<IActionResult> UpdateSystemRelation(int id, SystemAssetRelation input) => Ok(await analysis.SaveSystemRelation(id, input));

    [HttpPut("component-controls/{threatId:int}/{controlId:int}")]
    public async Task<IActionResult> SetControl(int threatId, int controlId, AttackComponentCisControl input) => Ok(await analysis.SetControl(threatId, controlId, input));
    [HttpDelete("{resource}/{id:int}")]
    public async Task<IActionResult> Delete(string resource, int id)
    {
        await analysis.Delete(resource, id);
        return NoContent();
    }
}
