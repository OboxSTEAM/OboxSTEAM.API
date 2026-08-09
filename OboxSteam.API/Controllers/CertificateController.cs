using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.CertificateDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/certificates")]
[ApiController]
public class CertificateController : ControllerBase
{
    private readonly ICertificateService _certificateService;

    public CertificateController(ICertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student,Parent,Admin,Manager")]
    [SwaggerOperation(
        Summary = "List my program certificates",
        Description = "Students see their own certificates. Parents see linked students. Admins see all program certificates.")]
    [ProducesResponseType(typeof(ApiResult<List<CertificateListItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> GetMyCertificates()
    {
        var result = await _certificateService.GetMyCertificatesAsync();
        return Ok(ApiResult<List<CertificateListItemDto>>.Success(
            result,
            "200",
            "Certificates retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Student,Parent,Admin,Manager")]
    [SwaggerOperation(
        Summary = "Get certificate by ID",
        Description = "Full show-page payload for an issued program certificate.")]
    [ProducesResponseType(typeof(ApiResult<CertificateDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetCertificateById([FromRoute] Guid id)
    {
        var result = await _certificateService.GetCertificateByIdAsync(id);
        return Ok(ApiResult<CertificateDetailDto>.Success(
            result,
            "200",
            "Certificate retrieved successfully."));
    }

    [HttpGet("by-enrollment/{programEnrollmentId:guid}")]
    [Authorize(Roles = "Student,Parent,Admin,Manager")]
    [SwaggerOperation(
        Summary = "Get certificate by program enrollment",
        Description = "Returns the program certificate for an enrollment when issued; otherwise null data.")]
    [ProducesResponseType(typeof(ApiResult<CertificateDetailDto?>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetCertificateByEnrollment([FromRoute] Guid programEnrollmentId)
    {
        var result = await _certificateService.GetCertificateByEnrollmentAsync(programEnrollmentId);
        return Ok(ApiResult<CertificateDetailDto?>.Success(
            result,
            "200",
            result == null
                ? "No certificate issued for this enrollment yet."
                : "Certificate retrieved successfully."));
    }

    [HttpGet("verify/{code}")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Verify a certificate by public code",
        Description = "Public endpoint for the FE share/verify page.")]
    [ProducesResponseType(typeof(ApiResult<CertificateDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> VerifyCertificate([FromRoute] string code)
    {
        var result = await _certificateService.GetCertificateByCodeAsync(code);
        return Ok(ApiResult<CertificateDetailDto>.Success(
            result,
            "200",
            "Certificate verified successfully."));
    }

    [HttpPost("program-enrollments/{programEnrollmentId:guid}/ensure")]
    [Authorize(Roles = "Student,Admin,Manager")]
    [SwaggerOperation(
        Summary = "Ensure / retry program certificate issuance",
        Description = "Issues a program certificate when all activities are Done. Regenerates and re-uploads the PDF (including avatar/thumbnail images). Reuses the same certificate code when already issued.")]
    [ProducesResponseType(typeof(ApiResult<CertificateDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> EnsureProgramCertificate([FromRoute] Guid programEnrollmentId)
    {
        var result = await _certificateService.EnsureProgramCertificateAsync(programEnrollmentId);
        if (result == null)
        {
            return BadRequest(ApiResult<object>.Failure(
                "400",
                "Certificate cannot be issued until all program activities are completed."));
        }

        return Ok(ApiResult<CertificateDetailDto>.Success(
            result,
            "200",
            "Certificate ensured successfully."));
    }
}
