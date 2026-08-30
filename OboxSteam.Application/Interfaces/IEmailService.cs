using OboxSteam.Application.DTOs.EmailDTO;

namespace OboxSteam.Application.Interfaces;

public interface IEmailService
{
    Task SendRegistrationSuccessEmailAsync(EmailRequestDto request);

    Task SendOtpVerificationEmailAsync(EmailRequestDto request);

    Task SendPasswordChangeSuccessAsync(EmailRequestDto request);

    Task SendForgotPasswordLinkEmailAsync(ActionEmailRequestDto request);

    Task SendMagicLinkEmailAsync(ActionEmailRequestDto request);

    Task SendApproveLinkEmailAsync(ActionEmailRequestDto request);

    Task SendPaymentRequestToParentEmailAsync(PaymentRequestEmailDto request);

    Task SendPaymentInvoiceEmailAsync(InvoiceEmailDto request);

    Task SendEnrollmentConfirmationEmailAsync(EnrollmentConfirmationEmailDto request);

    Task SendInboxNotificationEmailAsync(InboxNotificationEmailDto request);
}