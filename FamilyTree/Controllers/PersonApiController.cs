using FamilyTree.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyTree.Controllers;

[ApiController]
[Route("api/person")]
public class PersonApiController : ControllerBase
{
    private readonly IPersonService _personService;

    public PersonApiController(IPersonService personService)
    {
        _personService = personService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int? excludeId)
    {
        var results = await _personService.QuickSearchAsync(q ?? string.Empty, excludeId);
        return Ok(results);
    }
}
