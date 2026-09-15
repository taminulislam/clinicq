using ClinicQ.Domain.Entities;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

public sealed class LabReportRepository : RepositoryBase, ILabReportRepository
{
    public LabReportRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<LabReport?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM LabReports WHERE Id = @Id";
        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.QuerySingleOrDefaultAsync<LabReport>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LabReport>> ListByPatientAsync(int patientId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM LabReports WHERE PatientId = @PatientId ORDER BY UploadedAt DESC";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<LabReport>(new CommandDefinition(sql, new { PatientId = patientId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<LabReport>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM LabReports ORDER BY UploadedAt DESC {Dialect.Paging}";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<LabReport>(new CommandDefinition(sql, new { Offset = 0, Limit = limit }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> CreateAsync(LabReport report, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO LabReports (PatientId, AppointmentId, FileName, ContentType, SizeBytes, StoragePath, TestName, Notes, UploadedAt, UploadedBy)
            VALUES (@PatientId, @AppointmentId, @FileName, @ContentType, @SizeBytes, @StoragePath, @TestName, @Notes, @UploadedAt, @UploadedBy);
            {Dialect.SelectInsertedId}
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        report.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, report, cancellationToken: cancellationToken));
        return report.Id;
    }
}
