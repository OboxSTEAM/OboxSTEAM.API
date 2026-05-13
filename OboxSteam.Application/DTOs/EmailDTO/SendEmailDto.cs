namespace OboxSteam.Application.DTOs.EmailDTO;

public class SendEmailDto
{
    public string To { get; set; }
    public string Subject { get; set; }
    public string HtmlContent { get; set; }
}
