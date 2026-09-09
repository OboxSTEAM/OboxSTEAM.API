using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Resend;

namespace OboxSteam.Infrastructure.Services;

public class EmailService : IEmailService
{
    private const string LogoUrl = "https://oboxsteam-bucket.s3.ap-southeast-1.amazonaws.com/logo/obox-logo.png";
    private const int MaxSendAttempts = 3;

    // OboxSTEAM brand colors
    private const string ColorRed = "#E94B3C";
    private const string ColorGreen = "#7CB342";
    private const string ColorCyan = "#4FC3F7";
    private const string ColorYellow = "#FDD835";
    private const string ColorPurple = "#7E57C2";

    // Neutrals
    private const string ColorBackground = "#EEEDE6";
    private const string ColorSurface = "#FAFAF5";
    private const string ColorCard = "#FFFFFF";
    private const string ColorBorder = "#EEEEE8";
    private const string ColorCharcoal = "#2D2D2D";
    private const string ColorMuted = "#6B6B6B";
    private const string ColorLight = "#ADADAD";

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
<html lang=""vi"">
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
              <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:11px;color:{ColorLight};line-height:1.6;"">Nơi những tâm hồn tò mò được học hỏi, sáng tạo và trưởng thành.</p>
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
        if (_skipEmailInDevelopment)
        {
            _logger.LogWarning("EMAIL SKIPPED (Development Mode) — To: {To}, Subject: {Subject}", to, subject);
            return;
        }

        Exception? lastError = null;
        for (var attempt = 1; attempt <= MaxSendAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Sending email to {To} — Subject: {Subject} (attempt {Attempt}/{Max})",
                    to, subject, attempt, MaxSendAttempts);

                var message = new EmailMessage
                {
                    From = _fromEmail,
                    Subject = subject,
                    HtmlBody = htmlContent
                };
                message.To.Add(to);

                var response = await _resend.EmailSendAsync(message);
                _logger.LogInformation("Email sent to {To}. Response: {@Response}", to, response);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogError(
                    ex,
                    "Failed to send email to {To} — Subject: {Subject} (attempt {Attempt}/{Max})",
                    to, subject, attempt, MaxSendAttempts);

                if (attempt < MaxSendAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
        }

        throw ErrorHelper.Internal(
            $"Failed to send email after {MaxSendAttempts} attempts. Verify Resend configuration and domain. Error: {lastError?.Message}");
    }


    public async Task SendRegistrationSuccessEmailAsync(EmailRequestDto request)
    {
        var userName = WebUtility.HtmlEncode(request.UserName ?? "");
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Chào mừng {userName}.<br />Hành trình của bạn bắt đầu tại đây.
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Tài khoản của bạn đã sẵn sàng. Bạn có thể khám phá khóa học, theo dõi tiến độ và bắt đầu xây dựng hồ sơ STEAM của mình.
</p>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:32px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <p style=""margin:0 0 16px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:13px;color:{ColorLight};"">
        Bắt đầu với khóa học đầu tiên của bạn
      </p>
      <a href=""{_appBaseUrl}""
         style=""display:inline-block;background-color:{ColorRed};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(233,75,60,0.30);"">
        Khám phá ngay
      </a>
    </td>
  </tr>
</table>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
  <tr>
    <td style=""padding-bottom:8px;"">
      <p style=""margin:0 0 12px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;letter-spacing:0.08em;text-transform:uppercase;color:{ColorLight};"">Bạn có thể khám phá</p>
    </td>
  </tr>
  <tr>
    <td>
      <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
        <tr>
          <td width=""19%"" style=""background-color:{ColorRed}12;border-top:3px solid {ColorRed};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:{ColorRed};"">Khoa học</p>
          </td>
          <td width=""2%""></td>
          <td width=""19%"" style=""background-color:{ColorGreen}12;border-top:3px solid {ColorGreen};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:{ColorGreen};"">Công nghệ</p>
          </td>
          <td width=""2%""></td>
          <td width=""19%"" style=""background-color:{ColorCyan}12;border-top:3px solid {ColorCyan};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:{ColorCyan};"">Kỹ thuật</p>
          </td>
          <td width=""2%""></td>
          <td width=""19%"" style=""background-color:{ColorYellow}20;border-top:3px solid {ColorYellow};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:#9A7C00;"">Nghệ thuật</p>
          </td>
          <td width=""2%""></td>
          <td width=""19%"" style=""background-color:{ColorPurple}12;border-top:3px solid {ColorPurple};border-radius:10px;padding:14px 10px;text-align:center;"">
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:12px;font-weight:700;color:{ColorPurple};"">Toán học</p>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>";

        await SendEmailAsync(request.To, "Chào mừng đến với OboxSTEAM", BuildEmailShell("Tài khoản đã kích hoạt", body));
    }


    public async Task SendOtpVerificationEmailAsync(EmailRequestDto request)
    {
        var otp = WebUtility.HtmlEncode(request.Otp ?? "");
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Xác minh địa chỉ email của bạn.
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Dùng mã bên dưới để hoàn tất đăng ký. Mã có hiệu lực trong <strong style=""color:{ColorCharcoal};font-weight:600;"">10 phút</strong>.
</p>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:32px;text-align:center;"">
      <p style=""margin:0 0 12px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:11px;font-weight:600;letter-spacing:0.1em;text-transform:uppercase;color:{ColorLight};"">Mã xác minh của bạn</p>
      <p style=""margin:0;font-family:'Courier New',Courier,monospace;font-size:44px;font-weight:800;letter-spacing:14px;color:{ColorCharcoal};padding-left:14px;"">{otp}</p>
    </td>
  </tr>
</table>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorYellow}18;border:1px solid {ColorYellow}50;border-radius:10px;margin-bottom:24px;"">
  <tr>
    <td style=""padding:12px 20px;text-align:center;"">
      <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;color:#7A6000;"">Mã này hết hạn sau 10 phút</p>
    </td>
  </tr>
