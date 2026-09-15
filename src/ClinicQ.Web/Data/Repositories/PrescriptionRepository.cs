using ClinicQ.Domain.Entities;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

public sealed class PrescriptionRepository : RepositoryBase, IPrescriptionRepository
{
    public PrescriptionRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<Prescription?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Prescriptions WHERE Id = @Id; SELECT * FROM PrescriptionItems WHERE PrescriptionId = @Id ORDER BY Id;";
        await using var db = await Connections.OpenAsync(cancellationToken);
        await using var grid = await db.QueryMultipleAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        var prescription = await grid.ReadSingleOrDefaultAsync<Prescription>();
        if (prescription is null)
        {
            return null;
        }

        prescription.Items = (await grid.ReadAsync<PrescriptionItem>()).ToList();
        return prescription;
    }

    public Task<IReadOnlyList<Prescription>> ListByPatientAsync(int patientId, CancellationToken cancellationToken = default)
        => ListAsync("PatientId = @Key", patientId, cancellationToken);

    public Task<IReadOnlyList<Prescription>> ListByAppointmentAsync(int appointmentId, CancellationToken cancellationToken = default)
        => ListAsync("AppointmentId = @Key", appointmentId, cancellationToken);

    public async Task<int> CreateAsync(Prescription prescription, CancellationToken cancellationToken = default)
    {
        var insertHeader = $"""
            INSERT INTO Prescriptions (AppointmentId, PatientId, DoctorId, IssuedAt, Diagnosis, Instructions)
            VALUES (@AppointmentId, @PatientId, @DoctorId, @IssuedAt, @Diagnosis, @Instructions);
            {Dialect.SelectInsertedId}
            """;
        const string insertItem = """
            INSERT INTO PrescriptionItems (PrescriptionId, Medication, Dosage, Frequency, DurationDays, Notes)
            VALUES (@PrescriptionId, @Medication, @Dosage, @Frequency, @DurationDays, @Notes)
            """;

        await using var db = await Connections.OpenAsync(cancellationToken);
        await using var tx = await db.BeginTransactionAsync(cancellationToken);

        prescription.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(insertHeader, prescription, tx, cancellationToken: cancellationToken));
        foreach (var item in prescription.Items)
        {
            item.PrescriptionId = prescription.Id;
            await db.ExecuteAsync(new CommandDefinition(insertItem, item, tx, cancellationToken: cancellationToken));
        }

        await tx.CommitAsync(cancellationToken);
        return prescription.Id;
    }

    private async Task<IReadOnlyList<Prescription>> ListAsync(string predicate, int key, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT * FROM Prescriptions WHERE {predicate} ORDER BY IssuedAt DESC;
            SELECT i.* FROM PrescriptionItems i JOIN Prescriptions p ON p.Id = i.PrescriptionId WHERE p.{predicate} ORDER BY i.Id;
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        await using var grid = await db.QueryMultipleAsync(new CommandDefinition(sql, new { Key = key }, cancellationToken: cancellationToken));
        var prescriptions = (await grid.ReadAsync<Prescription>()).ToList();
        var items = (await grid.ReadAsync<PrescriptionItem>()).ToLookup(i => i.PrescriptionId);
        foreach (var p in prescriptions)
        {
            p.Items = items[p.Id].ToList();
        }

        return prescriptions;
    }
}
