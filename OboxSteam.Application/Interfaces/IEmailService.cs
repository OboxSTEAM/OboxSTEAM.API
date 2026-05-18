using OboxSteam.Application.DTOs.EmailDTO;

namespace OboxSteam.Application.Interfaces;

public interface IEmailService
{
    Task SendRegistrationSuccessEmailAsync(EmailRequestDto request);

    Task SendOtpVerificationEmailAsync(EmailRequestDto request);

    Task SendForgotPasswordOtpEmailAsync(EmailRequestDto request);

    Task SendPasswordChangeSuccessAsync(EmailRequestDto request);

    Task SendMagicLinkEmailAsync(ActionEmailRequestDto request);

    Task SendApproveLinkEmailAsync(ActionEmailRequestDto request);
}