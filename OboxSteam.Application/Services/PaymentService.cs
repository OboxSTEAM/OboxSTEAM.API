using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.DTOs.PaymentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IProgramEnrollmentService _programEnrollmentService;
    private readonly IStripePaymentService _stripe;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IClassRedeliveryRequestService _classRedeliveryRequestService;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IProgramEnrollmentService programEnrollmentService,
        IStripePaymentService stripe,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<PaymentService> logger,
        INotificationPublisher notificationPublisher,
        IClassRedeliveryRequestService classRedeliveryRequestService)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _programEnrollmentService = programEnrollmentService;
        _stripe = stripe;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
        _classRedeliveryRequestService = classRedeliveryRequestService;
    }

    // ══════════════════════════════════════════════════════════════════════
    // FLOW 1: Student pays directly
    // ══════════════════════════════════════════════════════════════════════

    public async Task<CheckoutResponseDto> CreateDirectCheckout(Guid programId, PaymentGateway gateway)
    {
        var studentId = _claimsService.GetCurrentUserId;
        var student = await _unitOfWork.Users.GetByIdAsync(studentId)
            ?? throw ErrorHelper.NotFound("Student not found.");

        if (student.Role != RoleType.Student)
            throw ErrorHelper.Forbidden("Only students can initiate direct checkout.");

        var program = await _unitOfWork.Programs.GetByIdAsync(programId)
            ?? throw ErrorHelper.NotFound($"Program '{programId}' not found.");

        if (program.Price == null || program.Price <= 0)
            throw ErrorHelper.BadRequest("This program cannot be purchased because it has no valid price.");

        var enrollment = await _programEnrollmentService.GetOrCreatePendingEnrollmentAsync(studentId, programId);

        // Create Payment record
        var payment = new Payment
        {
            Code = GeneratePaymentCode(),
            StudentId = studentId,
            PaidById = studentId,
            ProgramEnrollmentId = enrollment.Id,
            Amount = program.Price.Value,
            Currency = "VND",
            Gateway = gateway,
            Status = PaymentStatus.Pending
        };
        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        var description = BuildRichCheckoutDescription(program);
        // Create checkout URL
        var (checkoutUrl, sessionId) = await CreateGatewayCheckout(payment, program.Name, description, program.ThumbnailUrl, gateway);
        payment.CheckoutSessionId = sessionId;
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[CreateDirectCheckout] Student {StudentId} initiated checkout for program {ProgramId}. Payment={PaymentId}",
            studentId, programId, payment.Id);

        return new CheckoutResponseDto
        {
            PaymentId = payment.Id,
            EnrollmentId = enrollment.Id,
            CheckoutUrl = checkoutUrl
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    // FLOW 1b: Student pays for module retake fee directly
    // ══════════════════════════════════════════════════════════════════════

    public async Task<CheckoutResponseDto> CreateModuleRetakeCheckout(Guid moduleEnrollmentId, PaymentGateway gateway)
    {
        var studentId = _claimsService.GetCurrentUserId;
        var student = await _unitOfWork.Users.GetByIdAsync(studentId)
            ?? throw ErrorHelper.NotFound("Student not found.");

        if (student.Role != RoleType.Student)
            throw ErrorHelper.Forbidden("Only students can initiate module retake checkout.");

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId, me => me.Module)
            ?? throw ErrorHelper.NotFound($"Module enrollment '{moduleEnrollmentId}' not found.");

        if (moduleEnrollment.StudentId != studentId)
            throw ErrorHelper.Forbidden("This module enrollment does not belong to you.");

        if (moduleEnrollment.Status != EnrollmentStatus.PendingPayment)
            throw ErrorHelper.BadRequest("This module enrollment is not pending payment.");

        var module = moduleEnrollment.Module;
        if (module == null || module.IsDeleted)
            throw ErrorHelper.NotFound("Module not found.");

        if (module.RetakeFee <= 0)
            throw ErrorHelper.BadRequest("This module does not have a retake fee.");

        // Create Payment record
        var payment = new Payment
        {
            Code = GeneratePaymentCode(),
            StudentId = studentId,
            PaidById = studentId,
            ModuleEnrollmentId = moduleEnrollment.Id,
            Amount = module.RetakeFee,
            Currency = "VND",
            Gateway = gateway,
            Status = PaymentStatus.Pending
        };
        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        var description = $"Retake Fee for Module: {module.Name}";
        var (checkoutUrl, sessionId) = await CreateGatewayCheckout(payment, $"Retake {module.Name}", description, null, gateway);
        payment.CheckoutSessionId = sessionId;
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[CreateModuleRetakeCheckout] Student {StudentId} initiated retake checkout for module {ModuleId}. Payment={PaymentId}",
            studentId, module.Id, payment.Id);

        return new CheckoutResponseDto
        {
            PaymentId = payment.Id,
            EnrollmentId = moduleEnrollment.Id,
            CheckoutUrl = checkoutUrl
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    // FLOW 2a: Student requests parent to pay
    // ══════════════════════════════════════════════════════════════════════

    public async Task RequestParentPayment(Guid programId, Guid parentId)
    {
        var studentId = _claimsService.GetCurrentUserId;
        var student = await _unitOfWork.Users.GetByIdAsync(studentId)
            ?? throw ErrorHelper.NotFound("Student not found.");

        if (student.Role != RoleType.Student)
            throw ErrorHelper.Forbidden("Only students can send payment requests.");

        // Validate verified ParentStudent link
        var link = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(
            ps => ps.ParentId == parentId && ps.StudentId == studentId && ps.IsVerified && !ps.IsDeleted)
            ?? throw ErrorHelper.BadRequest("No verified parent-student link found with this parent.");

        var parent = await _unitOfWork.Users.GetByIdAsync(parentId)
            ?? throw ErrorHelper.NotFound("Parent not found.");

        var program = await _unitOfWork.Programs.GetByIdAsync(programId)
            ?? throw ErrorHelper.NotFound($"Program '{programId}' not found.");

        if (program.Price == null || program.Price <= 0)
            throw ErrorHelper.BadRequest("This program cannot be purchased because it has no valid price.");

        var enrollment = await _programEnrollmentService.GetOrCreatePendingEnrollmentAsync(studentId, programId);

        // Create PaymentRequest record
        var token = Guid.NewGuid().ToString("N");
        var paymentRequest = new PaymentRequest
        {
            StudentId = studentId,
            ParentId = parentId,
            ProgramId = programId,
            ProgramEnrollmentId = enrollment.Id,
            Amount = program.Price.Value,
            Currency = "VND",
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Status = PaymentRequestStatus.Pending
        };
        await _unitOfWork.PaymentRequests.AddAsync(paymentRequest);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ParentPaymentRequested(
                parentId,
                studentId,
                paymentRequest.Id,
                programId,
                enrollment.Id));

        // Build payment link for parent
        var frontendBaseUrl = (_configuration["APP_FRONTEND_URL"] ?? _configuration["APP_BASE_URL"] ?? "https://oboxsteam.website").TrimEnd('/');
        var paymentLink = $"{frontendBaseUrl}/payment/parent-checkout?token={token}";

        // Send email to parent
        await _emailService.SendPaymentRequestToParentEmailAsync(new PaymentRequestEmailDto
        {
            To = parent.Email,
            ParentName = parent.FullName ?? "Parent",
            StudentName = student.FullName ?? "Student",
            ProgramName = program.Name,
            Amount = program.Price.Value,
            Currency = "VND",
            PaymentLink = paymentLink
        });

        _logger.LogInformation(
            "[RequestParentPayment] Student {StudentId} sent payment request to parent {ParentId} for program {ProgramId}.",
            studentId, parentId, programId);
    }

    // ══════════════════════════════════════════════════════════════════════
    // FLOW 2a-retake: Student requests parent to pay for module retake
    // ══════════════════════════════════════════════════════════════════════

    public async Task RequestParentModulePayment(Guid moduleEnrollmentId, Guid parentId)
    {
        var studentId = _claimsService.GetCurrentUserId;
        var student = await _unitOfWork.Users.GetByIdAsync(studentId)
            ?? throw ErrorHelper.NotFound("Student not found.");

        if (student.Role != RoleType.Student)
            throw ErrorHelper.Forbidden("Only students can send payment requests.");

        // Validate verified ParentStudent link
        var link = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(
            ps => ps.ParentId == parentId && ps.StudentId == studentId && ps.IsVerified && !ps.IsDeleted)
            ?? throw ErrorHelper.BadRequest("No verified parent-student link found with this parent.");

        var parent = await _unitOfWork.Users.GetByIdAsync(parentId)
            ?? throw ErrorHelper.NotFound("Parent not found.");

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId, me => me.Module)
            ?? throw ErrorHelper.NotFound($"Module enrollment '{moduleEnrollmentId}' not found.");

        if (moduleEnrollment.StudentId != studentId)
            throw ErrorHelper.Forbidden("This module enrollment does not belong to you.");

        if (moduleEnrollment.Status != EnrollmentStatus.PendingPayment)
            throw ErrorHelper.BadRequest("This module enrollment is not pending payment.");

        var module = moduleEnrollment.Module;
        if (module == null || module.IsDeleted)
            throw ErrorHelper.NotFound("Module not found.");

        if (module.RetakeFee <= 0)
            throw ErrorHelper.BadRequest("This module does not have a retake fee.");

        // Create PaymentRequest record
        var token = Guid.NewGuid().ToString("N");
        var paymentRequest = new PaymentRequest
        {
            StudentId = studentId,
            ParentId = parentId,
            ModuleId = module.Id,
            ModuleEnrollmentId = moduleEnrollment.Id,
            Amount = module.RetakeFee,
            Currency = "VND",
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Status = PaymentRequestStatus.Pending
        };
        await _unitOfWork.PaymentRequests.AddAsync(paymentRequest);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ParentModuleRetakeRequested(
                parentId,
                studentId,
                paymentRequest.Id,
                module.Id,
                module.ProgramId,
                moduleEnrollment.ProgramEnrollmentId));

        // Build payment link for parent
        var frontendBaseUrl = (_configuration["APP_FRONTEND_URL"] ?? _configuration["APP_BASE_URL"] ?? "https://oboxsteam.website").TrimEnd('/');
        var paymentLink = $"{frontendBaseUrl}/payment/parent-checkout?token={token}";

        // Send email to parent
        await _emailService.SendPaymentRequestToParentEmailAsync(new PaymentRequestEmailDto
        {
            To = parent.Email,
            ParentName = parent.FullName ?? "Parent",
            StudentName = student.FullName ?? "Student",
            ProgramName = $"Module Retake: {module.Name}",
            Amount = module.RetakeFee,
            Currency = "VND",
            PaymentLink = paymentLink
        });

        _logger.LogInformation(
            "[RequestParentModulePayment] Student {StudentId} sent payment request to parent {ParentId} for module retake {ModuleId}.",
            studentId, parentId, module.Id);
    }

    // ══════════════════════════════════════════════════════════════════════
    // FLOW 2b: Parent opens checkout from token link
    // ══════════════════════════════════════════════════════════════════════

    public async Task<CheckoutResponseDto> CreateParentCheckout(string token, PaymentGateway gateway)
    {
        var now = DateTime.UtcNow;
        var paymentRequest = await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
            pr => pr.Token == token && pr.Status == PaymentRequestStatus.Pending && pr.ExpiresAt > now && !pr.IsDeleted)
            ?? throw ErrorHelper.BadRequest("Payment token is invalid, expired, or already used.");

        string itemName = "";
        string? itemDescription = null;
        string? thumbnailUrl = null;
        Guid? programEnrollmentId = null;
        Guid? moduleEnrollmentId = null;

        if (paymentRequest.ProgramEnrollmentId.HasValue)
        {
            var program = await _unitOfWork.Programs.GetByIdAsync(paymentRequest.ProgramId!.Value)
                ?? throw ErrorHelper.NotFound("Program not found.");
            itemName = program.Name;
            itemDescription = BuildRichCheckoutDescription(program);
            thumbnailUrl = program.ThumbnailUrl;
            programEnrollmentId = paymentRequest.ProgramEnrollmentId;
        }
        else if (paymentRequest.ModuleEnrollmentId.HasValue)
        {
            var module = await _unitOfWork.Modules.GetByIdAsync(paymentRequest.ModuleId!.Value)
                ?? throw ErrorHelper.NotFound("Module not found.");
            itemName = $"Retake {module.Name}";
            itemDescription = $"Retake Fee for Module: {module.Name}";
            moduleEnrollmentId = paymentRequest.ModuleEnrollmentId;
        }
        else
        {
            throw ErrorHelper.BadRequest("Invalid payment request type.");
        }

        // Create Payment record on behalf of the student, paid by parent
        var payment = new Payment
        {
            Code = GeneratePaymentCode(),
            StudentId = paymentRequest.StudentId,
            PaidById = paymentRequest.ParentId,
            ProgramEnrollmentId = programEnrollmentId,
            ModuleEnrollmentId = moduleEnrollmentId,
            Amount = paymentRequest.Amount,
            Currency = paymentRequest.Currency,
            Gateway = gateway,
            Status = PaymentStatus.Pending
        };
        await _unitOfWork.Payments.AddAsync(payment);

        // Create checkout URL
        var (checkoutUrl, sessionId) = await CreateGatewayCheckout(payment, itemName, itemDescription, thumbnailUrl, gateway);
        payment.CheckoutSessionId = sessionId;

        paymentRequest.Status = PaymentRequestStatus.Accepted;
        await _unitOfWork.SaveChangesAsync();

        var parent = await _unitOfWork.Users.GetByIdAsync(paymentRequest.ParentId)
            ?? throw ErrorHelper.NotFound("Parent not found.");

        var parentAccessToken = JwtUtils.GenerateJwtToken(
            parent.Id,
            parent.Email,
            parent.Role.ToString(),
            _configuration,
            TimeSpan.FromMinutes(30));

        _logger.LogInformation(
            "[CreateParentCheckout] Parent {ParentId} created checkout for student {StudentId}, payment request {RequestId}. Payment={PaymentId}",
            paymentRequest.ParentId, paymentRequest.StudentId, paymentRequest.Id, payment.Id);

        return new CheckoutResponseDto
        {
            PaymentId = payment.Id,
            EnrollmentId = programEnrollmentId ?? moduleEnrollmentId ?? Guid.Empty,
            CheckoutUrl = checkoutUrl,
            AccessToken = parentAccessToken
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    // WEBHOOKS
    // ══════════════════════════════════════════════════════════════════════

    public async Task HandleStripeWebhook(string json, string signature)
    {
        var (eventType, sessionId, transactionId) = await _stripe.ParseWebhookEvent(json, signature);

        switch (eventType)
        {
            case "checkout.session.completed":
            {
                _logger.LogInformation("[StripeWebhook] Processing checkout.session.completed for session {SessionId}", sessionId);

                var payment = await _unitOfWork.Payments.FirstOrDefaultAsync(
                    p => p.CheckoutSessionId == sessionId && !p.IsDeleted)
                    ?? throw ErrorHelper.NotFound($"Payment not found for Stripe session '{sessionId}'.");

                await HandlePaymentSuccess(payment, transactionId ?? sessionId);
                break;
            }

            case "checkout.session.expired":
            {
                _logger.LogInformation("[StripeWebhook] Session expired: {SessionId}", sessionId);

                var payment = await _unitOfWork.Payments.FirstOrDefaultAsync(
                    p => p.CheckoutSessionId == sessionId && !p.IsDeleted);

                if (payment != null)
                    await HandlePaymentFailed(payment);
                else
                    _logger.LogWarning("[StripeWebhook] No payment found for expired session {SessionId}", sessionId);

                break;
            }

            default:
                _logger.LogInformation("[StripeWebhook] Ignoring event type: {Type}", eventType);
                break;
        }
    }


    // ══════════════════════════════════════════════════════════════════════
    // CANCEL (FE gọi khi redirect về cancelUrl)
    // ══════════════════════════════════════════════════════════════════════

    public async Task CancelPayment(Guid paymentId)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId)
            ?? throw ErrorHelper.NotFound($"Payment '{paymentId}' not found.");

        if (payment.Status != PaymentStatus.Pending)
        {
            _logger.LogInformation(
                "[CancelPayment] Payment {Id} is already {Status}. Skipping.",
                payment.Id, payment.Status);
            return; // idempotent
        }

        payment.Status = PaymentStatus.Cancelled;

        // Rollback PaymentRequest về Pending nếu còn hạn → parent có thể thử lại
        PaymentRequest? paymentRequest = null;
        if (payment.ProgramEnrollmentId.HasValue)
        {
            paymentRequest = await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
                pr => pr.StudentId == payment.StudentId
                      && pr.ProgramEnrollmentId == payment.ProgramEnrollmentId
                      && pr.Status == PaymentRequestStatus.Accepted
                      && pr.ExpiresAt > DateTime.UtcNow
                      && !pr.IsDeleted);
        }
        else if (payment.ModuleEnrollmentId.HasValue)
        {
            paymentRequest = await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
                pr => pr.StudentId == payment.StudentId
                      && pr.ModuleEnrollmentId == payment.ModuleEnrollmentId
                      && pr.Status == PaymentRequestStatus.Accepted
                      && pr.ExpiresAt > DateTime.UtcNow
                      && !pr.IsDeleted);
        }

        if (paymentRequest != null)
        {
            paymentRequest.Status = PaymentRequestStatus.Pending;
            _logger.LogInformation(
                "[CancelPayment] Rolled back PaymentRequest {RequestId} to Pending.",
                paymentRequest.Id);
        }

        await _unitOfWork.SaveChangesAsync();

        var cancelledProgramId = await ResolveProgramIdForPaymentAsync(payment);
        await _notificationPublisher.PublishAsync(
            NotificationCatalog.PaymentCancelled(
                payment.StudentId,
                payment.Id,
                cancelledProgramId,
                payment.ProgramEnrollmentId));

        _logger.LogInformation("[CancelPayment] Payment {Id} marked Cancelled.", payment.Id);
    }

    // ══════════════════════════════════════════════════════════════════════
    // QUERY
    // ══════════════════════════════════════════════════════════════════════

    public async Task<PaymentResponseDto> GetPaymentById(Guid id)
    {
        var currentUserId = _claimsService.GetCurrentUserId;
        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId)
            ?? throw ErrorHelper.Unauthorized("User not found.");

        var payment = await _unitOfWork.Payments.GetByIdAsync(id)
            ?? throw ErrorHelper.NotFound($"Payment '{id}' not found.");

        // Check ownership: Admin or Manager can view all, otherwise user must be Student or Payer
        if (currentUser.Role != RoleType.Admin && currentUser.Role != RoleType.Manager)
        {
            if (payment.StudentId != currentUserId && payment.PaidById != currentUserId)
            {
                throw ErrorHelper.Forbidden("You do not have permission to view this payment.");
            }
        }

        return MapToDto(payment);
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════

    private async Task HandlePaymentFailed(Payment payment)
    {
        if (payment.Status != PaymentStatus.Pending)
        {
            _logger.LogInformation(
                "[HandlePaymentFailed] Payment {Id} is already {Status}. Skipping.",
                payment.Id, payment.Status);
            return;
        }

        payment.Status = PaymentStatus.Failed;

        PaymentRequest? paymentRequest = null;
        if (payment.ProgramEnrollmentId.HasValue)
        {
            paymentRequest = await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
                pr => pr.StudentId == payment.StudentId
                      && pr.ProgramEnrollmentId == payment.ProgramEnrollmentId
                      && pr.Status == PaymentRequestStatus.Accepted
                      && pr.ExpiresAt > DateTime.UtcNow
                      && !pr.IsDeleted);
        }
        else if (payment.ModuleEnrollmentId.HasValue)
        {
            paymentRequest = await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
                pr => pr.StudentId == payment.StudentId
                      && pr.ModuleEnrollmentId == payment.ModuleEnrollmentId
                      && pr.Status == PaymentRequestStatus.Accepted
                      && pr.ExpiresAt > DateTime.UtcNow
                      && !pr.IsDeleted);
        }

        if (paymentRequest != null)
        {
            paymentRequest.Status = PaymentRequestStatus.Pending;
            _logger.LogInformation(
                "[HandlePaymentFailed] Rolled back PaymentRequest {RequestId} to Pending.",
                paymentRequest.Id);
        }

        await _unitOfWork.SaveChangesAsync();

        var failedProgramId = await ResolveProgramIdForPaymentAsync(payment);
        await _notificationPublisher.PublishAsync(
            NotificationCatalog.PaymentFailed(
                payment.StudentId,
                payment.Id,
                failedProgramId,
                payment.ProgramEnrollmentId));

        _logger.LogWarning("[HandlePaymentFailed] Payment {Id} marked Failed.", payment.Id);
    }

    private async Task HandlePaymentSuccess(Payment payment, string transactionId)
    {
        if(payment.Status == PaymentStatus.Success)
        {
            _logger.LogInformation("[HandlePaymentSuccess] Payment {PaymentId} is already marked as Success. Skipping.", payment.Id);
            return;
        }

        var now = DateTime.UtcNow;

        // 1. Update Payment
        payment.Status = PaymentStatus.Success;
        payment.TransactionId = transactionId;
        payment.PaidAt = now;

        // 2. Activate enrollment (load once)
        ProgramEnrollment? enrollment = null;
        if (payment.ProgramEnrollmentId.HasValue)
        {
            enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(payment.ProgramEnrollmentId.Value);
            if (enrollment != null)
            {
                enrollment.Status = EnrollmentStatus.Active;
                enrollment.EnrolledAt = now;
            }
        }

        ModuleEnrollment? moduleEnrollment = null;
        if (payment.ModuleEnrollmentId.HasValue)
        {
            moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(payment.ModuleEnrollmentId.Value);
            if (moduleEnrollment != null)
            {
                moduleEnrollment.Status = EnrollmentStatus.Active;
                moduleEnrollment.EnrolledAt = now;
            }
        }

        // 3. Update PaymentRequest if parent paid
        PaymentRequest? paymentRequest = null;
        if (payment.ProgramEnrollmentId.HasValue)
        {
            paymentRequest = await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
                pr => pr.StudentId == payment.StudentId
                      && pr.ProgramEnrollmentId == payment.ProgramEnrollmentId
                      && pr.Status == PaymentRequestStatus.Accepted
                      && !pr.IsDeleted);
        }
        else if (payment.ModuleEnrollmentId.HasValue)
        {
            paymentRequest = await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
                pr => pr.StudentId == payment.StudentId
                      && pr.ModuleEnrollmentId == payment.ModuleEnrollmentId
                      && pr.Status == PaymentRequestStatus.Accepted
                      && !pr.IsDeleted);
        }

        if (paymentRequest != null)
        {
            paymentRequest.Status = PaymentRequestStatus.Paid;
            paymentRequest.PaymentId = payment.Id;
        }

        // 4. Load payer and student for emails and invoice info
        var payer = await _unitOfWork.Users.GetByIdAsync(payment.PaidById);
        var student = payment.PaidById != payment.StudentId
            ? await _unitOfWork.Users.GetByIdAsync(payment.StudentId)
            : payer;

        string? programName = null;
        string? programThumbnail = null;
        if (enrollment != null)
        {
            var program = await _unitOfWork.Programs.GetByIdAsync(enrollment.ProgramId);
            programName = program?.Name;
            programThumbnail = program?.ThumbnailUrl;
        }

        string? moduleName = null;
        if (moduleEnrollment != null)
        {
            var module = await _unitOfWork.Modules.GetByIdAsync(moduleEnrollment.ModuleId);
            moduleName = module?.Name;
        }

        // 5. Create Invoice with all details populated
        var invoiceNumber = GenerateInvoiceNumber();
        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            PaymentId = payment.Id,
            IssuedToId = payment.PaidById,
            Currency = payment.Currency,
            SubTotal = payment.Amount,
            TotalAmount = payment.Amount,
            BillingName = payer?.FullName ?? payer?.Email ?? string.Empty,
            BillingEmail = payer?.Email ?? string.Empty,
            ItemDescription = programName ?? (moduleName != null ? $"Module Retake: {moduleName}" : "Module Retake")
        };
        await _unitOfWork.Invoices.AddAsync(invoice);

        // 6. Save all changes atomically
        await _unitOfWork.SaveChangesAsync();

        Guid? nextActivityId = null;
        if (enrollment != null)
        {
            nextActivityId = await NotificationDeeplinkResolver.ResolveCurrentActivityIdAsync(
                _unitOfWork,
                enrollment.ProgramId,
                enrollment.Id);
        }

        var successNotifications = new List<NotificationCommand>
        {
            NotificationCatalog.PaymentSucceeded(
                payment.StudentId,
                payment.Id,
                enrollment?.ProgramId,
                payment.ProgramEnrollmentId,
                nextActivityId)
        };

        if (enrollment != null)
        {
            successNotifications.Add(
                NotificationCatalog.ProgramActivated(
                    payment.StudentId,
                    enrollment.ProgramId,
                    enrollment.Id,
                    programName,
                    nextActivityId));
        }

        await _notificationPublisher.PublishManyAsync(successNotifications);

        // 7. Send invoice email to payer
        if (payer != null)
        {
            await _emailService.SendPaymentInvoiceEmailAsync(new InvoiceEmailDto
            {
                To = payer.Email,
                PayerName = payer.FullName ?? payer.Email,
                StudentName = student?.FullName ?? "Student",
                ProgramName = programName ?? moduleName ?? "Module Retake",
                ThumbnailUrl = programThumbnail,
                Amount = payment.Amount,
                Currency = payment.Currency,
                TransactionId = payment.TransactionId ?? payment.CheckoutSessionId ?? payment.Id.ToString(),
                PaidAt = payment.PaidAt ?? now,
                InvoiceCode = invoice.InvoiceNumber
            });
        }

        // 8. If parent paid, also send confirmation to student
        if (paymentRequest != null && student != null && payer?.Id != student.Id)
        {
            await _emailService.SendEnrollmentConfirmationEmailAsync(new EnrollmentConfirmationEmailDto
            {
                To = student.Email,
                StudentName = student.FullName ?? "Student",
                ProgramName = programName ?? "Program"
            });
        }

        _logger.LogInformation(
            "[HandlePaymentSuccess] Payment {PaymentId} confirmed. Invoice={InvoiceNumber}. Student={StudentId}",
            payment.Id, invoice.InvoiceNumber, payment.StudentId);

        await _classRedeliveryRequestService.CompleteAfterPaymentAsync(payment.Id);
    }

    private async Task<Guid?> ResolveProgramIdForPaymentAsync(Payment payment)
    {
        if (!payment.ProgramEnrollmentId.HasValue)
        {
            return null;
        }

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(payment.ProgramEnrollmentId.Value);
        return enrollment?.ProgramId;
    }

    private async Task<(string checkoutUrl, string sessionId)> CreateGatewayCheckout(
        Payment payment,
        string programName,
        string? description,
        string? thumbnailUrl,
        PaymentGateway gateway)
    {
        var appUrl = (_configuration["APP_FRONTEND_URL"] ?? _configuration["APP_BASE_URL"] ?? "https://oboxsteam.website").TrimEnd('/');
        var successUrl = $"{appUrl}/payment/success?paymentId={payment.Id}";
        var cancelUrl = $"{appUrl}/payment/cancel?paymentId={payment.Id}";

        return gateway switch
        {
            PaymentGateway.Stripe => await CreateStripeCheckout(payment, programName, description, thumbnailUrl, successUrl, cancelUrl),
            _ => throw ErrorHelper.BadRequest($"Gateway '{gateway}' is not supported for online checkout.")
        };
    }

    private async Task<(string url, string sessionId)> CreateStripeCheckout(
        Payment payment, string programName, string? description, string? thumbnailUrl, string successUrl, string cancelUrl)
    {
        var (url, sessionId) = await _stripe.CreateCheckoutSession(payment, programName, description, thumbnailUrl, successUrl, cancelUrl);
        return (url, sessionId);
    }

    private static string GeneratePaymentCode()
        => $"PAY-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

    private static string GenerateInvoiceNumber()
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomPart = Guid.NewGuid().ToString("N")[..6].ToUpper();
        return $"INV-{datePart}-{randomPart}";
    }

    private static PaymentResponseDto MapToDto(Payment p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        StudentId = p.StudentId,
        PaidById = p.PaidById,
        ProgramEnrollmentId = p.ProgramEnrollmentId,
        ModuleEnrollmentId = p.ModuleEnrollmentId,
        Amount = p.Amount,
        Currency = p.Currency,
        Gateway = p.Gateway,
        TransactionId = p.TransactionId,
        CheckoutSessionId = p.CheckoutSessionId,
        Status = p.Status,
        PaidAt = p.PaidAt,
        CreatedAt = p.CreatedAt
    };

    private static string BuildRichCheckoutDescription(Program program)
    {
        if (string.IsNullOrEmpty(program.Description))
            return string.Empty;

        var plainDesc = System.Text.RegularExpressions.Regex.Replace(program.Description, "<.*?>", string.Empty).Trim();
        if (plainDesc.Length > 500)
            plainDesc = plainDesc[..500] + "...";

        return plainDesc;
    }
}
