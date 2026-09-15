using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Web.Data.Models;
using ClinicQ.Web.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Invoices;

public sealed class IndexModel : PageModel
{
    private readonly IInvoiceRepository _invoices;
    private readonly IBranchRepository _branches;

    public IndexModel(IInvoiceRepository invoices, IBranchRepository branches)
    {
        _invoices = invoices;
        _branches = branches;
    }

    [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
    [BindProperty(SupportsGet = true)] public InvoiceStatus? Status { get; set; }

    public IReadOnlyList<Branch> Branches { get; private set; } = Array.Empty<Branch>();
    public IReadOnlyList<InvoiceListItem> Invoices { get; private set; } = Array.Empty<InvoiceListItem>();

    public decimal TotalInvoiced => Invoices.Where(i => i.Status != InvoiceStatus.Void).Sum(i => i.Total);
    public decimal TotalOutstanding => Invoices.Where(i => i.Status != InvoiceStatus.Void).Sum(i => i.Balance);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Branches = await _branches.GetAllAsync(cancellationToken);
        Invoices = await _invoices.ListAsync(BranchId, Status, null, 5000, cancellationToken);
    }
}
