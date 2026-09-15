/*
    ClinicQ - stored procedures (SQL Server)
    The SQLite fallback replaces each procedure with equivalent inline SQL
    inside the *Sqlite* repository implementations in src/ClinicQ.Web/Data.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =========================================================================
   usp_GetAvailableSlots
   Expands a branch's slot rule into concrete slots for one doctor and day and
   returns the remaining capacity per slot. Mirrors Domain.Scheduling.SlotGenerator.
   ========================================================================= */
CREATE OR ALTER PROCEDURE dbo.usp_GetAvailableSlots
    @BranchId INT,
    @DoctorId INT,
    @Date     DATE,
    @Now      DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OpenTime  NVARCHAR(5), @CloseTime NVARCHAR(5),
            @Duration  INT, @Mask INT, @MaxPerSlot INT, @LeadHours INT, @AdvanceDays INT;

    SELECT @OpenTime = OpenTime, @CloseTime = CloseTime, @Duration = SlotDurationMinutes,
           @Mask = WorkingDaysMask, @MaxPerSlot = MaxBookingsPerSlot,
           @LeadHours = MinLeadTimeHours, @AdvanceDays = MaxAdvanceDays
    FROM dbo.BranchSlotRules
    WHERE BranchId = @BranchId;

    IF @OpenTime IS NULL
    BEGIN
        RAISERROR('No slot rule configured for branch %d.', 16, 1, @BranchId);
        RETURN;
    END

    /* DATEPART(WEEKDAY) depends on @@DATEFIRST; normalise so Monday = 1 ... Sunday = 7 */
    DECLARE @DayNumber INT = ((DATEPART(WEEKDAY, @Date) + @@DATEFIRST - 2) % 7) + 1;
    DECLARE @DayFlag INT = POWER(2, @DayNumber - 1);

    IF (@Mask & @DayFlag) = 0 OR @Date > DATEADD(DAY, @AdvanceDays, CAST(@Now AS DATE))
    BEGIN
        SELECT TOP (0) CAST(NULL AS DATETIME2(0)) AS SlotStart, CAST(NULL AS DATETIME2(0)) AS SlotEnd,
               CAST(NULL AS INT) AS Capacity, CAST(NULL AS INT) AS Booked;
        RETURN;
    END

    DECLARE @DayOpen  DATETIME2(0) = DATEADD(MINUTE, DATEDIFF(MINUTE, '00:00', CAST(@OpenTime  + ':00' AS TIME)), CAST(@Date AS DATETIME2(0)));
    DECLARE @DayClose DATETIME2(0) = DATEADD(MINUTE, DATEDIFF(MINUTE, '00:00', CAST(@CloseTime + ':00' AS TIME)), CAST(@Date AS DATETIME2(0)));
    DECLARE @Earliest DATETIME2(0) = DATEADD(HOUR, @LeadHours, @Now);

    ;WITH Slots AS
    (
        SELECT @DayOpen AS SlotStart, DATEADD(MINUTE, @Duration, @DayOpen) AS SlotEnd
        UNION ALL
        SELECT DATEADD(MINUTE, @Duration, SlotStart), DATEADD(MINUTE, @Duration, SlotEnd)
        FROM Slots
        WHERE DATEADD(MINUTE, @Duration, SlotEnd) <= @DayClose
    )
    SELECT s.SlotStart,
           s.SlotEnd,
           @MaxPerSlot AS Capacity,
           COUNT(a.Id) AS Booked
    FROM Slots s
    LEFT JOIN dbo.Appointments a
           ON a.DoctorId = @DoctorId
          AND a.ScheduledStart = s.SlotStart
          AND a.Status NOT IN ('Cancelled', 'NoShow')
    WHERE s.SlotStart >= @Earliest
    GROUP BY s.SlotStart, s.SlotEnd
    ORDER BY s.SlotStart
    OPTION (MAXRECURSION 500);
END
GO

/* =========================================================================
   usp_ReconcileBilling
   Nightly job: totals invoiced/paid for a branch and period, counts completed
   appointments that never produced an invoice, records a run row and returns it.
   ========================================================================= */
CREATE OR ALTER PROCEDURE dbo.usp_ReconcileBilling
    @BranchId    INT,
    @PeriodStart DATETIME2(0),
    @PeriodEnd   DATETIME2(0),
    @RunAt       DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @InvoiceCount INT, @TotalInvoiced DECIMAL(12,2), @TotalPaid DECIMAL(12,2), @Unbilled INT;

    SELECT @InvoiceCount  = COUNT(*),
           @TotalInvoiced = ISNULL(SUM(Total), 0),
           @TotalPaid     = ISNULL(SUM(AmountPaid), 0)
    FROM dbo.Invoices
    WHERE BranchId = @BranchId
      AND Status <> 'Void'
      AND IssuedAt >= @PeriodStart
      AND IssuedAt <  @PeriodEnd;

    SELECT @Unbilled = COUNT(*)
    FROM dbo.Appointments a
    WHERE a.BranchId = @BranchId
      AND a.Status = 'Completed'
      AND a.CompletedAt >= @PeriodStart
      AND a.CompletedAt <  @PeriodEnd
      AND NOT EXISTS (SELECT 1 FROM dbo.Invoices i WHERE i.AppointmentId = a.Id AND i.Status <> 'Void');

    DECLARE @Notes NVARCHAR(500) =
        CASE WHEN @Unbilled > 0 THEN CONCAT(@Unbilled, ' completed appointment(s) have no invoice.') ELSE 'All completed appointments are billed.' END;

    INSERT INTO dbo.ReconciliationRuns
        (BranchId, RunAt, PeriodStart, PeriodEnd, InvoiceCount, TotalInvoiced, TotalPaid, Outstanding, UnbilledCompletedAppointments, Notes)
    VALUES
        (@BranchId, @RunAt, @PeriodStart, @PeriodEnd, @InvoiceCount, @TotalInvoiced, @TotalPaid, @TotalInvoiced - @TotalPaid, @Unbilled, @Notes);

    SELECT Id, BranchId, RunAt, PeriodStart, PeriodEnd, InvoiceCount, TotalInvoiced, TotalPaid, Outstanding,
           UnbilledCompletedAppointments, Notes
    FROM dbo.ReconciliationRuns
    WHERE Id = SCOPE_IDENTITY();
END
GO

/* =========================================================================
   usp_GetBranchRevenue
   Revenue per branch for the dashboard (paid amounts within the period).
   ========================================================================= */
CREATE OR ALTER PROCEDURE dbo.usp_GetBranchRevenue
    @From DATETIME2(0),
    @To   DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT b.Id   AS BranchId,
           b.Name AS BranchName,
           COUNT(i.Id)                 AS InvoiceCount,
           ISNULL(SUM(i.Total), 0)     AS TotalInvoiced,
           ISNULL(SUM(i.AmountPaid), 0) AS TotalCollected
    FROM dbo.Branches b
    LEFT JOIN dbo.Invoices i
           ON i.BranchId = b.Id
          AND i.Status <> 'Void'
          AND i.IssuedAt >= @From
          AND i.IssuedAt <  @To
    GROUP BY b.Id, b.Name
    ORDER BY b.Name;
END
GO
