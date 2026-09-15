using ClinicQ.Domain.Entities;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

public sealed class UserRepository : RepositoryBase, IUserRepository
{
    public UserRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<AppUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Users WHERE Username = @Username";
        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.QuerySingleOrDefaultAsync<AppUser>(new CommandDefinition(sql, new { Username = username.Trim().ToLowerInvariant() }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Users ORDER BY Username";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<AppUser>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> CreateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO Users (Username, DisplayName, Email, PasswordHash, Role, BranchId, IsActive)
            VALUES (@Username, @DisplayName, @Email, @PasswordHash, @Role, @BranchId, @IsActive);
            {Dialect.SelectInsertedId}
            """;
        user.Username = user.Username.Trim().ToLowerInvariant();
        await using var db = await Connections.OpenAsync(cancellationToken);
        user.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, user, cancellationToken: cancellationToken));
        return user.Id;
    }
}
