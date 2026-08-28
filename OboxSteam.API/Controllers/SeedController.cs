using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/seed")]
[ApiController]
public class SeedController : ControllerBase
{
    private readonly ISeedService _seedService;
    private readonly IConfiguration _configuration;

    public SeedController(ISeedService seedService, IConfiguration configuration)
    {
        _seedService = seedService;
        _configuration = configuration;
    }

    /// <summary>
    /// Seeds all data (Users, Groups, Trips, Badges).
    /// </summary>
    /// <returns>Success message.</returns>
    [HttpPost("all")]
    [SwaggerOperation(
        Summary = "Seed all data",
        Description = "Seeds all database tables with sample data. Run DELETE /api/seed/clear first when resetting dev data."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    [ProducesResponseType(typeof(ApiResult), 403)]
    public async Task<IActionResult> SeedAllData()
    {
        await _seedService.SeedAllDataAsync();
        return Ok(ApiResult.Success("200", "All data seeded successfully."));
    }

    /// <summary>
    /// Seeds WS7 FE test fixtures (retake ladder scenarios A–F).
    /// </summary>
    [HttpPost("ws7")]
    [SwaggerOperation(
        Summary = "Seed WS7 FE test data",
        Description = "Idempotent WS7 program with six scenario students (STD-WS7-A..F), shared staff, "
            + "open/full/remedial classes, and pre-built redelivery states. Safe to run after POST /api/seed/all.")]
    [ProducesResponseType(typeof(ApiResult), 200)]
    public async Task<IActionResult> SeedWs7FeTestData()
    {
        await _seedService.SeedWs7FeTestDataAsync();
        return Ok(ApiResult.Success("200", "WS7 FE test data seeded successfully."));
    }

    /// <summary>
    /// Clears all data from the database.
    /// </summary>
    /// <returns>Success message.</returns>
    [HttpDelete("clear")]
    [SwaggerOperation(
        Summary = "Clear all data",
        Description = "Removes all application data from the database (PostgreSQL TRUNCATE CASCADE). EF migration history is preserved."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    [ProducesResponseType(typeof(ApiResult), 403)]
    public async Task<IActionResult> ClearAllData()
    {
        await _seedService.ClearAllDataAsync();
        return Ok(ApiResult.Success("200", "All data cleared successfully."));
    }
}