</table>

<p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};text-align:center;line-height:1.6;"">
  Nếu bạn không yêu cầu mã này, bạn có thể bỏ qua email.
</p>";

        await SendEmailAsync(request.To, "Xác minh email — OboxSTEAM", BuildEmailShell("Xác minh email", body));
    }


    public async Task SendForgotPasswordLinkEmailAsync(ActionEmailRequestDto request)
    {
        var userName = WebUtility.HtmlEncode(request.UserName ?? "");
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Đặt lại mật khẩu của bạn.
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Xin chào <strong style=""color:{ColorCharcoal};font-weight:600;"">{userName}</strong>. Chúng tôi nhận được yêu cầu đặt lại mật khẩu. Nhấn nút bên dưới để tạo mật khẩu mới. Liên kết có hiệu lực trong <strong style=""color:{ColorCharcoal};font-weight:600;"">15 phút</strong>.
</p>
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <a href=""{request.Link}"" style=""display:inline-block;background-color:{ColorRed};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(233,75,60,0.30);"">
        Đặt lại mật khẩu
      </a>
    </td>
  </tr>
</table>
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorRed}0d;border:1px solid {ColorRed}30;border-radius:10px;margin-bottom:8px;"">
  <tr>
    <td style=""padding:12px 20px;text-align:center;"">
      <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;color:{ColorRed};"">Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.</p>
    </td>
  </tr>
</table>";

        await SendEmailAsync(request.To, "Đặt lại mật khẩu — OboxSTEAM", BuildEmailShell("Đặt lại mật khẩu", body));
    }


    public async Task SendPasswordChangeSuccessAsync(EmailRequestDto request)
    {
        var userName = WebUtility.HtmlEncode(request.UserName ?? "");
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Đổi mật khẩu thành công.
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Xin chào <strong style=""color:{ColorCharcoal};font-weight:600;"">{userName}</strong>. Mật khẩu của bạn đã được cập nhật. Bạn có thể đăng nhập bằng thông tin mới.
</p>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <p style=""margin:0 0 16px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:13px;color:{ColorLight};"">
        Sẵn sàng tiếp tục hành trình học tập?
      </p>
      <a href=""{_appBaseUrl}/login""
         style=""display:inline-block;background-color:{ColorGreen};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(124,179,66,0.30);"">
        Đăng nhập
      </a>
    </td>
  </tr>
</table>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorRed}0d;border:1px solid {ColorRed}30;border-radius:10px;"">
  <tr>
    <td style=""padding:12px 20px;text-align:center;"">
      <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;color:{ColorRed};"">Nếu bạn không thực hiện thay đổi này, hãy liên hệ hỗ trợ ngay.</p>
    </td>
  </tr>
