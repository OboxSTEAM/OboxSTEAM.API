using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.PaymentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/payments")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    // =========================================================================
    // FLOW 1: Student initiates direct checkout
    // POST /api/payments/checkout  [Student]
    // =========================================================================

    [HttpPost("checkout")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Initiate direct checkout (student pays)",
        Description = "Student initiates checkout for a paid program. Requires a valid seat hold from select-class and matching classId. Returns a hosted payment URL.")]
    [ProducesResponseType(typeof(ApiResult<CheckoutResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateDirectCheckout([FromBody] CheckoutRequestDto dto)
    {
        var result = await _paymentService.CreateDirectCheckout(dto.ProgramId, dto.ClassId, dto.Gateway);
        return Ok(ApiResult<CheckoutResponseDto>.Success(result, "200", "Checkout session created. Redirect to checkoutUrl to complete payment."));
    }

    // =========================================================================
    // RETAKE CHECKOUT  —  POST /api/payments/checkout/retake  [Student]
    // =========================================================================
    [HttpPost("checkout/retake")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Initiate module retake checkout (student pays)",
        Description = "Student initiates checkout for a module retake fee. Returns a hosted payment URL.")]
    [ProducesResponseType(typeof(ApiResult<CheckoutResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CreateModuleRetakeCheckout([FromBody] ModuleRetakeCheckoutRequestDto dto)
    {
        var result = await _paymentService.CreateModuleRetakeCheckout(dto.ModuleEnrollmentId, dto.Gateway);
        return Ok(ApiResult<CheckoutResponseDto>.Success(result, "200", "Retake checkout session created. Redirect to checkoutUrl to complete payment."));
    }

    // =========================================================================
    // FLOW 2a: Student requests parent to pay
    // POST /api/payments/request-parent  [Student]
    // =========================================================================

    [HttpPost("request-parent")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Request parent to pay for enrollment",
        Description = "Student sends a payment request email to a linked, verified parent. Requires a valid seat hold from select-class and matching classId. Parent token expires in 5 minutes.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> RequestParentPayment([FromBody] ParentPaymentRequestDto dto)
    {
        await _paymentService.RequestParentPayment(dto.ProgramId, dto.ClassId, dto.ParentId);
        return Ok(ApiResult<object>.Success(null, "200", "Payment request sent to parent's email."));
    }

    // =========================================================================
    // POST /api/payments/request-parent/retake  [Student]
    // =========================================================================
    [HttpPost("request-parent/retake")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Request parent to pay for module retake fee",
        Description = "Student sends a payment request email to a linked, verified parent. The parent receives a 24h payment link for the module retake fee.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> RequestParentModulePayment([FromBody] ParentModulePaymentRequestDto dto)
    {
        await _paymentService.RequestParentModulePayment(dto.ModuleEnrollmentId, dto.ParentId);
        return Ok(ApiResult<object>.Success(null, "200", "Retake payment request sent to parent's email."));
    }

    // =========================================================================
    // FLOW 2b: Parent opens checkout from email token
    // POST /api/payments/parent-checkout  [AllowAnonymous]
    // =========================================================================

    [HttpPost("parent-checkout")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Parent completes checkout via token",
        Description = "Parent uses the token from their email to initiate checkout. No authentication required — the token itself is the credential.")]
    [ProducesResponseType(typeof(ApiResult<CheckoutResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CreateParentCheckout([FromBody] ParentCheckoutRequestDto dto)
    {
        var result = await _paymentService.CreateParentCheckout(dto.Token, dto.Gateway);
        return Ok(ApiResult<CheckoutResponseDto>.Success(result, "200", "Checkout session created. Redirect to checkoutUrl to complete payment."));
    }

    // =========================================================================
    // STRIPE WEBHOOK
    // POST /api/payments/stripe-webhook  [AllowAnonymous]
    // =========================================================================

    [HttpPost("stripe-webhook")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Stripe IPN webhook",
        Description = "Receives checkout.session.completed events from Stripe. Must be registered in the Stripe dashboard.")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> StripeWebhook()
    {
        string json;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            json = await reader.ReadToEndAsync();
        }

        var stripeSignature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(stripeSignature))
        {
            return BadRequest("Missing Stripe-Signature header.");
        }

        await _paymentService.HandleStripeWebhook(json, stripeSignature);
        return Ok();
    }


    // =========================================================================
    // GET payment by ID
    // GET /api/payments/{id}  [Authorized]
    // =========================================================================

    [HttpGet("{id:guid}")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Get payment details by ID",
        Description = "Returns payment details. Available to authenticated users.")]
    [ProducesResponseType(typeof(ApiResult<PaymentResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetPaymentById([FromRoute] Guid id)
    {
        var result = await _paymentService.GetPaymentById(id);
        return Ok(ApiResult<PaymentResponseDto>.Success(result, "200", "Payment retrieved successfully."));
    }

    // =========================================================================
    // CANCEL payment (FE gọi khi redirect về cancelUrl)
    // PATCH /api/payments/{id}/cancel  [AllowAnonymous]
    // =========================================================================

    [HttpPatch("{id:guid}/cancel")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Mark payment as Cancelled",
        Description = "Called by the front-end when the user is redirected back to the cancelUrl. " +
                      "Marks the Payment as Cancelled and, if applicable, rolls back the parent's " +
                      "PaymentRequest to Pending so they can retry. Idempotent — safe to call multiple times.")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> CancelPayment([FromRoute] Guid id)
    {
        await _paymentService.CancelPayment(id);
        return NoContent();
    }
}

