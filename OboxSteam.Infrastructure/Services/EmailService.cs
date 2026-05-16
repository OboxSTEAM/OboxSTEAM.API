using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.Interfaces;
using Resend;

namespace OboxSteam.Infrastructure.Services;

public class EmailService : IEmailService
{
    private const string LogoUrl = "https://oboxsteam-bucket.s3.ap-southeast-1.amazonaws.com/obox-logo.png";

    // OboxSTEAM brand colors
    private const string ColorRed = "#E94B3C";
    private const string ColorGreen = "#7CB342";
    private const string ColorCyan = "#4FC3F7";
    private const string ColorYellow = "#FDD835";
    private const string ColorPurple = "#7E57C2";

    // Neutrals
    private const string ColorBackground = "#EEEDE6";   // outer cream wrap
    private const string ColorSurface = "#FAFAF5";      // header / CTA inset
    private const string ColorCard = "#FFFFFF";
    private const string ColorBorder = "#EEEEE8";
    private const string ColorCharcoal = "#2D2D2D";     // primary text
    private const string ColorMuted = "#6B6B6B";        // body text
    private const string ColorLight = "#ADADAD";        // captions

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

    /// <summary>
    /// Wraps body content in the full OboxSTEAM email shell:
    /// cream outer layer → white elevated card → 5-color top bar → logo header → body → footer
    /// </summary>
    private static string BuildEmailShell(string eyebrowLabel, string bodyContent)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>OboxSTEAM</title>
</head>
<body style=""margin:0;padding:0;background-color:{ColorBackground};font-family:'DM Sans','Segoe UI',Arial,sans-serif;"">

  <!-- Outer cream wrapper -->
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:{ColorBackground};padding:40px 16px;"">
    <tr>
      <td align=""center"">

        <!-- Email card — max 600px, white, elevated -->
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
               style=""max-width:600px;background-color:{ColorCard};border-radius:20px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.09),0 1px 4px rgba(0,0,0,0.06);"">

          <!-- 5-color STEAM top bar -->
          <tr>
            <td style=""padding:0;height:6px;background:linear-gradient(to right,{ColorRed} 20%,{ColorGreen} 20% 40%,{ColorCyan} 40% 60%,{ColorYellow} 60% 80%,{ColorPurple} 80%);""></td>
          </tr>

          <!-- Brand header — off-white inset -->
          <tr>
            <td align=""center"" style=""background-color:{ColorSurface};padding:28px 40px 24px;border-bottom:1px solid {ColorBorder};"">
              <table cellpadding=""0"" cellspacing=""0"" border=""0"">
                <tr>
                  <td style=""vertical-align:middle;padding-right:12px;"">
                    <img src=""{LogoUrl}"" alt=""OboxSTEAM"" width=""40"" height=""40""
                         style=""display:block;border-radius:10px;"" />
                  </td>
                  <td style=""vertical-align:middle;"">
                    <span style=""font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:22px;font-weight:800;color:{ColorCharcoal};letter-spacing:-0.02em;"">OboxSTEAM</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Eyebrow label row -->
          <tr>
            <td style=""background-color:{ColorCard};padding:32px 40px 0;"">
              <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:11px;font-weight:600;letter-spacing:0.1em;text-transform:uppercase;color:{ColorLight};"">
                {eyebrowLabel}
              </p>
            </td>
          </tr>

          <!-- Body content -->
          <tr>
            <td style=""background-color:{ColorCard};padding:16px 40px 40px;"">
              {bodyContent}
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style=""background-color:{ColorSurface};padding:20px 40px;border-top:1px solid {ColorBorder};text-align:center;"">
              <p style=""margin:0 0 4px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;color:{ColorCharcoal};"">OboxSTEAM</p>
              <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:11px;color:{ColorLight};line-height:1.6;"">A place for curious minds to learn, create &amp; grow.</p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>

</body>
</html>";
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
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Welcome, {request.UserName}.<br />Your adventure begins here.
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Your account is ready. You can now explore courses, track your progress, and start building your STEAM portfolio.
</p>

<!-- CTA inset -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:32px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <p style=""margin:0 0 16px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:13px;color:{ColorLight};"">
        Start exploring your first course
      </p>
      <a href=""{_appBaseUrl}""
         style=""display:inline-block;background-color:{ColorRed};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(233,75,60,0.30);"">
        Start Exploring
      </a>
    </td>
  </tr>
</table>

