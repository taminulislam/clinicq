using System.Data;
using ClinicQ.Web.Data.Models;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

public sealed class DashboardRepository : RepositoryBase, IDashboardRepository
{
    public DashboardRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<IReadOnlyList<DoctorUtilizationRow>> GetDoctorUtilizationAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT d.Id AS DoctorId, d.FullName AS DoctorName, d.Specialty, d.BranchId, b.Name AS BranchName,
                   SUM(CASE WHEN a.Status = 'Completed' THEN 1 ELSE 0 END)                  AS Completed,
                   SUM(CASE WHEN a.Status IN ('Cancelled') OR a.Id IS NULL THEN 0 ELSE 1 END) AS Scheduled,
                   SUM(CASE WHEN a.Status = 'NoShow' THEN 1 ELSE 0 END)                     AS NoShows
              FROM Doctors d
              JOIN Branches b ON b.Id = d.BranchId
              LEFT JOIN Appointments a ON a.DoctorId = d.Id AND a.ScheduledStart >= @From AND a.ScheduledStart < @To
             WHERE d.IsActive = 1
             GROUP BY d.Id, d.FullName, d.Specialty, d.BranchId, b.Name
             ORDER BY b.Name, d.FullName
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<DoctorUtilizationRow>(new CommandDefinition(sql, new { From = from, To = to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<WaitTimeRow>> GetWaitTimesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT BranchId, CheckedInAt, ConsultationStartedAt
              FROM Appointments
             WHERE CheckedInAt IS NOT NULL AND ConsultationStartedAt IS NOT NULL
               AND ScheduledStart >= @From AND ScheduledStart < @To
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<WaitTimeRow>(new CommandDefinition(sql, new { From = from, To = to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<BranchRevenueRow>> GetBranchRevenueAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        await using var db = await Connections.OpenAsync(cancellationToken);

        if (Connections.Provider == DatabaseProvider.SqlServer)
        {
            var args = new DynamicParameters();
            args.Add("@From", from, DbType.DateTime2);
            args.Add("@To", to, DbType.DateTime2);
            var procRows = await db.QueryAsync<BranchRevenueRow>(new CommandDefinition("dbo.usp_GetBranchRevenue", args, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
            return procRows.ToList();
        }

        // SQLite: inline equivalent of dbo.usp_GetBranchRevenue
        const string sql = """
            SELECT b.Id AS BranchId, b.Name AS BranchName,
                   COUNT(i.Id)                   AS InvoiceCount,
                   COALESCE(SUM(i.Total), 0)      AS TotalInvoiced,
                   COALESCE(SUM(i.AmountPaid), 0) AS TotalCollected
              FROM Branches b
              LEFT JOIN Invoices i ON i.BranchId = b.Id AND i.Status <> 'Void' AND i.IssuedAt >= @From AND i.IssuedAt < @To
             GROUP BY b.Id, b.Name
             ORDER BY b.Name
            """;
        var rows = await db.QueryAsync<BranchRevenueRow>(new CommandDefinition(sql, new { From = from, To = to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StatusCountRow>> GetStatusCountsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Status, COUNT(*) AS Count
              FROM Appointments
             WHERE ScheduledStart >= @From AND ScheduledStart < @To
             GROUP BY Status
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<StatusCountRow>(new CommandDefinition(sql, new { From = from, To = to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