</table>";

        await SendEmailAsync(request.To, "Đã đổi mật khẩu — OboxSTEAM", BuildEmailShell("Cập nhật bảo mật", body));
    }

    public async Task SendMagicLinkEmailAsync(ActionEmailRequestDto request)
    {
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Quyền truy cập phụ huynh đã được cấp
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Bạn được mời theo dõi tiến độ học tập của con trên OboxSTEAM. Nhấn liên kết bên dưới để đăng nhập an toàn mà không cần mật khẩu. Liên kết có hiệu lực trong 24 giờ.
</p>
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <a href=""{request.Link}"" style=""display:inline-block;background-color:{ColorCyan};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(79,195,247,0.30);"">
        Đăng nhập ngay
      </a>
    </td>
  </tr>
</table>
<p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};text-align:center;line-height:1.6;"">
  Nếu bạn không mong đợi email này, hãy bỏ qua.
</p>";

        await SendEmailAsync(request.To, "Liên kết đăng nhập nhanh — OboxSTEAM", BuildEmailShell("Liên kết đăng nhập", body));
    }


    public async Task SendApproveLinkEmailAsync(ActionEmailRequestDto request)
    {
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Yêu cầu liên kết học sinh
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Một học sinh đang yêu cầu liên kết với tài khoản của bạn trên OboxSTEAM. Vui lòng phê duyệt bằng cách nhấn nút bên dưới. Liên kết có hiệu lực trong 24 giờ.
</p>
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <a href=""{request.Link}"" style=""display:inline-block;background-color:{ColorGreen};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(124,179,66,0.30);"">
        Phê duyệt yêu cầu
      </a>
    </td>
  </tr>
</table>
<p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};text-align:center;line-height:1.6;"">
  Nếu bạn không nhận ra yêu cầu này, hãy bỏ qua email.
</p>";

        await SendEmailAsync(request.To, "Cần xử lý: Yêu cầu liên kết — OboxSTEAM", BuildEmailShell("Cần xử lý", body));
    }


    public async Task SendPaymentRequestToParentEmailAsync(PaymentRequestEmailDto request)
    {
        var formattedAmount = $"{request.Amount:N0} {request.Currency}";
        var parentName = WebUtility.HtmlEncode(request.ParentName);
        var studentName = WebUtility.HtmlEncode(request.StudentName);
        var programName = WebUtility.HtmlEncode(request.ProgramName);
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Yêu cầu thanh toán từ {studentName}
</h1>
<p style=""margin:0 0 24px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Xin chào <strong style=""color:{ColorCharcoal};font-weight:600;"">{parentName}</strong>.
  Con bạn <strong style=""color:{ColorCharcoal};font-weight:600;"">{studentName}</strong> muốn ghi danh chương trình bên dưới.
  Vui lòng xem thông tin và hoàn tất thanh toán.
</p>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:28px;"">
  <tr>
    <td style=""padding:24px 28px;"">
      <p style=""margin:0 0 6px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:11px;font-weight:600;letter-spacing:0.1em;text-transform:uppercase;color:{ColorLight};"">Chương trình</p>
      <p style=""margin:0 0 16px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:18px;font-weight:700;color:{ColorCharcoal};"">{programName}</p>
      <p style=""margin:0 0 6px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:11px;font-weight:600;letter-spacing:0.1em;text-transform:uppercase;color:{ColorLight};"">Số tiền cần thanh toán</p>
      <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:24px;font-weight:800;color:{ColorGreen};"">{formattedAmount}</p>
    </td>
  </tr>
</table>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <p style=""margin:0 0 16px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:13px;color:{ColorLight};"">
        Liên kết hết hạn sau 24 giờ
      </p>
      <a href=""{request.PaymentLink}""
         style=""display:inline-block;background-color:{ColorGreen};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(124,179,66,0.30);"">
        Thanh toán ngay
      </a>
    </td>
  </tr>
</table>
<p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};text-align:center;line-height:1.6;"">
  Nếu bạn không nhận ra yêu cầu này, hãy bỏ qua email.
