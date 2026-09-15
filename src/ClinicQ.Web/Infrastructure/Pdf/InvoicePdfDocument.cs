using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClinicQ.Web.Infrastructure.Pdf;

/// <summary>QuestPDF layout for a patient invoice.</summary>
public sealed class InvoicePdfDocument : IDocument
{
    private readonly InvoicePdfModel _model;

    public InvoicePdfDocument(InvoicePdfModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => new() { Title = $"Invoice {_model.Invoice.InvoiceNumber}", Author = "ClinicQ" };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Invoice").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                    col.Item().Text(_model.Invoice.InvoiceNumber).FontSize(11);
                    col.Item().Text($"Issued {_model.Invoice.IssuedAt:d MMM yyyy}");
                    col.Item().Text($"Due {_model.Invoice.DueDate:d MMM yyyy}");
                });
                row.ConstantItem(220).Column(col =>
                {
                    col.Item().Text(_model.Branch.Name).SemiBold();
                    col.Item().Text(_model.Branch.AddressLine);
                    col.Item().Text($"{_model.Branch.City}, {_model.Branch.State} {_model.Branch.PostalCode}");
                    col.Item().Text(_model.Branch.Phone);
                });
            });

            page.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(10);

                col.Item().Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text("Bill to").SemiBold();
                    c.Item().Text(_model.Patient.FullName);
                    c.Item().Text($"MRN {_model.Patient.Mrn}");
                    if (!string.IsNullOrWhiteSpace(_model.Patient.AddressLine))
                    {
                        c.Item().Text($"{_model.Patient.AddressLine}, {_model.Patient.City} {_model.Patient.State} {_model.Patient.PostalCode}");
                    }
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(5);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("Code");
                        h.Cell().Element(HeaderCell).Text("Description");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Unit");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Total");
                    });

                    foreach (var line in _model.Invoice.Lines)
                    {
                        table.Cell().PaddingVertical(4).Text(line.ServiceCode);
                        table.Cell().PaddingVertical(4).Text(line.Description);
                        table.Cell().PaddingVertical(4).AlignRight().Text(line.Quantity.ToString());
                        table.Cell().PaddingVertical(4).AlignRight().Text(line.UnitPrice.ToString("C2"));
                        table.Cell().PaddingVertical(4).AlignRight().Text(line.LineTotal.ToString("C2"));
                    }
                });

                col.Item().AlignRight().Width(220).Column(c =>
                {
                    Total(c, "Subtotal", _model.Invoice.Subtotal);
                    if (_model.Invoice.DiscountAmount > 0)
                    {
                        Total(c, $"Discount ({_model.Invoice.DiscountPercent:0.##}%)", -_model.Invoice.DiscountAmount);
                    }

                    Total(c, $"Tax ({_model.Invoice.TaxRatePercent:0.##}%)", _model.Invoice.TaxAmount);
                    c.Item().BorderTop(1).PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("Total").SemiBold();
                        r.ConstantItem(90).AlignRight().Text(_model.Invoice.Total.ToString("C2")).SemiBold();
                    });
                    Total(c, "Paid", _model.Invoice.AmountPaid);
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Balance due").SemiBold();
                        r.ConstantItem(90).AlignRight().Text(_model.Invoice.Balance.ToString("C2")).SemiBold()
                            .FontColor(_model.Invoice.Balance > 0 ? Colors.Red.Darken1 : Colors.Green.Darken2);
                    });
                });

                if (_model.Payments.Count > 0)
                {
                    col.Item().PaddingTop(10).Text("Payments").SemiBold();
                    foreach (var p in _model.Payments)
                    {
                        col.Item().Text($"{p.PaidAt:d MMM yyyy} - {p.Method} - {p.Amount:C2} {(string.IsNullOrEmpty(p.Reference) ? string.Empty : $"(ref {p.Reference})")}");
                    }
                }
            });

            page.Footer().AlignCenter().Text($"Status: {_model.Invoice.Status}. Thank you for choosing ClinicQ.").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static IContainer HeaderCell(IContainer c)
        => c.BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4).DefaultTextStyle(x => x.SemiBold());

    private static void Total(ColumnDescriptor column, string label, decimal amount)
    {
        column.Item().Row(r =>
        {
            r.RelativeItem().Text(label);
            r.ConstantItem(90).AlignRight().Text(amount.ToString("C2"));
        });
    }
}
