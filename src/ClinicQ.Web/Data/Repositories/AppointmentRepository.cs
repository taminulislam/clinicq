using ClinicQ.Domain.Appointments;
using ClinicQ.Web.Data.Models;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

public sealed class AppointmentRepository : RepositoryBase, IAppointmentRepository
{
    private const string ListSelect = """
        SELECT a.Id, a.PatientId, p.FirstName || ' ' || p.LastName AS PatientName, p.Mrn AS PatientMrn,
               a.DoctorId, d.FullName AS DoctorName, a.BranchId, b.Name AS BranchName,
               a.ScheduledStart, a.ScheduledEnd, a.Status, a.Reason
          FROM Appointments a
          JOIN Patients p ON p.Id = a.PatientId
          JOIN Doctors  d ON d.Id = a.DoctorId
          JOIN Branches b ON b.Id = a.BranchId
        """;

    public AppointmentRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Appointments WHERE Id = @Id";
        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.QuerySingleOrDefaultAsync<Appointment>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<AppointmentListItem?> GetListItemAsync(int id, CancellationToken cancellationToken = default)
    {
        var sql = ConcatFriendly(ListSelect) + " WHERE a.Id = @Id";
        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.QuerySingleOrDefaultAsync<AppointmentListItem>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AppointmentListItem>> ListAsync(AppointmentFilter filter, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            {ConcatFriendly(ListSelect)}
            WHERE (@BranchId IS NULL OR a.BranchId = @BranchId)
              AND (@DoctorId IS NULL OR a.DoctorId = @DoctorId)
              AND (@PatientId IS NULL OR a.PatientId = @PatientId)
              AND (@Status IS NULL OR a.Status = @Status)
              AND (@From IS NULL OR a.ScheduledStart >= @From)
              AND (@To IS NULL OR a.ScheduledStart < @To)
            ORDER BY a.ScheduledStart DESC
            {Dialect.Paging}
            """;
        var args = new
        {
            filter.BranchId,
            filter.DoctorId,
            filter.PatientId,
            Status = filter.Status?.ToString(),
            filter.From,
            filter.To,
            filter.Offset,
            filter.Limit
        };
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<AppointmentListItem>(new CommandDefinition(sql, args, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> CreateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO Appointments (PatientId, DoctorId, BranchId, ScheduledStart, ScheduledEnd, Status, Reason, Notes, CreatedAt,
                                      ConfirmedAt, CheckedInAt, ConsultationStartedAt, CompletedAt, CancelledAt, CancellationReason)
            VALUES (@PatientId, @DoctorId, @BranchId, @ScheduledStart, @ScheduledEnd, @Status, @Reason, @Notes, @CreatedAt,
                    @ConfirmedAt, @CheckedInAt, @ConsultationStartedAt, @CompletedAt, @CancelledAt, @CancellationReason);
            {Dialect.SelectInsertedId}
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        appointment.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(sql, ToParams(appointment), cancellationToken: cancellationToken));
        return appointment.Id;
    }

    public async Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Appointments
               SET PatientId = @PatientId, DoctorId = @DoctorId, BranchId = @BranchId,
                   ScheduledStart = @ScheduledStart, ScheduledEnd = @ScheduledEnd, Status = @Status,
                   Reason = @Reason, Notes = @Notes, ConfirmedAt = @ConfirmedAt, CheckedInAt = @CheckedInAt,
                   ConsultationStartedAt = @ConsultationStartedAt, CompletedAt = @CompletedAt,
                   CancelledAt = @CancelledAt, CancellationReason = @CancellationReason
             WHERE Id = @Id
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        await db.ExecuteAsync(new CommandDefinition(sql, ToParams(appointment), cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<DateTime, int>> GetBookedCountsAsync(int doctorId, DateOnly date, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ScheduledStart AS SlotStart, COUNT(*) AS Booked
              FROM Appointments
             WHERE DoctorId = @DoctorId
               AND ScheduledStart >= @DayStart AND ScheduledStart < @DayEnd
               AND Status NOT IN ('Cancelled', 'NoShow')
             GROUP BY ScheduledStart
            """;
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var args = new { DoctorId = doctorId, DayStart = dayStart, DayEnd = dayStart.AddDays(1) };
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<BookedRow>(new CommandDefinition(sql, args, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.SlotStart, r => r.Booked);
    }

    private sealed class BookedRow
    {
        public DateTime SlotStart { get; set; }
        public int Booked { get; set; }
    }

    public async Task<IReadOnlyList<Appointment>> ListByStatusInWindowAsync(AppointmentStatus status, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM Appointments
             WHERE Status = @Status AND ScheduledStart >= @From AND ScheduledStart < @To
             ORDER BY ScheduledStart
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<Appointment>(new CommandDefinition(sql, new { Status = status.ToString(), From = from, To = to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Appointment>> ListCompletedBetweenAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM Appointments
             WHERE Status = 'Completed' AND CompletedAt >= @From AND CompletedAt < @To
             ORDER BY CompletedAt
            """;
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<Appointment>(new CommandDefinition(sql, new { From = from, To = to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    /// <summary>SQL Server has no || operator; rewrite the name concatenation for that provider.</summary>
    private string ConcatFriendly(string sql)
        => Connections.Provider == DatabaseProvider.SqlServer
            ? sql.Replace("p.FirstName || ' ' || p.LastName", "p.FirstName + ' ' + p.LastName")
            : sql;

    private static object ToParams(Appointment a) => new
    {
        a.Id,
        a.PatientId,
        a.DoctorId,
        a.BranchId,
        a.ScheduledStart,
        a.ScheduledEnd,
        Status = a.Status.ToString(),
        a.Reason,
        a.Notes,
        a.CreatedAt,
        a.ConfirmedAt,
        a.CheckedInAt,
        a.ConsultationStartedAt,
        a.CompletedAt,
        a.CancelledAt,
        a.CancellationReason
    };
}
