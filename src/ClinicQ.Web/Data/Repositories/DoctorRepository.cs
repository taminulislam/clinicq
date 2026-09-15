using ClinicQ.Domain.Entities;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

public sealed class DoctorRepository : RepositoryBase, IDoctorRepository
{
    public DoctorRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<IReadOnlyList<Doctor>> GetAllAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Doctors WHERE (@BranchId IS NULL OR BranchId = @BranchId) ORDER BY FullName";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<Doctor>(new CommandDefinition(sql, new { BranchId = branchId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Doctors WHERE Id = @Id";
        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.QuerySingleOrDefaultAsync<Doctor>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(Doctor doctor, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO Doctors (BranchId, FullName, Specialty, Email, LicenseNumber, IsActive)
            VALUES (@BranchId, @FullName, @Specialty, @Email, @LicenseNumber, @IsActive);
            {Dialect.SelectInsertedId}
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        doctor.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, doctor, cancellationToken: cancellationToken));
        return doctor.Id;
    }
}
