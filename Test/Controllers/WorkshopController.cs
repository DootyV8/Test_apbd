using Microsoft.AspNetCore.Mvc;
using Test.Exceptions;
using Test.Models;
using Test.Services;
using TestGroupB.Services;

namespace Test.Controllers;

[ApiController]
[Route("api")]
public class WorkshopController : ControllerBase
{
    private readonly IDbService _dbService;

    public WorkshopController(IDbService dbService)
    {
        _dbService = dbService;
    }

    [HttpGet("visits/{id}")]
    public async Task<IActionResult> GetVisit(int id)
    {
        try
        {
            var result = await _dbService.GetVisit(id);
            return Ok(result);
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }

    [HttpPost("visits")]
    public async Task<IActionResult> CreateVisit([FromBody] NewVisitDTO newVisit)
    {
        if (newVisit.services == null || !newVisit.services.Any())
            return BadRequest("At least one service is required.");

        try
        {
            await _dbService.CreateVisit(newVisit);
            return CreatedAtAction(nameof(GetVisit), new { id = newVisit.visitId }, null);
        }
        catch (ConflictException e)
        {
            return Conflict(e.Message);
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
}