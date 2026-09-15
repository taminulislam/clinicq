using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using QuestPDF.Fluent;

namespace ClinicQ.Web.Infrastructure.Pdf;

public sealed record PrescriptionPdfModel(Prescription Prescription, Patient Patient, Doctor Doctor, Branch Branch);

public sealed record InvoicePdfModel(Invoice Invoice, Patient Patient, Branch Branch, IReadOnlyList<Payment> Payments);

/// <summary>Renders clinic documents to PDF with QuestPDF.</summary>
public interface IPdfService
{
    byte[] RenderPrescription(PrescriptionPdfModel model);
    byte[] RenderInvoice(InvoicePdfModel model);
}

public sealed class PdfService : IPdfService
{
    public byte[] RenderPrescription(PrescriptionPdfModel model) => new PrescriptionPdfDocument(model).GeneratePdf();

    public byte[] RenderInvoice(InvoicePdfModel model) => new InvoicePdfDocument(model).GeneratePdf();
}
