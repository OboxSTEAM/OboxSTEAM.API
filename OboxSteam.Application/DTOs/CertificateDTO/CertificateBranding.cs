namespace OboxSteam.Application.DTOs.CertificateDTO;

/// <summary>Shared issuer branding for certificate API detail and PDF rendering (code constant, not config).</summary>
public static class CertificateBranding
{
    public const string IssuerName = "OboxSTEAM";

    /// <summary>Hosted Obox brand mark (same asset as MediaConvert branding).</summary>
    public const string IssuerLogoUrl =
        "https://oboxsteam-bucket-main.s3.ap-southeast-1.amazonaws.com/Seed/Material/logo-obox.png";
}
