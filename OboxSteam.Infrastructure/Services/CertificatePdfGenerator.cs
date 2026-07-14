using OboxSteam.Application.DTOs.CertificateDTO;
using OboxSteam.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OboxSteam.Infrastructure.Services;

public sealed class CertificatePdfGenerator : ICertificatePdfGenerator
{
    static CertificatePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(CertificatePdfModel model)
    {
        var issueDateText = model.IssueDate.ToString("MMM d, yyyy");
        var moduleCount = model.ModuleNames.Count;
        var modulesLabel = moduleCount == 1 ? "1 Module" : $"{moduleCount} Modules";

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
                        sidebar.Item().AlignCenter().Text("OboxSTEAM")
                            .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);

                        sidebar.Item().PaddingTop(6).AlignCenter().Text("PROGRAM CERTIFICATE")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);

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

                        main.Item().PaddingTop(28).Row(footer =>
                        {
                            footer.RelativeItem().Column(brand =>
                            {
                                brand.Item().Text("OboxSTEAM").FontSize(14).Bold()
                                    .FontColor(Colors.Blue.Darken3);
                                brand.Item().Text("STEAM Education Platform").FontSize(9);
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
}
