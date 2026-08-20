using FamilyTree.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyTree.Controllers;

[ApiController]
[Route("api/familytree")]
public class FamilyTreeApiController : ControllerBase
{
    private readonly IFamilyTreeService _familyTreeService;

    public FamilyTreeApiController(IFamilyTreeService familyTreeService)
    {
        _familyTreeService = familyTreeService;
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
}
