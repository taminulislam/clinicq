using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Web.Data;
using ClinicQ.Web.Data.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClinicQ.Tests.Data;

/// <summary>
/// A private in-memory SQLite database created from the embedded schema script, with the repositories
/// under test wired on top. Each instance gets its own named shared-cache database.
/// </summary>
public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnectionFactory _factory;

    private SqliteTestDatabase()
    {
        _factory = new SqliteConnectionFactory($"Data Source=clinicq-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        Branches = new BranchRepository(_factory);
        Doctors = new DoctorRepository(_factory);
        Patients = new PatientRepository(_factory);
        Appointments = new AppointmentRepository(_factory);
        Invoices = new InvoiceRepository(_factory);
        Prescriptions = new PrescriptionRepository(_factory);
        LabReports = new LabReportRepository(_factory);
        Users = new UserRepository(_factory);
        Dashboard = new DashboardRepository(_factory);
        Reconciliation = new SqliteBillingReconciliationRepository(_factory);
        Slots = new SqliteSlotRepository(Branches, Appointments, new SlotGenerator());
    }

    public IBranchRepository Branches { get; }
    public IDoctorRepository Doctors { get; }
    public IPatientRepository Patients { get; }
    public IAppointmentRepository Appointments { get; }
    public IInvoiceRepository Invoices { get; }
    public IPrescriptionRepository Prescriptions { get; }
    public ILabReportRepository LabReports { get; }
    public IUserRepository Users { get; }
    public IDashboardRepository Dashboard { get; }
    public IBillingReconciliationRepository Reconciliation { get; }
    public ISlotRepository Slots { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var db = new SqliteTestDatabase();
        await new DatabaseInitializer(db._factory, NullLogger<DatabaseInitializer>.Instance).InitializeAsync();
        return db;
    }

    /// <summary>One branch (Mon-Fri, 09:00-12:00, 30 minute slots) with one doctor and a small fee schedule.</summary>
    public async Task<(Branch Branch, Doctor Doctor, Patient Patient)> SeedReferenceDataAsync(DateTime createdAt)
    {
        var branch = new Branch
        {
            Code = "TST",
            Name = "Test Clinic",
            AddressLine = "1 Test St",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Phone = "(217) 555-0100",
            TaxRatePercent = 10m
        };
        await Branches.CreateAsync(branch);

        await Branches.UpsertSlotRuleAsync(new BranchSlotRule
        {
            BranchId = branch.Id,
            OpenTime = new TimeOnly(9, 0),
            CloseTime = new TimeOnly(12, 0),
            SlotDurationMinutes = 30,
            WorkingDaysMask = WorkingDays.MondayToFriday,
            MaxBookingsPerSlot = 1,
            MinLeadTimeHours = 1,
            MaxAdvanceDays = 30
        });

        var doctor = new Doctor
        {
            BranchId = branch.Id,
            FullName = "Dr. Test Example",
            Specialty = "Family Medicine",
            Email = "test@clinicq.example",
            LicenseNumber = "IL-MD-000001"
        };
        await Doctors.CreateAsync(doctor);

        foreach (var (code, description, amount) in new[]
                 {
                     ("CONSULT", "Consultation", 120m),
                     ("FOLLOWUP", "Follow-up visit", 75m),
                     ("LAB-CBC", "Blood count", 45m)
                 })
        {
            await Branches.AddFeeScheduleItemAsync(new FeeScheduleItem
            {
                BranchId = branch.Id,
                ServiceCode = code,
                Description = description,
                Amount = amount,
                IsActive = true,
                EffectiveFrom = new DateTime(2024, 1, 1)
            });
        }

        var patient = new Patient
        {
            Mrn = await Patients.NextMrnAsync(),
            FirstName = "Ada",
            LastName = "Lovelace",
            DateOfBirth = new DateTime(1990, 12, 10),
            Gender = "Female",
            Email = "ada@example.com",
            Phone = "(217) 555-0123",
            CreatedAt = createdAt
        };
        await Patients.CreateAsync(patient);

        return (branch, doctor, patient);
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }
}
