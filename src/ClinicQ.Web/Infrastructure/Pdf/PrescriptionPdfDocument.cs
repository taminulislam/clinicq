using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClinicQ.Web.Infrastructure.Pdf;

/// <summary>QuestPDF layout for a prescription.</summary>
public sealed class PrescriptionPdfDocument : IDocument
{
    private readonly PrescriptionPdfModel _model;

    public PrescriptionPdfDocument(PrescriptionPdfModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Prescription {_model.Prescription.Id}",
        Author = _model.Doctor.FullName
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("ClinicQ - ").SemiBold();
                t.Span($"{_model.Branch.Name}, {_model.Branch.AddressLine}, {_model.Branch.City} {_model.Branch.State} {_model.Branch.PostalCode} - {_model.Branch.Phone}");
            });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Prescription").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                col.Item().Text($"Rx #{_model.Prescription.Id:00000}").FontSize(11);
                col.Item().Text($"Issued {_model.Prescription.IssuedAt:d MMM yyyy HH:mm}");
            });

            row.ConstantItem(220).Column(col =>
            {
                col.Item().Text(_model.Doctor.FullName).SemiBold();
                col.Item().Text(_model.Doctor.Specialty);
                col.Item().Text($"License {_model.Doctor.LicenseNumber}");
                col.Item().Text(_model.Branch.Name);
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(16).Column(col =>
        {
            col.Spacing(10);

            col.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Patient").SemiBold();
                    c.Item().Text(_model.Patient.FullName);
                    c.Item().Text($"MRN {_model.Patient.Mrn}");
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Date of birth").SemiBold();
                    c.Item().Text(_model.Patient.DateOfBirth.ToString("d MMM yyyy"));
                    c.Item().Text($"Allergies: {(string.IsNullOrWhiteSpace(_model.Patient.Allergies) ? "None recorded" : _model.Patient.Allergies)}");
                });
            });

            if (!string.IsNullOrWhiteSpace(_model.Prescription.Diagnosis))
            {
                col.Item().Text(t =>
                {
                    t.Span("Diagnosis: ").SemiBold();
                    t.Span(_model.Prescription.Diagnosis);
                });
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(3);
                    c.RelativeColumn(1);
                });

                table.Header(h =>
                {
                    foreach (var title in new[] { "Medication", "Dosage", "Frequency", "Days" })
                    {
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4).Text(title).SemiBold();
                    }
                });

                foreach (var item in _model.Prescription.Items)
                {
                    table.Cell().PaddingVertical(4).Text(item.Medication);
                    table.Cell().PaddingVertical(4).Text(item.Dosage);
                    table.Cell().PaddingVertical(4).Text(item.Frequency);
                    table.Cell().PaddingVertical(4).Text(item.DurationDays.ToString());
                }
            });

            if (!string.IsNullOrWhiteSpace(_model.Prescription.Instructions))
            {
                col.Item().PaddingTop(8).Text(t =>
                {
                    t.Span("Instructions: ").SemiBold();
                    t.Span(_model.Prescription.Instructions);
                });
            }

            col.Item().PaddingTop(40).AlignRight().Column(c =>
            {
                c.Item().Width(180).BorderTop(1).PaddingTop(4).Text(_model.Doctor.FullName);
                c.Item().Text("Signature").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });
    }
}
