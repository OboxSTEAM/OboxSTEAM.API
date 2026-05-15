using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.Interfaces;
using Resend;

namespace OboxSteam.Infrastructure.Services;

public class EmailService : IEmailService
{
    private const string LogoUrl = "https://oboxsteam-bucket.s3.ap-southeast-1.amazonaws.com/obox-logo.png";

    private const string ColorPurple = "#7C3AED";
    private const string ColorRed = "#EF4444";
    private const string ColorGreen = "#84CC16";
    private const string ColorBlue = "#0EA5E9";
    private const string ColorYellow = "#FBBF24";
    private const string ColorSurface = "#fef7ff";
    private const string ColorCard = "#ffffff";
    private const string ColorOnSurface = "#1d1a24";
    private const string ColorMuted = "#4a4455";
    private const string ColorBorder = "#e8dfee";

    private readonly string _fromEmail;
    private readonly string _appBaseUrl;
    private readonly IResend _resend;
    private readonly ILogger<EmailService> _logger;
    private readonly bool _skipEmailInDevelopment;

    public EmailService(IResend resend, IConfiguration configuration, ILogger<EmailService> logger)
    {
        _resend = resend;
        _logger = logger;
        _fromEmail = FormatFromAddress(configuration["RESEND_FROM"] ?? "noreply@contact.oboxsteam.website");
        _appBaseUrl = (configuration["APP_BASE_URL"] ?? "https://oboxsteam.website").TrimEnd('/');
        _skipEmailInDevelopment = bool.TryParse(configuration["Email:SkipInDevelopment"], out var skip) && skip;

        _logger.LogInformation("EmailService initialized. FROM={FromEmail}, SkipInDev={Skip}",
            _fromEmail, _skipEmailInDevelopment);
    }

    private static string FormatFromAddress(string from)
    {
        if (from.Contains('<', StringComparison.Ordinal))
            return from;

        return $"OboxSteam <{from.Trim()}>";
    }

    private static string BuildEmailShell(string subtitle, string bodyContent)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>OboxSteam</title>
</head>
<body style=""margin:0;padding:0;background-color:{ColorSurface};font-family:'Segoe UI',Arial,sans-serif;"">

  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:{ColorSurface};padding:40px 16px;"">
    <tr>
      <td align=""center"">
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:580px;"">

          <!-- Header card -->
          <tr>
            <td style=""background:{ColorCard};border-radius:24px 24px 0 0;border:1px solid {ColorBorder};border-bottom:none;padding:32px 40px 24px;text-align:center;"">
              <img src=""{LogoUrl}"" alt=""OboxSteam"" width=""56"" height=""56"" style=""display:block;margin:0 auto 16px;border-radius:14px;"" />
              <span style=""display:inline-block;background:linear-gradient(135deg,{ColorPurple},{ColorBlue});color:#ffffff;font-size:11px;font-weight:700;letter-spacing:0.08em;padding:4px 14px;border-radius:9999px;text-transform:uppercase;"">{subtitle}</span>
            </td>
          </tr>

          <!-- Body card -->
          <tr>
            <td style=""background:{ColorCard};border-left:1px solid {ColorBorder};border-right:1px solid {ColorBorder};padding:8px 40px 32px;"">
              {bodyContent}
            </td>
          </tr>

