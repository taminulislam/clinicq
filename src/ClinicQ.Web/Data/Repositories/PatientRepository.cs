using ClinicQ.Domain.Entities;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

public sealed class PatientRepository : RepositoryBase, IPatientRepository
{
    public PatientRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<IReadOnlyList<Patient>> SearchAsync(string? search, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT * FROM Patients
            WHERE (@Search IS NULL OR {Dialect.Like("FirstName")} OR {Dialect.Like("LastName")} OR {Dialect.Like("Mrn")} OR {Dialect.Like("Email")})
            ORDER BY LastName, FirstName
            {Dialect.Paging}
            """;
        var args = new { Search = ToPattern(search), Offset = offset, Limit = limit };
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<Patient>(new CommandDefinition(sql, args, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> CountAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT COUNT(*) FROM Patients
            WHERE (@Search IS NULL OR {Dialect.Like("FirstName")} OR {Dialect.Like("LastName")} OR {Dialect.Like("Mrn")} OR {Dialect.Like("Email")})
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Search = ToPattern(search) }, cancellationToken: cancellationToken));
    }

    public async Task<Patient?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Patients WHERE Id = @Id";
        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.QuerySingleOrDefaultAsync<Patient>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Patient?> GetByMrnAsync(string mrn, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Patients WHERE Mrn = @Mrn";
        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.QuerySingleOrDefaultAsync<Patient>(new CommandDefinition(sql, new { Mrn = mrn }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO Patients (Mrn, FirstName, LastName, DateOfBirth, Gender, Email, Phone, AddressLine, City, State, PostalCode, Allergies, CreatedAt)
            VALUES (@Mrn, @FirstName, @LastName, @DateOfBirth, @Gender, @Email, @Phone, @AddressLine, @City, @State, @PostalCode, @Allergies, @CreatedAt);
            {Dialect.SelectInsertedId}
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        patient.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, patient, cancellationToken: cancellationToken));
        return patient.Id;
    }

    public async Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Patients
               SET FirstName = @FirstName, LastName = @LastName, DateOfBirth = @DateOfBirth, Gender = @Gender,
                   Email = @Email, Phone = @Phone, AddressLine = @AddressLine, City = @City, State = @State,
                   PostalCode = @PostalCode, Allergies = @Allergies
             WHERE Id = @Id
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        await db.ExecuteAsync(new CommandDefinition(sql, patient, cancellationToken: cancellationToken));
    }

    public async Task<string> NextMrnAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COALESCE(MAX(Id), 0) FROM Patients";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var maxId = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return $"MRN-{100001 + maxId:000000}";
    }

    private static string? ToPattern(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
}
