using System.Data;
using ClinicQ.Domain.Scheduling;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

/// <summary>
/// SQL Server implementation: delegates slot expansion to dbo.usp_GetAvailableSlots (db/003_procs.sql).
/// </summary>
public sealed class SqlServerSlotRepository : RepositoryBase, ISlotRepository
{
    public SqlServerSlotRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<IReadOnlyList<AppointmentSlot>> GetAvailableSlotsAsync(int branchId, int doctorId, DateOnly date, DateTime now, CancellationToken cancellationToken = default)
    {
        var args = new DynamicParameters();
        args.Add("@BranchId", branchId, DbType.Int32);
        args.Add("@DoctorId", doctorId, DbType.Int32);
        args.Add("@Date", date.ToDateTime(TimeOnly.MinValue), DbType.Date);
        args.Add("@Now", now, DbType.DateTime2);

        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<SlotRow>(new CommandDefinition("dbo.usp_GetAvailableSlots", args, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return rows.Select(r => new AppointmentSlot(r.SlotStart, r.SlotEnd, r.Capacity, r.Booked)).ToList();
    }

    private sealed class SlotRow
    {
        public DateTime SlotStart { get; set; }
        public DateTime SlotEnd { get; set; }
        public int Capacity { get; set; }
        public int Booked { get; set; }
    }
}