<!-- STEAM category row -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
  <tr>
    <td style=""padding-bottom:8px;"">
      <p style=""margin:0 0 12px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;letter-spacing:0.08em;text-transform:uppercase;color:{ColorLight};"">What you can explore</p>
    </td>
  </tr>
  <tr>
    <td>
      <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
        <tr>
          <td width=""19%"" style=""background-color:{ColorRed}12;border-top:3px solid {ColorRed};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:{ColorRed};"">Science</p>
          </td>
          <td width=""2%""></td>
          <td width=""19%"" style=""background-color:{ColorGreen}12;border-top:3px solid {ColorGreen};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:{ColorGreen};"">Technology</p>
          </td>
          <td width=""2%""></td>
          <td width=""19%"" style=""background-color:{ColorCyan}12;border-top:3px solid {ColorCyan};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:{ColorCyan};"">Engineering</p>
          </td>
          <td width=""2%""></td>
          <td width=""19%"" style=""background-color:{ColorYellow}20;border-top:3px solid {ColorYellow};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:#9A7C00;"">Arts</p>
          </td>
          <td width=""2%""></td>
          <td width=""19%"" style=""background-color:{ColorPurple}12;border-top:3px solid {ColorPurple};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:{ColorPurple};"">Math</p>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        await SendEmailAsync(request.To, "Welcome to OboxSTEAM", BuildEmailShell("Account Activated", body));
    }


    public async Task SendOtpVerificationEmailAsync(EmailRequestDto request)
    {
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Verify your email address.
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Use the code below to complete your registration. It expires in <strong style=""color:{ColorCharcoal};font-weight:600;"">10 minutes</strong>.
</p>

<!-- OTP display -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:32px;text-align:center;"">
      <p style=""margin:0 0 12px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:11px;font-weight:600;letter-spacing:0.1em;text-transform:uppercase;color:{ColorLight};"">Your verification code</p>
      <p style=""margin:0;font-family:'Courier New',Courier,monospace;font-size:44px;font-weight:800;letter-spacing:14px;color:{ColorCharcoal};padding-left:14px;"">{request.Otp}</p>
    </td>
  </tr>
</table>

<!-- Expiry notice -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorYellow}18;border:1px solid {ColorYellow}50;border-radius:10px;margin-bottom:24px;"">
  <tr>
    <td style=""padding:12px 20px;text-align:center;"">
      <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;color:#7A6000;"">This code expires in 10 minutes</p>
    </td>
  </tr>
</table>

<p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};text-align:center;line-height:1.6;"">
  If you did not request this code, you can safely ignore this email.
</p>";

        await SendEmailAsync(request.To, "Verify Your Email — OboxSTEAM", BuildEmailShell("Email Verification", body));
    }


    public async Task SendForgotPasswordOtpEmailAsync(EmailRequestDto request)
    {
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Reset your password.
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Use the code below to reset your password. It expires in <strong style=""color:{ColorCharcoal};font-weight:600;"">15 minutes</strong>.
</p>

<!-- OTP display -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:32px;text-align:center;"">
      <p style=""margin:0 0 12px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:11px;font-weight:600;letter-spacing:0.1em;text-transform:uppercase;color:{ColorLight};"">Your reset code</p>
      <p style=""margin:0;font-family:'Courier New',Courier,monospace;font-size:44px;font-weight:800;letter-spacing:14px;color:{ColorCharcoal};padding-left:14px;"">{request.Otp}</p>
    </td>
  </tr>
</table>

<!-- Expiry notice -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorYellow}18;border:1px solid {ColorYellow}50;border-radius:10px;margin-bottom:16px;"">
  <tr>
    <td style=""padding:12px 20px;text-align:center;"">
      <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;color:#7A6000;"">This code expires in 15 minutes</p>
    </td>
  </tr>
</table>

<!-- Security notice -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorRed}0d;border:1px solid {ColorRed}30;border-radius:10px;margin-bottom:8px;"">
  <tr>
    <td style=""padding:12px 20px;text-align:center;"">
      <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;color:{ColorRed};"">Never share this code with anyone. OboxSTEAM will never ask for it.</p>
    </td>
  </tr>
</table>

<p style=""margin:12px 0 0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};text-align:center;line-height:1.6;"">
  If you did not request a password reset, you can safely ignore this email.
</p>";

        await SendEmailAsync(request.To, "Password Reset — OboxSTEAM", BuildEmailShell("Password Reset", body));
    }


    public async Task SendPasswordChangeSuccessAsync(EmailRequestDto request)
    {
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Password changed successfully.
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Hello, <strong style=""color:{ColorCharcoal};font-weight:600;"">{request.UserName}</strong>. Your password has been updated. You can now sign in with your new credentials.
</p>

<!-- CTA inset -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <p style=""margin:0 0 16px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:13px;color:{ColorLight};"">
        Ready to continue your learning journey?
      </p>
      <a href=""{_appBaseUrl}/login""
         style=""display:inline-block;background-color:{ColorGreen};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(124,179,66,0.30);"">
        Sign In
      </a>
    </td>
  </tr>
</table>

<!-- Security notice -->
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorRed}0d;border:1px solid {ColorRed}30;border-radius:10px;"">
  <tr>
    <td style=""padding:12px 20px;text-align:center;"">
      <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;color:{ColorRed};"">If you did not make this change, contact our support team immediately.</p>
    </td>
  </tr>
</table>";

        await SendEmailAsync(request.To, "Password Changed — OboxSTEAM", BuildEmailShell("Security Update", body));
    }
}