          <!-- Footer card -->
          <tr>
            <td style=""background:#f3ebfa;border-radius:0 0 24px 24px;border:1px solid {ColorBorder};border-top:none;padding:24px 40px;text-align:center;"">
              <!-- STEAM dots -->
              <table cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 16px;"">
                <tr>
                  <td style=""width:8px;height:8px;border-radius:9999px;background:{ColorRed};""></td>
                  <td width=""6""></td>
                  <td style=""width:8px;height:8px;border-radius:9999px;background:{ColorGreen};""></td>
                  <td width=""6""></td>
                  <td style=""width:8px;height:8px;border-radius:9999px;background:{ColorPurple};""></td>
                  <td width=""6""></td>
                  <td style=""width:8px;height:8px;border-radius:9999px;background:{ColorBlue};""></td>
                  <td width=""6""></td>
                  <td style=""width:8px;height:8px;border-radius:9999px;background:{ColorYellow};""></td>
                </tr>
              </table>
              <p style=""margin:0 0 4px;font-size:12px;font-weight:700;color:{ColorOnSurface};letter-spacing:0.05em;"">OboxSteam</p>
              <p style=""margin:0;font-size:11px;color:{ColorMuted};"">A place for curious minds to learn, create &amp; grow.</p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>

</body>
</html>";
    }

    private static string SteamChip(string color, string text)
    {
        return $@"<span style=""display:inline-block;background:{color}1a;color:{color};font-size:11px;font-weight:700;letter-spacing:0.06em;padding:4px 12px;border-radius:9999px;text-transform:uppercase;margin:4px;border:1px solid {color}40;"">{text}</span>";
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
        var body = $@"
<h2 style=""margin:28px 0 8px;font-size:26px;font-weight:800;color:{ColorOnSurface};text-align:center;letter-spacing:-0.01em;"">
  Welcome, <span style=""color:{ColorPurple}"">{request.UserName}</span>!
</h2>
<p style=""margin:0 0 28px;font-size:15px;color:{ColorMuted};text-align:center;line-height:1.7;"">
  Your account is ready. Dive in and start your STEAM learning adventure.
</p>

<!-- Bento feature grid -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:separate;border-spacing:8px;"">
  <tr>
    <td width=""50%"" style=""background:#f9f1ff;border:1px solid {ColorBorder};border-radius:16px;padding:18px;vertical-align:top;"">
      <div style=""width:32px;height:32px;border-radius:9999px;background:{ColorGreen}20;display:inline-flex;align-items:center;justify-content:center;margin-bottom:10px;"">
        <span style=""font-size:16px;"">🔬</span>
      </div>
      <p style=""margin:0 0 4px;font-size:12px;font-weight:700;color:{ColorGreen};letter-spacing:0.06em;text-transform:uppercase;"">Science</p>
      <p style=""margin:0;font-size:12px;color:{ColorMuted};line-height:1.5;"">Explore experiments &amp; discoveries</p>
    </td>
    <td width=""50%"" style=""background:#f9f1ff;border:1px solid {ColorBorder};border-radius:16px;padding:18px;vertical-align:top;"">
      <div style=""width:32px;height:32px;border-radius:9999px;background:{ColorBlue}20;display:inline-flex;align-items:center;justify-content:center;margin-bottom:10px;"">
        <span style=""font-size:16px;"">💻</span>
      </div>
      <p style=""margin:0 0 4px;font-size:12px;font-weight:700;color:{ColorBlue};letter-spacing:0.06em;text-transform:uppercase;"">Technology</p>
      <p style=""margin:0;font-size:12px;color:{ColorMuted};line-height:1.5;"">Build, code &amp; innovate</p>
    </td>
  </tr>
  <tr>
    <td width=""50%"" style=""background:#f9f1ff;border:1px solid {ColorBorder};border-radius:16px;padding:18px;vertical-align:top;"">
      <div style=""width:32px;height:32px;border-radius:9999px;background:{ColorRed}20;display:inline-flex;align-items:center;justify-content:center;margin-bottom:10px;"">
        <span style=""font-size:16px;"">⚙️</span>
      </div>
      <p style=""margin:0 0 4px;font-size:12px;font-weight:700;color:{ColorRed};letter-spacing:0.06em;text-transform:uppercase;"">Engineering</p>
      <p style=""margin:0;font-size:12px;color:{ColorMuted};line-height:1.5;"">Design &amp; solve real problems</p>
    </td>
    <td width=""50%"" style=""background:#f9f1ff;border:1px solid {ColorBorder};border-radius:16px;padding:18px;vertical-align:top;"">
      <div style=""width:32px;height:32px;border-radius:9999px;background:{ColorYellow}20;display:inline-flex;align-items:center;justify-content:center;margin-bottom:10px;"">
        <span style=""font-size:16px;"">🎨</span>
      </div>
      <p style=""margin:0 0 4px;font-size:12px;font-weight:700;color:{ColorYellow};letter-spacing:0.06em;text-transform:uppercase;"">Arts &amp; Math</p>
      <p style=""margin:0;font-size:12px;color:{ColorMuted};line-height:1.5;"">Create, express &amp; reason</p>
    </td>
  </tr>
</table>

<!-- CTA -->
<div style=""text-align:center;margin:32px 0 0;"">
  <a href=""{_appBaseUrl}""
     style=""display:inline-block;background:linear-gradient(135deg,{ColorPurple},{ColorBlue});color:#ffffff;font-size:14px;font-weight:700;letter-spacing:0.04em;padding:14px 40px;border-radius:12px;text-decoration:none;"">
    Start Exploring →
  </a>
</div>";

        await SendEmailAsync(request.To, "Welcome to OboxSteam! 🎉", BuildEmailShell("Account Activated", body));
    }

    public async Task SendOtpVerificationEmailAsync(EmailRequestDto request)
    {
        var body = $@"
<h2 style=""margin:28px 0 8px;font-size:24px;font-weight:800;color:{ColorOnSurface};text-align:center;letter-spacing:-0.01em;"">
  Verify your <span style=""color:{ColorPurple}"">email</span>
</h2>
<p style=""margin:0 0 32px;font-size:15px;color:{ColorMuted};text-align:center;line-height:1.7;"">
  Use the code below to complete your registration. It expires in <strong style=""color:{ColorOnSurface};"">10 minutes</strong>.
</p>

<!-- OTP display -->
<div style=""background:linear-gradient(135deg,{ColorPurple}0d,{ColorBlue}0d);border:2px solid {ColorPurple}40;border-radius:20px;padding:28px 24px;text-align:center;margin-bottom:24px;"">
  <p style=""margin:0 0 8px;font-size:11px;font-weight:700;letter-spacing:0.1em;color:{ColorMuted};text-transform:uppercase;"">Your verification code</p>
  <div style=""font-size:42px;font-weight:800;letter-spacing:16px;color:{ColorPurple};font-family:'Courier New',monospace;padding-left:16px;"">{request.Otp}</div>
</div>

<!-- Timer chip -->
<div style=""text-align:center;margin-bottom:24px;"">
  <span style=""display:inline-block;background:{ColorYellow}1a;color:#92640a;font-size:12px;font-weight:700;padding:8px 20px;border-radius:9999px;border:1px solid {ColorYellow}60;"">
    ⏱ Expires in 10 minutes
  </span>
</div>

<p style=""margin:0;font-size:12px;color:{ColorMuted};text-align:center;line-height:1.6;"">
  If you did not request this code, you can safely ignore this email.
</p>";

        await SendEmailAsync(request.To, "Verify Your Email — OboxSteam", BuildEmailShell("Email Verification", body));
    }

    public async Task SendForgotPasswordOtpEmailAsync(EmailRequestDto request)
    {
        var body = $@"
<h2 style=""margin:28px 0 8px;font-size:24px;font-weight:800;color:{ColorOnSurface};text-align:center;letter-spacing:-0.01em;"">
  Reset your <span style=""color:{ColorRed}"">password</span>
</h2>
<p style=""margin:0 0 32px;font-size:15px;color:{ColorMuted};text-align:center;line-height:1.7;"">
  Use the code below to reset your password. It expires in <strong style=""color:{ColorOnSurface};"">15 minutes</strong>.
</p>

<!-- OTP display -->
<div style=""background:linear-gradient(135deg,{ColorRed}0d,{ColorYellow}0d);border:2px solid {ColorRed}40;border-radius:20px;padding:28px 24px;text-align:center;margin-bottom:24px;"">
  <p style=""margin:0 0 8px;font-size:11px;font-weight:700;letter-spacing:0.1em;color:{ColorMuted};text-transform:uppercase;"">Your reset code</p>
  <div style=""font-size:42px;font-weight:800;letter-spacing:16px;color:{ColorRed};font-family:'Courier New',monospace;padding-left:16px;"">{request.Otp}</div>
</div>

<!-- Timer chip -->
<div style=""text-align:center;margin-bottom:24px;"">
  <span style=""display:inline-block;background:{ColorYellow}1a;color:#92640a;font-size:12px;font-weight:700;padding:8px 20px;border-radius:9999px;border:1px solid {ColorYellow}60;"">
    ⏱ Expires in 15 minutes
  </span>
</div>

<!-- Security notice -->
<div style=""background:{ColorRed}0d;border:1px solid {ColorRed}30;border-radius:12px;padding:14px 20px;margin-bottom:8px;"">
  <p style=""margin:0;font-size:12px;color:{ColorRed};text-align:center;font-weight:600;"">
    🔒 Never share this code with anyone — OboxSteam will never ask for it.
  </p>
</div>

<p style=""margin:12px 0 0;font-size:12px;color:{ColorMuted};text-align:center;line-height:1.6;"">
  If you did not request a password reset, you can safely ignore this email.
</p>";

        await SendEmailAsync(request.To, "Password Reset — OboxSteam", BuildEmailShell("Password Reset", body));
    }

    public async Task SendPasswordChangeSuccessAsync(EmailRequestDto request)
    {
        var body = $@"
<!-- Success badge -->
<div style=""text-align:center;margin:28px 0 20px;"">
  <div style=""display:inline-block;width:56px;height:56px;border-radius:9999px;background:linear-gradient(135deg,{ColorGreen},{ColorBlue});line-height:56px;text-align:center;font-size:26px;"">
    ✓
  </div>
</div>

<h2 style=""margin:0 0 8px;font-size:24px;font-weight:800;color:{ColorOnSurface};text-align:center;letter-spacing:-0.01em;"">
  Password <span style=""color:{ColorGreen}"">changed</span>
</h2>
<p style=""margin:0 0 8px;font-size:15px;color:{ColorMuted};text-align:center;line-height:1.7;"">
  Hello, <strong style=""color:{ColorOnSurface}"">{request.UserName}</strong>!
</p>
<p style=""margin:0 0 32px;font-size:15px;color:{ColorMuted};text-align:center;line-height:1.7;"">
  Your password has been successfully updated. You can now log in with your new credentials.
</p>

<!-- CTA -->
<div style=""text-align:center;margin-bottom:28px;"">
  <a href=""{_appBaseUrl}/login""
     style=""display:inline-block;background:linear-gradient(135deg,{ColorGreen},{ColorBlue});color:#ffffff;font-size:14px;font-weight:700;letter-spacing:0.04em;padding:14px 40px;border-radius:12px;text-decoration:none;"">
    Log In Now →
  </a>
</div>

<!-- Security notice -->
<div style=""background:{ColorRed}0d;border:1px solid {ColorRed}30;border-radius:12px;padding:14px 20px;"">
  <p style=""margin:0;font-size:12px;color:{ColorRed};text-align:center;font-weight:600;"">
    🔒 If you did not make this change, contact our support team immediately.
  </p>
</div>";

        await SendEmailAsync(request.To, "Password Changed Successfully — OboxSteam", BuildEmailShell("Security Update", body));
    }
}
