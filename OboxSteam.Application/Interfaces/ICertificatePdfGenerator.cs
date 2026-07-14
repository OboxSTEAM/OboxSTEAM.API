using OboxSteam.Application.DTOs.CertificateDTO;

namespace OboxSteam.Application.Interfaces;

public interface ICertificatePdfGenerator
{
    /// <summary>Renders a Coursera-style program certificate PDF and returns the file bytes.</summary>
    byte[] Generate(CertificatePdfModel model);
}
