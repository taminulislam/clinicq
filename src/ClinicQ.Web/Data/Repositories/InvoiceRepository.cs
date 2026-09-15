using ClinicQ.Domain.Billing;
using ClinicQ.Web.Data.Models;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

public sealed class InvoiceRepository : RepositoryBase, IInvoiceRepository
{
    public InvoiceRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Invoices WHERE Id = @Id; SELECT * FROM InvoiceLines WHERE InvoiceId = @Id ORDER BY Id;";
        await using var db = await Connections.OpenAsync(cancellationToken);
        await using var grid = await db.QueryMultipleAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        var invoice = await grid.ReadSingleOrDefaultAsync<Invoice>();
        if (invoice is null)
        {
            return null;
        }

        invoice.Lines = (await grid.ReadAsync<InvoiceLine>()).ToList();
        return invoice;
    }

    public async Task<Invoice?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Id FROM Invoices WHERE AppointmentId = @AppointmentId AND Status <> 'Void'";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var id = await db.ExecuteScalarAsync<int?>(new CommandDefinition(sql, new { AppointmentId = appointmentId }, cancellationToken: cancellationToken));
        return id.HasValue ? await GetByIdAsync(id.Value, cancellationToken) : null;
    }

    public async Task<IReadOnlyList<InvoiceListItem>> ListAsync(int? branchId = null, InvoiceStatus? status = null, int? patientId = null, int limit = 200, CancellationToken cancellationToken = default)
    {
        var nameExpr = Connections.Provider == DatabaseProvider.SqlServer ? "p.FirstName + ' ' + p.LastName" : "p.FirstName || ' ' || p.LastName";
        var sql = $"""
            SELECT i.Id, i.InvoiceNumber, i.AppointmentId, i.PatientId, {nameExpr} AS PatientName,
                   i.BranchId, b.Name AS BranchName, i.Status, i.Total, i.AmountPaid, i.IssuedAt, i.DueDate
              FROM Invoices i
              JOIN Patients p ON p.Id = i.PatientId
              JOIN Branches b ON b.Id = i.BranchId
             WHERE (@BranchId IS NULL OR i.BranchId = @BranchId)
               AND (@Status IS NULL OR i.Status = @Status)
               AND (@PatientId IS NULL OR i.PatientId = @PatientId)
             ORDER BY i.IssuedAt DESC
             {Dialect.Paging}
            """;
        var args = new { BranchId = branchId, Status = status?.ToString(), PatientId = patientId, Offset = 0, Limit = limit };
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<InvoiceListItem>(new CommandDefinition(sql, args, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> CreateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        var insertInvoice = $"""
            INSERT INTO Invoices (InvoiceNumber, AppointmentId, PatientId, BranchId, Status, Subtotal, DiscountPercent, DiscountAmount,
                                  TaxRatePercent, TaxAmount, Total, AmountPaid, IssuedAt, DueDate, PaidAt)
            VALUES (@InvoiceNumber, @AppointmentId, @PatientId, @BranchId, @Status, @Subtotal, @DiscountPercent, @DiscountAmount,
                    @TaxRatePercent, @TaxAmount, @Total, @AmountPaid, @IssuedAt, @DueDate, @PaidAt);
            {Dialect.SelectInsertedId}
            """;
        const string insertLine = """
            INSERT INTO InvoiceLines (InvoiceId, ServiceCode, Description, Quantity, UnitPrice, LineTotal)
            VALUES (@InvoiceId, @ServiceCode, @Description, @Quantity, @UnitPrice, @LineTotal)
            """;

        await using var db = await Connections.OpenAsync(cancellationToken);
        await using var tx = await db.BeginTransactionAsync(cancellationToken);

        invoice.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(insertInvoice, ToParams(invoice), tx, cancellationToken: cancellationToken));
        foreach (var line in invoice.Lines)
        {
            line.InvoiceId = invoice.Id;
            await db.ExecuteAsync(new CommandDefinition(insertLine, line, tx, cancellationToken: cancellationToken));
        }

        await tx.CommitAsync(cancellationToken);
        return invoice.Id;
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Invoices
               SET Status = @Status, AmountPaid = @AmountPaid, PaidAt = @PaidAt, DueDate = @DueDate
             WHERE Id = @Id
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        await db.ExecuteAsync(new CommandDefinition(sql, ToParams(invoice), cancellationToken: cancellationToken));
    }

    public async Task<int> AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO Payments (InvoiceId, Amount, Method, Reference, PaidAt, ReceivedBy)
            VALUES (@InvoiceId, @Amount, @Method, @Reference, @PaidAt, @ReceivedBy);
            {Dialect.SelectInsertedId}
            """;
        var args = new { payment.InvoiceId, payment.Amount, Method = payment.Method.ToString(), payment.Reference, payment.PaidAt, payment.ReceivedBy };
        await using var db = await Connections.OpenAsync(cancellationToken);
        payment.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, args, cancellationToken: cancellationToken));
        return payment.Id;
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Payments WHERE InvoiceId = @InvoiceId ORDER BY PaidAt";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<Payment>(new CommandDefinition(sql, new { InvoiceId = invoiceId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<string> NextInvoiceNumberAsync(string branchCode, DateTime issuedAt, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COALESCE(MAX(Id), 0) FROM Invoices";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var maxId = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return $"INV-{branchCode}-{issuedAt:yyyyMM}-{maxId + 1:00000}";
    }

    private static object ToParams(Invoice i) => new
    {
        i.Id,
        i.InvoiceNumber,
        i.AppointmentId,
        i.PatientId,
        i.BranchId,
        Status = i.Status.ToString(),
        i.Subtotal,
        i.DiscountPercent,
        i.DiscountAmount,
        i.TaxRatePercent,
        i.TaxAmount,
        i.Total,
        i.AmountPaid,
        i.IssuedAt,
        i.DueDate,
        i.PaidAt
    };
}
