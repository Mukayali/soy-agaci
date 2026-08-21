using FamilyTree.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyTree.Controllers;

public record LogExportRequest(int? PersonId, string? Format);

[ApiController]
[Route("api/familytree")]
public class FamilyTreeApiController : ControllerBase
{
    private readonly IFamilyTreeService _familyTreeService;
    private readonly IAuditLogService _auditLogService;

    public FamilyTreeApiController(IFamilyTreeService familyTreeService, IAuditLogService auditLogService)
    {
        _familyTreeService = familyTreeService;
        _auditLogService = auditLogService;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBaseTree(int id)
    {
        var graph = await _familyTreeService.GetBaseTreeAsync(id);
        if (graph == null)
        {
            return NotFound();
        }

        return Ok(graph);
    }

    [HttpGet("{id:int}/grandparents")]
    public async Task<IActionResult> GetGrandparents(int id)
    {
        return Ok(await _familyTreeService.GetGrandparentsAsync(id));
    }

    [HttpGet("{id:int}/grandchildren")]
    public async Task<IActionResult> GetGrandchildren(int id)
    {
        return Ok(await _familyTreeService.GetGrandchildrenAsync(id));
    }

    [HttpGet("{id:int}/nephews")]
    public async Task<IActionResult> GetNephews(int id)
    {
        return Ok(await _familyTreeService.GetNephewsAsync(id));
    }

    [HttpGet("{id:int}/aunts-uncles")]
    public async Task<IActionResult> GetAuntsUncles(int id)
    {
        return Ok(await _familyTreeService.GetAuntsUnclesAsync(id));
    }

    [HttpGet("{id:int}/cousins")]
    public async Task<IActionResult> GetCousins(int id)
    {
        return Ok(await _familyTreeService.GetCousinsAsync(id));
    }

    [HttpPost("log-export")]
    public async Task<IActionResult> LogExport([FromBody] LogExportRequest request)
    {
        var format = string.IsNullOrWhiteSpace(request.Format) ? "bilinmiyor" : request.Format;
        await _auditLogService.LogAsync($"Soy ağacı {format.ToUpperInvariant()} olarak dışa aktarıldı", "Person", request.PersonId);
        return NoContent();
    }
}
