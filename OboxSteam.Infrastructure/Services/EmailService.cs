using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.Interfaces;
using Resend;

namespace OboxSteam.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly string _fromEmail;
    private readonly string _appBaseUrl;
    private readonly IResend _resend;
    private readonly ILogger<EmailService> _logger;
    private readonly bool _skipEmailInDevelopment;

    public EmailService(IResend resend, IConfiguration configuration, ILogger<EmailService> logger)
    {
        _resend = resend;
        _logger = logger;
        _fromEmail = configuration["RESEND_FROM"] ?? "onboarding@resend.dev";
        _appBaseUrl = (configuration["APP_BASE_URL"] ?? "https://oboxsteam.com").TrimEnd('/');
        _skipEmailInDevelopment = bool.TryParse(configuration["Email:SkipInDevelopment"], out var skip) && skip;

        _logger.LogInformation("EmailService initialized. FROM={FromEmail}, SkipInDev={Skip}",
            _fromEmail, _skipEmailInDevelopment);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlContent)
    {
        try
        {
            if (_skipEmailInDevelopment)
            {
                _logger.LogWarning("EMAIL SKIPPED (Development Mode) — To: {To}, Subject: {Subject}", to, subject);
                return;
            }

            _logger.LogInformation("Sending email to {To} — Subject: {Subject}", to, subject);

            var message = new EmailMessage
            {
                From = _fromEmail,
                Subject = subject,
                HtmlBody = htmlContent
            };
            message.To.Add(to);

            var response = await _resend.EmailSendAsync(message);
            _logger.LogInformation("Email sent to {To}. Response: {@Response}", to, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To} — Subject: {Subject}", to, subject);
            throw new InvalidOperationException(
                $"Failed to send email. Verify Resend configuration and domain. Error: {ex.Message}", ex);
        }
    }

    public async Task SendRegistrationSuccessEmailAsync(EmailRequestDto request)
    {
        var html = $@"
<html style=""background-color:#0F1923;margin:0;padding:0;"">
  <body style=""font-family:'Segoe UI',Arial,sans-serif;color:#C8D6E5;padding:40px 20px;background-color:#0F1923;line-height:1.6;"">
    <div style=""max-width:560px;margin:auto;background:#1A2635;border:1px solid #2A3F5F;padding:32px;border-radius:4px;"">

      <!-- Header -->
      <div style=""text-align:center;margin-bottom:28px;padding-bottom:20px;border-bottom:1px solid #2A3F5F;"">
        <h1 style=""color:#66C0F4;font-size:24px;font-weight:bold;margin:0;letter-spacing:3px;"">OBOXSTEAM</h1>
        <p style=""color:#8899A6;font-size:11px;margin:6px 0 0 0;letter-spacing:1px;"">YOUR GAMING UNIVERSE</p>
      </div>

      <!-- Welcome Message -->
      <div style=""text-align:center;margin-bottom:28px;"">
        <h2 style=""color:#C8D6E5;font-size:20px;font-weight:bold;margin:0 0 12px 0;"">Welcome, {request.UserName}!</h2>
        <p style=""color:#8899A6;font-size:13px;margin:0;line-height:1.7;"">Your account has been successfully created. You're all set to explore the OboxSteam platform.</p>
      </div>

      <!-- Feature Cards -->
      <div style=""margin:24px 0;"">
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;"">
          <tr>
            <td style=""padding:12px;background:#0F1923;border:1px solid #2A3F5F;vertical-align:top;width:50%;"">
              <p style=""color:#66C0F4;font-size:12px;font-weight:bold;margin:0 0 4px 0;letter-spacing:1px;"">DISCOVER GAMES</p>
              <p style=""color:#8899A6;font-size:11px;margin:0;line-height:1.5;"">Browse and find your next favourite game</p>
            </td>
            <td style=""padding:12px;background:#0F1923;border:1px solid #2A3F5F;border-left:none;vertical-align:top;width:50%;"">
              <p style=""color:#4ECDC4;font-size:12px;font-weight:bold;margin:0 0 4px 0;letter-spacing:1px;"">BUILD YOUR LIBRARY</p>
              <p style=""color:#8899A6;font-size:11px;margin:0;line-height:1.5;"">Manage and organise your collection</p>
            </td>
          </tr>
          <tr>
            <td style=""padding:12px;background:#0F1923;border:1px solid #2A3F5F;border-top:none;vertical-align:top;"">
              <p style=""color:#A9C47F;font-size:12px;font-weight:bold;margin:0 0 4px 0;letter-spacing:1px;"">TRACK PROGRESS</p>
              <p style=""color:#8899A6;font-size:11px;margin:0;line-height:1.5;"">Achievements, playtime, and more</p>
            </td>
            <td style=""padding:12px;background:#0F1923;border:1px solid #2A3F5F;border-left:none;border-top:none;vertical-align:top;"">
              <p style=""color:#66C0F4;font-size:12px;font-weight:bold;margin:0 0 4px 0;letter-spacing:1px;"">CONNECT</p>
              <p style=""color:#8899A6;font-size:11px;margin:0;line-height:1.5;"">Sync with friends and the community</p>
            </td>
          </tr>
        </table>
      </div>

      <!-- CTA -->
      <div style=""text-align:center;margin:32px 0;"">
        <a href=""{_appBaseUrl}"" style=""display:inline-block;background:#66C0F4;color:#0F1923;padding:14px 36px;text-decoration:none;font-weight:bold;font-size:12px;letter-spacing:2px;border-radius:2px;"">EXPLORE NOW</a>
      </div>

      <!-- Footer -->
      <div style=""border-top:1px solid #2A3F5F;padding-top:20px;margin-top:28px;text-align:center;"">
        <p style=""color:#8899A6;font-size:10px;margin:0;letter-spacing:1px;"">Best regards,</p>
        <p style=""color:#66C0F4;font-size:11px;font-weight:bold;margin:4px 0 0 0;letter-spacing:1px;"">THE OBOXSTEAM TEAM</p>
      </div>

    </div>
  </body>
</html>";
        await SendEmailAsync(request.To, "Welcome to OboxSteam!", html);
    }

    public async Task SendOtpVerificationEmailAsync(EmailRequestDto request)
    {
        var html = $@"
<html style=""background-color:#0F1923;margin:0;padding:0;"">
  <body style=""font-family:'Segoe UI',Arial,sans-serif;color:#C8D6E5;padding:40px 20px;background-color:#0F1923;line-height:1.6;"">
    <div style=""max-width:560px;margin:auto;background:#1A2635;border:1px solid #2A3F5F;padding:32px;border-radius:4px;"">

      <!-- Header -->
      <div style=""text-align:center;margin-bottom:28px;padding-bottom:20px;border-bottom:1px solid #2A3F5F;"">
        <h1 style=""color:#66C0F4;font-size:24px;font-weight:bold;margin:0;letter-spacing:3px;"">OBOXSTEAM</h1>
        <p style=""color:#8899A6;font-size:11px;margin:6px 0 0 0;letter-spacing:1px;"">EMAIL VERIFICATION</p>
      </div>

      <!-- Message -->
      <div style=""text-align:center;margin-bottom:24px;"">
        <p style=""color:#C8D6E5;font-size:14px;margin:0 0 8px 0;font-weight:bold;"">Verify Your Email Address</p>
        <p style=""color:#8899A6;font-size:12px;margin:0;line-height:1.6;"">Enter the code below to complete your registration</p>
      </div>

      <!-- OTP Box -->
      <div style=""text-align:center;margin:28px 0;"">
        <div style=""display:inline-block;background:#0F1923;padding:20px 32px;border:2px solid #66C0F4;border-radius:4px;"">
          <div style=""color:#66C0F4;font-size:36px;font-weight:bold;letter-spacing:14px;font-family:'Courier New',monospace;"">{request.Otp}</div>
        </div>
      </div>

      <!-- Expiry -->
      <div style=""text-align:center;margin:24px 0;"">
        <div style=""display:inline-block;background:#0F1923;padding:10px 20px;border:1px solid #2A3F5F;border-radius:2px;"">
          <p style=""color:#8899A6;font-size:11px;margin:0;"">CODE EXPIRES IN <span style=""color:#66C0F4;font-weight:bold;"">10 MINUTES</span></p>
        </div>
      </div>

      <p style=""color:#8899A6;font-size:11px;margin:20px 0 0 0;text-align:center;line-height:1.6;"">If you did not request this code, you can safely ignore this email.</p>

      <!-- Footer -->
      <div style=""border-top:1px solid #2A3F5F;padding-top:20px;margin-top:28px;text-align:center;"">
        <p style=""color:#8899A6;font-size:10px;margin:0;letter-spacing:1px;"">Best regards,</p>
        <p style=""color:#66C0F4;font-size:11px;font-weight:bold;margin:4px 0 0 0;letter-spacing:1px;"">THE OBOXSTEAM TEAM</p>
      </div>

    </div>
  </body>
</html>";
        await SendEmailAsync(request.To, "Verify Your Email — OboxSteam", html);
    }

    public async Task SendForgotPasswordOtpEmailAsync(EmailRequestDto request)
    {
        var html = $@"
<html style=""background-color:#0F1923;margin:0;padding:0;"">
  <body style=""font-family:'Segoe UI',Arial,sans-serif;color:#C8D6E5;padding:40px 20px;background-color:#0F1923;line-height:1.6;"">
    <div style=""max-width:560px;margin:auto;background:#1A2635;border:1px solid #2A3F5F;padding:32px;border-radius:4px;"">

      <!-- Header -->
      <div style=""text-align:center;margin-bottom:28px;padding-bottom:20px;border-bottom:1px solid #2A3F5F;"">
        <h1 style=""color:#66C0F4;font-size:24px;font-weight:bold;margin:0;letter-spacing:3px;"">OBOXSTEAM</h1>
        <p style=""color:#8899A6;font-size:11px;margin:6px 0 0 0;letter-spacing:1px;"">PASSWORD RESET</p>
      </div>

      <!-- Message -->
      <div style=""text-align:center;margin-bottom:24px;"">
        <p style=""color:#C8D6E5;font-size:14px;margin:0 0 8px 0;font-weight:bold;"">Reset Your Password</p>
        <p style=""color:#8899A6;font-size:12px;margin:0;line-height:1.6;"">Use the code below to reset your password</p>
      </div>

      <!-- OTP Box -->
      <div style=""text-align:center;margin:28px 0;"">
        <div style=""display:inline-block;background:#0F1923;padding:20px 32px;border:2px solid #66C0F4;border-radius:4px;"">
          <div style=""color:#66C0F4;font-size:36px;font-weight:bold;letter-spacing:14px;font-family:'Courier New',monospace;"">{request.Otp}</div>
        </div>
      </div>

      <!-- Expiry -->
      <div style=""text-align:center;margin:24px 0;"">
        <div style=""display:inline-block;background:#0F1923;padding:10px 20px;border:1px solid #2A3F5F;border-radius:2px;"">
          <p style=""color:#8899A6;font-size:11px;margin:0;"">CODE EXPIRES IN <span style=""color:#66C0F4;font-weight:bold;"">15 MINUTES</span></p>
        </div>
      </div>

      <!-- Security Warning -->
      <div style=""background:#2A1A1A;padding:14px 16px;margin:20px 0;border:1px solid #5F2A2A;border-radius:2px;"">
        <p style=""color:#FF9999;font-size:11px;margin:0;text-align:center;letter-spacing:0.5px;"">SECURITY: Never share this code with anyone</p>
      </div>

      <p style=""color:#8899A6;font-size:11px;margin:16px 0 0 0;text-align:center;line-height:1.6;"">If you did not request this reset, you can safely ignore this email.</p>

      <!-- Footer -->
      <div style=""border-top:1px solid #2A3F5F;padding-top:20px;margin-top:28px;text-align:center;"">
        <p style=""color:#8899A6;font-size:10px;margin:0;letter-spacing:1px;"">Best regards,</p>
        <p style=""color:#66C0F4;font-size:11px;font-weight:bold;margin:4px 0 0 0;letter-spacing:1px;"">THE OBOXSTEAM TEAM</p>
      </div>

    </div>
  </body>
</html>";
        await SendEmailAsync(request.To, "Password Reset — OboxSteam", html);
    }

    public async Task SendPasswordChangeSuccessAsync(EmailRequestDto request)
    {
        var html = $@"
<html style=""background-color:#0F1923;margin:0;padding:0;"">
  <body style=""font-family:'Segoe UI',Arial,sans-serif;color:#C8D6E5;padding:40px 20px;background-color:#0F1923;line-height:1.6;"">
    <div style=""max-width:560px;margin:auto;background:#1A2635;border:1px solid #2A3F5F;padding:32px;border-radius:4px;"">

      <!-- Header -->
      <div style=""text-align:center;margin-bottom:28px;padding-bottom:20px;border-bottom:1px solid #2A3F5F;"">
        <h1 style=""color:#66C0F4;font-size:24px;font-weight:bold;margin:0;letter-spacing:3px;"">OBOXSTEAM</h1>
        <p style=""color:#8899A6;font-size:11px;margin:6px 0 0 0;letter-spacing:1px;"">SECURITY UPDATE</p>
      </div>

      <!-- Success Icon & Message -->
      <div style=""text-align:center;margin-bottom:24px;"">
        <div style=""display:inline-block;background:#A9C47F;width:48px;height:48px;line-height:48px;border-radius:50%;margin-bottom:16px;"">
          <span style=""font-size:24px;color:#0F1923;"">&#10003;</span>
        </div>
        <p style=""color:#A9C47F;font-size:14px;margin:0 0 8px 0;font-weight:bold;letter-spacing:1px;"">PASSWORD CHANGED</p>
      </div>

      <!-- Message -->
      <div style=""text-align:center;margin-bottom:24px;"">
        <p style=""color:#C8D6E5;font-size:14px;margin:0 0 12px 0;"">Hello <strong>{request.UserName}</strong>,</p>
        <p style=""color:#8899A6;font-size:12px;margin:0;line-height:1.7;"">Your password has been successfully updated. You can now log in with your new password.</p>
      </div>

      <!-- CTA -->
      <div style=""text-align:center;margin:28px 0;"">
        <a href=""{_appBaseUrl}/login"" style=""display:inline-block;background:#66C0F4;color:#0F1923;padding:14px 36px;text-decoration:none;font-weight:bold;font-size:12px;letter-spacing:2px;border-radius:2px;"">LOGIN NOW</a>
      </div>

      <!-- Security Warning -->
      <div style=""background:#2A1A1A;padding:14px 16px;margin:20px 0;border:1px solid #5F2A2A;border-radius:2px;"">
        <p style=""color:#FF9999;font-size:11px;margin:0;text-align:center;letter-spacing:0.5px;"">If you did not make this change, contact support immediately</p>
      </div>

      <!-- Footer -->
      <div style=""border-top:1px solid #2A3F5F;padding-top:20px;margin-top:28px;text-align:center;"">
        <p style=""color:#8899A6;font-size:10px;margin:0;letter-spacing:1px;"">Best regards,</p>
        <p style=""color:#66C0F4;font-size:11px;font-weight:bold;margin:4px 0 0 0;letter-spacing:1px;"">THE OBOXSTEAM TEAM</p>
      </div>

    </div>
  </body>
</html>";
        await SendEmailAsync(request.To, "Password Changed Successfully — OboxSteam", html);
    }
}