</p>";

        await SendEmailAsync(request.To,
            $"Yêu cầu thanh toán: {request.ProgramName} — OboxSTEAM",
            BuildEmailShell("Yêu cầu thanh toán", body));
    }


    public async Task SendPaymentInvoiceEmailAsync(InvoiceEmailDto request)
    {
        var formattedAmount = $"{request.Amount:N0} {request.Currency}";
        var payerName = WebUtility.HtmlEncode(request.PayerName);
        var programName = WebUtility.HtmlEncode(request.ProgramName);
        var studentName = WebUtility.HtmlEncode(request.StudentName);
        var invoiceCode = WebUtility.HtmlEncode(request.InvoiceCode);
        var transactionId = WebUtility.HtmlEncode(request.TransactionId);
        var programImageHtml = string.IsNullOrEmpty(request.ThumbnailUrl)
            ? ""
            : $@"<td style=""vertical-align:middle;padding-right:12px;"">
                  <img src=""{request.ThumbnailUrl}"" alt=""{programName}"" width=""60"" height=""60"" style=""border-radius:8px;object-fit:cover;display:block;"" />
                </td>";

        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Thanh toán thành công ✓
</h1>
<p style=""margin:0 0 24px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Xin chào <strong style=""color:{ColorCharcoal};font-weight:600;"">{payerName}</strong>.
  Thanh toán của bạn đã được xử lý thành công. Dưới đây là biên lai của bạn.
</p>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:28px;"">
  <tr>
    <td style=""padding:24px 28px;"">
      <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
        <tr>
          <td style=""padding:8px 0;border-bottom:1px solid {ColorBorder};"">
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};"">Hóa đơn</p>
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:14px;font-weight:700;color:{ColorCharcoal};"">{invoiceCode}</p>
          </td>
        </tr>
        <tr>
          <td style=""padding:8px 0;border-bottom:1px solid {ColorBorder};"">
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};"">Chương trình</p>
            <table cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-top:4px;"">
              <tr>
                {programImageHtml}
                <td style=""vertical-align:middle;"">
                  <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:14px;font-weight:700;color:{ColorCharcoal};"">{programName}</p>
                </td>
              </tr>
            </table>
          </td>
        </tr>
        <tr>
          <td style=""padding:8px 0;border-bottom:1px solid {ColorBorder};"">
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};"">Học sinh</p>
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:14px;font-weight:700;color:{ColorCharcoal};"">{studentName}</p>
          </td>
        </tr>
        <tr>
          <td style=""padding:8px 0;border-bottom:1px solid {ColorBorder};"">
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};"">Số tiền đã thanh toán</p>
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:20px;font-weight:800;color:{ColorGreen};"">{formattedAmount}</p>
          </td>
        </tr>
        <tr>
          <td style=""padding:8px 0;border-bottom:1px solid {ColorBorder};"">
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};"">Mã giao dịch</p>
            <p style=""margin:0;font-family:'Courier New',Courier,monospace;font-size:13px;color:{ColorCharcoal};"">{transactionId}</p>
          </td>
        </tr>
        <tr>
          <td style=""padding:8px 0;"">
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};"">Thời gian</p>
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:14px;font-weight:600;color:{ColorCharcoal};"">{request.PaidAt:dd/MM/yyyy HH:mm} UTC</p>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>

<p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};text-align:center;line-height:1.6;"">
  Cảm ơn bạn đã thanh toán. Học sinh đã được kích hoạt ghi danh.
</p>";

        await SendEmailAsync(request.To,
            $"Biên lai thanh toán: {request.InvoiceCode} — OboxSTEAM",
            BuildEmailShell("Biên lai thanh toán", body));
    }


    public async Task SendEnrollmentConfirmationEmailAsync(EnrollmentConfirmationEmailDto request)
    {
        var studentName = WebUtility.HtmlEncode(request.StudentName);
        var programName = WebUtility.HtmlEncode(request.ProgramName);
        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Bạn đã được ghi danh, {studentName}!
</h1>
<p style=""margin:0 0 28px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Tin vui — bạn đã được ghi danh vào <strong style=""color:{ColorCharcoal};font-weight:600;"">{programName}</strong>.
  Phụ huynh đã hoàn tất thanh toán giúp bạn. Hãy bắt đầu với học phần đầu tiên!
</p>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <p style=""margin:0 0 16px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:13px;color:{ColorLight};"">
        Sẵn sàng bắt đầu hành trình học tập?
      </p>
      <a href=""{_appBaseUrl}""
         style=""display:inline-block;background-color:{ColorPurple};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(126,87,194,0.30);"">
        Bắt đầu học
      </a>
    </td>
  </tr>
</table>
<p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};text-align:center;line-height:1.6;"">
  Cần hỗ trợ? Liên hệ support@oboxsteam.com
