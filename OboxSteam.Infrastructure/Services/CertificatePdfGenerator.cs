using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.CertificateDTO;
using OboxSteam.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OboxSteam.Infrastructure.Services;

public sealed class CertificatePdfGenerator : ICertificatePdfGenerator
{
    private static readonly SemaphoreSlim LogoLock = new(1, 1);
    private static byte[]? _cachedLogoBytes;
    private static string? _cachedLogoUrl;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CertificatePdfGenerator> _logger;

    static CertificatePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public CertificatePdfGenerator(
        IHttpClientFactory httpClientFactory,
        ILogger<CertificatePdfGenerator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public byte[] Generate(CertificatePdfModel model)
    {
        var issueDateText = model.IssueDate.ToString("MMM d, yyyy");
        var moduleCount = model.ModuleNames.Count;
        var modulesLabel = moduleCount == 1 ? "1 Module" : $"{moduleCount} Modules";
        var logoUrl = string.IsNullOrWhiteSpace(model.IssuerLogoUrl)
            ? CertificateBranding.IssuerLogoUrl
            : model.IssuerLogoUrl;
        var logoBytes = GetLogoBytes(logoUrl);
        var avatarBytes = TryDownloadImageBytes(model.StudentAvatarUrl, "student avatar");
        var thumbnailBytes = TryDownloadImageBytes(model.ProgramThumbnailUrl, "program thumbnail");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));

                page.Content().Row(row =>
                {
                    row.ConstantItem(180).Background(Colors.Grey.Lighten3).Padding(20).Column(sidebar =>
                    {
                        if (logoBytes is { Length: > 0 })
                        {
                            sidebar.Item().AlignCenter().Width(72).Height(72)
                                .Image(logoBytes)
                                .FitArea();
                        }

                        sidebar.Item().PaddingTop(8).AlignCenter().Text("OboxSTEAM")
                            .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);

                        sidebar.Item().PaddingTop(6).AlignCenter().Text("PROGRAM CERTIFICATE")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);

                        if (avatarBytes is { Length: > 0 })
                        {
                            sidebar.Item().PaddingTop(18).AlignCenter().Width(96).Height(96)
                                .Image(avatarBytes)
                                .FitArea();
                        }

                        sidebar.Item().PaddingTop(20).Background(Colors.Grey.Darken3).Padding(8)
                            .AlignCenter().Text(modulesLabel)
                            .FontSize(11).Bold().FontColor(Colors.White);

                        sidebar.Item().PaddingTop(16).Column(list =>
                        {
                            foreach (var moduleName in model.ModuleNames)
                            {
                                list.Item().PaddingBottom(6).Text(moduleName).FontSize(10);
                            }
                        });
                    });

                    row.RelativeItem().Padding(28).Column(main =>
                    {
                        main.Item().Text(issueDateText).FontSize(12);

                        main.Item().PaddingTop(12).Text(model.StudentFullName)
                            .FontSize(28).Bold().FontColor(Colors.Black);

                        main.Item().PaddingTop(8).Text("has successfully completed the online program")
                            .FontSize(12);

                        main.Item().PaddingTop(12).Text(model.ProgramName)
                            .FontSize(22).Bold().FontColor(Colors.Black);

                        if (!string.IsNullOrWhiteSpace(model.ProgramDescription))
                        {
                            main.Item().PaddingTop(10).Text(model.ProgramDescription)
                                .FontSize(10).FontColor(Colors.Grey.Darken2);
                        }

                        if (thumbnailBytes is { Length: > 0 })
                        {
                            main.Item().PaddingTop(14).Height(110).Image(thumbnailBytes).FitArea();
                        }

                        main.Item().PaddingTop(28).Row(footer =>
                        {
                            footer.RelativeItem().Row(brand =>
                            {
                                if (logoBytes is { Length: > 0 })
                                {
                                    brand.ConstantItem(40).AlignMiddle().Width(36).Height(36)
                                        .Image(logoBytes)
                                        .FitArea();
                                }

                                brand.RelativeItem().PaddingLeft(8).AlignMiddle().Column(text =>
                                {
                                    text.Item().Text("OboxSTEAM").FontSize(14).Bold()
                                        .FontColor(Colors.Blue.Darken3);
                                    text.Item().Text("STEAM Education Platform").FontSize(9);
                                });
                            });

                            footer.RelativeItem().AlignRight().Column(verify =>
                            {
                                verify.Item().Text($"Certificate ID: {model.Code}").FontSize(9);
                                verify.Item().PaddingTop(4).Text("Verify this certificate at:").FontSize(9);
                                verify.Item().Text(model.VerificationUrl)
                                    .FontSize(8).FontColor(Colors.Blue.Medium);
                            });
                        });

                        main.Item().PaddingTop(20).Text(
                                "This certificate confirms successful completion of the online program activities on OboxSTEAM. " +
                                "It does not constitute formal academic enrollment or confer a university degree.")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private byte[] GetLogoBytes(string logoUrl)
    {
        if (_cachedLogoBytes is { Length: > 0 }
            && string.Equals(_cachedLogoUrl, logoUrl, StringComparison.Ordinal))
        {
            return _cachedLogoBytes;
        }

        LogoLock.Wait();
        try
        {
            if (_cachedLogoBytes is { Length: > 0 }
                && string.Equals(_cachedLogoUrl, logoUrl, StringComparison.Ordinal))
            {
                return _cachedLogoBytes;
            }

            var bytes = TryDownloadImageBytes(logoUrl, "issuer logo");
            if (bytes is { Length: > 0 })
            {
                _cachedLogoBytes = bytes;
                _cachedLogoUrl = logoUrl;
            }

            return _cachedLogoBytes ?? [];
        }
        finally
        {
            LogoLock.Release();
        }
    }

    private byte[]? TryDownloadImageBytes(string? url, string label)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogWarning(
                "[CertificatePdfGenerator] Skipping invalid {Label} URL: {Url}",
                label,
                url);
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", "OboxSTEAM-CertificatePdf/1.0");
            using var response = client.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[CertificatePdfGenerator] Failed to load {Label} from {Url}. Status: {StatusCode}",
                    label,
                    url,
                    (int)response.StatusCode);
                return null;
            }

            var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            if (bytes.Length == 0)
            {
                _logger.LogWarning(
                    "[CertificatePdfGenerator] Empty {Label} payload from {Url}.",
                    label,
                    url);
                return null;
            }

            _logger.LogInformation(
                "[CertificatePdfGenerator] Loaded {Label} from {Url} ({Bytes} bytes).",
                label,
                url,
                bytes.Length);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CertificatePdfGenerator] Failed to load {Label} from {Url}.", label, url);
            return null;
        }
    }
}
