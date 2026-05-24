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
        Description = "Seeds all database tables with sample data."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    [ProducesResponseType(typeof(ApiResult), 403)]
    public async Task<IActionResult> SeedAllData()
    {
        // Check if in development mode
        //var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
        //if (environment != "Development")
        //{
        //    return StatusCode(403, ApiResult.Failure("403", "Seeding is only allowed in Development environment."));
        //}

        await _seedService.SeedAllDataAsync();
        return Ok(ApiResult.Success("200", "All data seeded successfully."));
    }

    /// <summary>
    /// Clears all data from the database.
    /// </summary>
    /// <returns>Success message.</returns>
    [HttpDelete("clear")]
    [SwaggerOperation(
        Summary = "Clear all data",
        Description = "Removes all data from the database."
    )]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    [ProducesResponseType(typeof(ApiResult), 403)]
    public async Task<IActionResult> ClearAllData()
    {
        //var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
        //if (environment != "Development")
        //{
        //    return StatusCode(403, ApiResult.Failure("403", "Clearing data is only allowed in Development environment."));
        //}

        await _seedService.ClearAllDataAsync();
        return Ok(ApiResult.Success("200", "All data cleared successfully."));
    }
}