</p>";

        await SendEmailAsync(request.To,
            $"Bạn đã ghi danh {request.ProgramName} — OboxSTEAM",
            BuildEmailShell("Xác nhận ghi danh", body));
    }

    public async Task SendInboxNotificationEmailAsync(InboxNotificationEmailDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = WebUtility.HtmlEncode(request.Title);
        var bodyText = string.IsNullOrWhiteSpace(request.Body)
            ? null
            : WebUtility.HtmlEncode(request.Body);

        var bodyParagraph = bodyText is null
            ? string.Empty
            : $@"
<p style=""margin:0 0 24px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  {bodyText}
</p>";

        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  {title}
</h1>
{bodyParagraph}
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <a href=""{_appBaseUrl}""
         style=""display:inline-block;background-color:{ColorGreen};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(124,179,66,0.30);"">
        Mở OboxSTEAM
      </a>
    </td>
  </tr>
</table>";

        await SendEmailAsync(
            request.To,
            $"{request.Title} — OboxSTEAM",
            BuildEmailShell("Thông báo", body));
    }

    public async Task SendStaffAccountCredentialsEmailAsync(StaffAccountCredentialsEmailDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userName = WebUtility.HtmlEncode(request.UserName);
        var email = WebUtility.HtmlEncode(request.Email);
        var password = WebUtility.HtmlEncode(request.Password);
        var roleLabel = WebUtility.HtmlEncode(request.RoleLabel);
        var accountCodeHtml = string.IsNullOrWhiteSpace(request.AccountCode)
            ? string.Empty
            : $@"
        <tr>
          <td style=""padding:8px 0;border-bottom:1px solid {ColorBorder};"">
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};"">Mã tài khoản</p>
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:14px;font-weight:700;color:{ColorCharcoal};"">{WebUtility.HtmlEncode(request.AccountCode)}</p>
          </td>
        </tr>";

        var body = $@"
<h1 style=""margin:0 0 12px;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:28px;font-weight:800;color:{ColorCharcoal};line-height:1.2;"">
  Tài khoản {roleLabel} của bạn đã sẵn sàng.
</h1>
<p style=""margin:0 0 24px;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:15px;color:{ColorMuted};line-height:1.7;"">
  Xin chào <strong style=""color:{ColorCharcoal};font-weight:600;"">{userName}</strong>.
  Quản trị viên đã tạo tài khoản <strong style=""color:{ColorCharcoal};font-weight:600;"">{roleLabel}</strong> cho bạn trên OboxSTEAM.
  Dùng thông tin bên dưới để đăng nhập lần đầu, rồi hãy đổi mật khẩu ngay khi vào hệ thống.
</p>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:28px;"">
  <tr>
    <td style=""padding:24px 28px;"">
      <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
        {accountCodeHtml}
        <tr>
          <td style=""padding:8px 0;border-bottom:1px solid {ColorBorder};"">
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};"">Email đăng nhập</p>
            <p style=""margin:0;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:14px;font-weight:700;color:{ColorCharcoal};"">{email}</p>
          </td>
        </tr>
        <tr>
          <td style=""padding:8px 0;"">
            <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;color:{ColorLight};"">Mật khẩu tạm thời</p>
            <p style=""margin:0;font-family:'Courier New',Courier,monospace;font-size:18px;font-weight:700;letter-spacing:1px;color:{ColorCharcoal};"">{password}</p>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;margin-bottom:20px;"">
  <tr>
    <td style=""padding:28px 32px;text-align:center;"">
      <a href=""{_appBaseUrl}/login""
         style=""display:inline-block;background-color:{ColorGreen};color:#ffffff;font-family:'Nunito','DM Sans',Arial,sans-serif;font-size:15px;font-weight:700;padding:14px 44px;border-radius:10px;text-decoration:none;box-shadow:0 4px 14px rgba(124,179,66,0.30);"">
        Đăng nhập ngay
      </a>
    </td>
  </tr>
</table>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
       style=""background-color:{ColorYellow}18;border:1px solid {ColorYellow}50;border-radius:10px;"">
  <tr>
    <td style=""padding:12px 20px;text-align:center;"">
      <p style=""margin:0;font-family:'DM Sans','Segoe UI',Arial,sans-serif;font-size:12px;font-weight:600;color:#7A6000;"">Vì lý do bảo mật, hãy đổi mật khẩu ngay sau lần đăng nhập đầu tiên.</p>
    </td>
  </tr>
</table>";

        await SendEmailAsync(
            request.To,
            $"Tài khoản {request.RoleLabel} OboxSTEAM của bạn",
            BuildEmailShell("Thông tin tài khoản", body));
    }
}
