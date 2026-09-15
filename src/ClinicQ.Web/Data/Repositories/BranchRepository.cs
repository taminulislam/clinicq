using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Scheduling;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

public sealed class BranchRepository : RepositoryBase, IBranchRepository
{
    public BranchRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Branches ORDER BY Name";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<Branch>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<Branch?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Branches WHERE Id = @Id";
        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.QuerySingleOrDefaultAsync<Branch>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO Branches (Code, Name, AddressLine, City, State, PostalCode, Phone, TimeZoneId, TaxRatePercent, IsActive)
            VALUES (@Code, @Name, @AddressLine, @City, @State, @PostalCode, @Phone, @TimeZoneId, @TaxRatePercent, @IsActive);
            {Dialect.SelectInsertedId}
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        branch.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, branch, cancellationToken: cancellationToken));
        return branch.Id;
    }

    public async Task<BranchSlotRule?> GetSlotRuleAsync(int branchId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM BranchSlotRules WHERE BranchId = @BranchId";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var row = await db.QuerySingleOrDefaultAsync<SlotRuleRow>(new CommandDefinition(sql, new { BranchId = branchId }, cancellationToken: cancellationToken));
        return row?.ToDomain();
    }

    public async Task UpsertSlotRuleAsync(BranchSlotRule rule, CancellationToken cancellationToken = default)
    {
        const string update = """
            UPDATE BranchSlotRules
               SET OpenTime = @OpenTime, CloseTime = @CloseTime, SlotDurationMinutes = @SlotDurationMinutes,
                   WorkingDaysMask = @WorkingDaysMask, MaxBookingsPerSlot = @MaxBookingsPerSlot,
                   MinLeadTimeHours = @MinLeadTimeHours, MaxAdvanceDays = @MaxAdvanceDays
             WHERE BranchId = @BranchId
            """;
        var insert = $"""
            INSERT INTO BranchSlotRules (BranchId, OpenTime, CloseTime, SlotDurationMinutes, WorkingDaysMask, MaxBookingsPerSlot, MinLeadTimeHours, MaxAdvanceDays)
            VALUES (@BranchId, @OpenTime, @CloseTime, @SlotDurationMinutes, @WorkingDaysMask, @MaxBookingsPerSlot, @MinLeadTimeHours, @MaxAdvanceDays);
            {Dialect.SelectInsertedId}
            """;

        var row = SlotRuleRow.FromDomain(rule);
        await using var db = await Connections.OpenAsync(cancellationToken);
        var affected = await db.ExecuteAsync(new CommandDefinition(update, row, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            rule.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(insert, row, cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<FeeScheduleItem>> GetFeeScheduleAsync(int branchId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM FeeSchedule WHERE BranchId = @BranchId ORDER BY ServiceCode, EffectiveFrom DESC";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<FeeScheduleItem>(new CommandDefinition(sql, new { BranchId = branchId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> AddFeeScheduleItemAsync(FeeScheduleItem item, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO FeeSchedule (BranchId, ServiceCode, Description, Amount, IsActive, EffectiveFrom)
            VALUES (@BranchId, @ServiceCode, @Description, @Amount, @IsActive, @EffectiveFrom);
            {Dialect.SelectInsertedId}
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        item.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, item, cancellationToken: cancellationToken));
        return item.Id;
    }

    /// <summary>Persistence shape of a slot rule: times are stored as HH:mm text for cross-provider parity.</summary>
    private sealed class SlotRuleRow
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string OpenTime { get; set; } = "08:00";
        public string CloseTime { get; set; } = "17:00";
        public int SlotDurationMinutes { get; set; }
        public int WorkingDaysMask { get; set; }
        public int MaxBookingsPerSlot { get; set; }
        public int MinLeadTimeHours { get; set; }
        public int MaxAdvanceDays { get; set; }

        public BranchSlotRule ToDomain() => new()
        {
            Id = Id,
            BranchId = BranchId,
            OpenTime = TimeOnly.ParseExact(OpenTime, "HH:mm"),
            CloseTime = TimeOnly.ParseExact(CloseTime, "HH:mm"),
            SlotDurationMinutes = SlotDurationMinutes,
            WorkingDaysMask = WorkingDaysMask,
            MaxBookingsPerSlot = MaxBookingsPerSlot,
            MinLeadTimeHours = MinLeadTimeHours,
            MaxAdvanceDays = MaxAdvanceDays
        };

        public static SlotRuleRow FromDomain(BranchSlotRule rule) => new()
        {
            Id = rule.Id,
            BranchId = rule.BranchId,
            OpenTime = rule.OpenTime.ToString("HH:mm"),
            CloseTime = rule.CloseTime.ToString("HH:mm"),
            SlotDurationMinutes = rule.SlotDurationMinutes,
            WorkingDaysMask = rule.WorkingDaysMask,
            MaxBookingsPerSlot = rule.MaxBookingsPerSlot,
            MinLeadTimeHours = rule.MinLeadTimeHours,
            MaxAdvanceDays = rule.MaxAdvanceDays
        };
    }
}
