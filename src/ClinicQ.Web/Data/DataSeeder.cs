using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Web.Data.Models;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Security;

namespace ClinicQ.Web.Data;

/// <summary>
/// Seeds demo data on first start. Reference data mirrors db/002_seed.sql; appointments, invoices and
/// prescriptions are generated relative to "today" so the dashboard and calendar are always populated.
/// </summary>
public sealed class DataSeeder
{
    public const string DemoPassword = "Passw0rd!";

    private readonly IBranchRepository _branches;
    private readonly IDoctorRepository _doctors;
    private readonly IPatientRepository _patients;
    private readonly IAppointmentRepository _appointments;
    private readonly IInvoiceRepository _invoices;
    private readonly IPrescriptionRepository _prescriptions;
    private readonly IUserRepository _users;
    private readonly IClock _clock;
    private readonly ILogger<DataSeeder> _logger;
    private readonly FeeCalculator _feeCalculator = new();
    private readonly SlotGenerator _slotGenerator = new();
    private readonly Random _rng = new(20240915);

    public DataSeeder(
        IBranchRepository branches,
        IDoctorRepository doctors,
        IPatientRepository patients,
        IAppointmentRepository appointments,
        IInvoiceRepository invoices,
        IPrescriptionRepository prescriptions,
        IUserRepository users,
        IClock clock,
        ILogger<DataSeeder> logger)
    {
        _branches = branches;
        _doctors = doctors;
        _patients = patients;
        _appointments = appointments;
        _invoices = invoices;
        _prescriptions = prescriptions;
        _users = users;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Seeds reference data (branches, rules, fees, doctors, users, patients) when the database is empty and,
    /// when <paramref name="includeDemoActivity"/> is true, a generated appointment/billing history.
    /// </summary>
    public async Task SeedAsync(bool includeDemoActivity = true, CancellationToken cancellationToken = default)
    {
        var branches = await _branches.GetAllAsync(cancellationToken);
        if (branches.Count == 0)
        {
            await SeedReferenceDataAsync(cancellationToken);
            branches = await _branches.GetAllAsync(cancellationToken);
        }

        if (!includeDemoActivity)
        {
            return;
        }

        var existing = await _appointments.ListAsync(new AppointmentFilter { Limit = 1 }, cancellationToken);
        if (existing.Count > 0)
        {
            _logger.LogInformation("Seed skipped: database already contains appointments");
            return;
        }

        await SeedAppointmentsAsync(branches, cancellationToken);
    }

    private async Task SeedReferenceDataAsync(CancellationToken ct)
    {
        _logger.LogInformation("Seeding reference data");

        var spi = new Branch { Code = "SPI", Name = "Springfield Clinic", AddressLine = "410 E Monroe St", City = "Springfield", State = "IL", PostalCode = "62701", Phone = "(217) 555-0140", TaxRatePercent = 0m };
        var chi = new Branch { Code = "CHI", Name = "Chicago Loop Clinic", AddressLine = "120 S Riverside Pl", City = "Chicago", State = "IL", PostalCode = "60606", Phone = "(312) 555-0188", TaxRatePercent = 1.25m };
        var peo = new Branch { Code = "PEO", Name = "Peoria Clinic", AddressLine = "530 NE Glen Oak Ave", City = "Peoria", State = "IL", PostalCode = "61637", Phone = "(309) 555-0112", TaxRatePercent = 0m };
        foreach (var b in new[] { spi, chi, peo })
        {
            await _branches.CreateAsync(b, ct);
        }

        await _branches.UpsertSlotRuleAsync(new BranchSlotRule { BranchId = spi.Id, OpenTime = new TimeOnly(8, 0), CloseTime = new TimeOnly(17, 0), SlotDurationMinutes = 30, WorkingDaysMask = WorkingDays.MondayToFriday, MaxBookingsPerSlot = 1, MinLeadTimeHours = 1, MaxAdvanceDays = 60 }, ct);
        await _branches.UpsertSlotRuleAsync(new BranchSlotRule { BranchId = chi.Id, OpenTime = new TimeOnly(7, 30), CloseTime = new TimeOnly(18, 30), SlotDurationMinutes = 20, WorkingDaysMask = WorkingDays.MondayToSaturday, MaxBookingsPerSlot = 2, MinLeadTimeHours = 2, MaxAdvanceDays = 90 }, ct);
        await _branches.UpsertSlotRuleAsync(new BranchSlotRule { BranchId = peo.Id, OpenTime = new TimeOnly(9, 0), CloseTime = new TimeOnly(16, 0), SlotDurationMinutes = 30, WorkingDaysMask = WorkingDays.MondayToFriday, MaxBookingsPerSlot = 1, MinLeadTimeHours = 1, MaxAdvanceDays = 45 }, ct);

        var doctors = new[]
        {
            new Doctor { BranchId = spi.Id, FullName = "Dr. Anita Patel", Specialty = "Family Medicine", Email = "anita.patel@clinicq.example", LicenseNumber = "IL-MD-104422" },
            new Doctor { BranchId = spi.Id, FullName = "Dr. Marcus Whitfield", Specialty = "Internal Medicine", Email = "marcus.whitfield@clinicq.example", LicenseNumber = "IL-MD-118730" },
            new Doctor { BranchId = chi.Id, FullName = "Dr. Elena Rossi", Specialty = "Pediatrics", Email = "elena.rossi@clinicq.example", LicenseNumber = "IL-MD-120915" },
            new Doctor { BranchId = chi.Id, FullName = "Dr. Samuel Okafor", Specialty = "Cardiology", Email = "samuel.okafor@clinicq.example", LicenseNumber = "IL-MD-098211" },
            new Doctor { BranchId = chi.Id, FullName = "Dr. Grace Lindqvist", Specialty = "Dermatology", Email = "grace.lindqvist@clinicq.example", LicenseNumber = "IL-MD-131004" },
            new Doctor { BranchId = peo.Id, FullName = "Dr. Daniel Nguyen", Specialty = "Family Medicine", Email = "daniel.nguyen@clinicq.example", LicenseNumber = "IL-MD-115688" }
        };
        foreach (var d in doctors)
        {
            await _doctors.CreateAsync(d, ct);
        }

        var services = new (string Code, string Description, decimal Amount)[]
        {
            ("CONSULT", "New patient consultation", 120m),
            ("FOLLOWUP", "Follow-up visit", 75m),
            ("LAB-CBC", "Complete blood count", 45m),
            ("LAB-LIPID", "Lipid panel", 65m),
            ("XRAY", "X-ray, single view", 150m),
            ("VACCINE", "Vaccination administration", 60m),
            ("ECG", "Electrocardiogram", 95m)
        };
        var multipliers = new Dictionary<int, decimal> { [spi.Id] = 1.00m, [chi.Id] = 1.20m, [peo.Id] = 0.95m };
        foreach (var (branchId, multiplier) in multipliers)
        {
            foreach (var (code, description, amount) in services)
            {
                await _branches.AddFeeScheduleItemAsync(new FeeScheduleItem
                {
                    BranchId = branchId,
                    ServiceCode = code,
                    Description = description,
                    Amount = FeeCalculator.Round(amount * multiplier),
                    IsActive = true,
                    EffectiveFrom = new DateTime(2024, 1, 1)
                }, ct);
            }
        }

        var users = new[]
        {
            new AppUser { Username = "admin", DisplayName = "System Administrator", Email = "admin@clinicq.example", Role = Roles.Admin },
            new AppUser { Username = "reception", DisplayName = "Front Desk", Email = "reception@clinicq.example", Role = Roles.Receptionist, BranchId = spi.Id },
            new AppUser { Username = "drpatel", DisplayName = "Dr. Anita Patel", Email = "anita.patel@clinicq.example", Role = Roles.Doctor, BranchId = spi.Id },
            new AppUser { Username = "billing", DisplayName = "Billing Office", Email = "billing@clinicq.example", Role = Roles.Billing }
        };
        foreach (var u in users)
        {
            u.PasswordHash = PasswordHasher.Hash(DemoPassword);
            await _users.CreateAsync(u, ct);
        }

        var now = _clock.Now;
        var patients = new[]
        {
            new Patient { Mrn = "MRN-100001", FirstName = "Olivia", LastName = "Bennett", DateOfBirth = new DateTime(1986, 3, 14), Gender = "Female", Email = "olivia.bennett@example.com", Phone = "(217) 555-0101", AddressLine = "12 Oak St", City = "Springfield", State = "IL", PostalCode = "62704", Allergies = "Penicillin" },
            new Patient { Mrn = "MRN-100002", FirstName = "Liam", LastName = "Carter", DateOfBirth = new DateTime(1979, 11, 2), Gender = "Male", Email = "liam.carter@example.com", Phone = "(217) 555-0102", AddressLine = "88 Maple Ave", City = "Springfield", State = "IL", PostalCode = "62702" },
            new Patient { Mrn = "MRN-100003", FirstName = "Sophia", LastName = "Diaz", DateOfBirth = new DateTime(2016, 6, 21), Gender = "Female", Email = "sophia.diaz@example.com", Phone = "(312) 555-0103", AddressLine = "400 W Lake St", City = "Chicago", State = "IL", PostalCode = "60606", Allergies = "Peanuts" },
            new Patient { Mrn = "MRN-100004", FirstName = "Noah", LastName = "Eriksen", DateOfBirth = new DateTime(1954, 1, 30), Gender = "Male", Email = "noah.eriksen@example.com", Phone = "(312) 555-0104", AddressLine = "9 Wells St", City = "Chicago", State = "IL", PostalCode = "60610" },
            new Patient { Mrn = "MRN-100005", FirstName = "Ava", LastName = "Foster", DateOfBirth = new DateTime(1992, 9, 9), Gender = "Female", Email = "ava.foster@example.com", Phone = "(309) 555-0105", AddressLine = "71 Main St", City = "Peoria", State = "IL", PostalCode = "61602", Allergies = "Latex" },
            new Patient { Mrn = "MRN-100006", FirstName = "Ethan", LastName = "Garcia", DateOfBirth = new DateTime(2001, 4, 17), Gender = "Male", Email = "ethan.garcia@example.com", Phone = "(309) 555-0106", AddressLine = "23 River Rd", City = "Peoria", State = "IL", PostalCode = "61603" },
            new Patient { Mrn = "MRN-100007", FirstName = "Mia", LastName = "Hoffmann", DateOfBirth = new DateTime(1948, 12, 5), Gender = "Female", Email = "mia.hoffmann@example.com", Phone = "(217) 555-0107", AddressLine = "5 Lincoln Pl", City = "Springfield", State = "IL", PostalCode = "62703", Allergies = "Sulfa drugs" },
            new Patient { Mrn = "MRN-100008", FirstName = "James", LastName = "Ibrahim", DateOfBirth = new DateTime(1988, 7, 23), Gender = "Male", Email = "james.ibrahim@example.com", Phone = "(312) 555-0108", AddressLine = "250 N State St", City = "Chicago", State = "IL", PostalCode = "60654" },
            new Patient { Mrn = "MRN-100009", FirstName = "Isabella", LastName = "Jensen", DateOfBirth = new DateTime(1995, 2, 11), Gender = "Female", Email = "isabella.jensen@example.com", Phone = "(217) 555-0109", AddressLine = "17 Elm Ct", City = "Springfield", State = "IL", PostalCode = "62704" },
            new Patient { Mrn = "MRN-100010", FirstName = "Lucas", LastName = "Kim", DateOfBirth = new DateTime(2013, 10, 28), Gender = "Male", Email = "lucas.kim@example.com", Phone = "(312) 555-0110", AddressLine = "60 E Erie St", City = "Chicago", State = "IL", PostalCode = "60611", Allergies = "Eggs" },
            new Patient { Mrn = "MRN-100011", FirstName = "Charlotte", LastName = "Lopez", DateOfBirth = new DateTime(1970, 5, 19), Gender = "Female", Email = "charlotte.lopez@example.com", Phone = "(309) 555-0111", AddressLine = "14 Prospect Rd", City = "Peoria", State = "IL", PostalCode = "61604" },
            new Patient { Mrn = "MRN-100012", FirstName = "Benjamin", LastName = "Murphy", DateOfBirth = new DateTime(1963, 8, 8), Gender = "Male", Email = "benjamin.murphy@example.com", Phone = "(217) 555-0112", AddressLine = "33 Capitol Ave", City = "Springfield", State = "IL", PostalCode = "62701", Allergies = "Aspirin" }
        };
        foreach (var p in patients)
        {
            p.CreatedAt = now.AddDays(-_rng.Next(60, 400));
            await _patients.CreateAsync(p, ct);
        }
    }

    private async Task SeedAppointmentsAsync(IReadOnlyList<Branch> branches, CancellationToken ct)
    {
        _logger.LogInformation("Seeding appointments, invoices and prescriptions");

        var now = _clock.Now;
        var today = DateOnly.FromDateTime(now);
        var patients = await _patients.SearchAsync(null, 0, 500, ct);
        if (patients.Count == 0)
        {
            return;
        }

        var reasons = new[] { "Annual physical", "Persistent cough", "Blood pressure review", "Skin rash", "Back pain", "Medication review", "Vaccination", "Chest discomfort", "Follow-up after lab work", "Headaches" };
        var created = 0;

        foreach (var branch in branches)
        {
            var rule = await _branches.GetSlotRuleAsync(branch.Id, ct);
            var doctors = await _doctors.GetAllAsync(branch.Id, ct);
            var fees = await _branches.GetFeeScheduleAsync(branch.Id, ct);
            if (rule is null || doctors.Count == 0 || fees.Count == 0)
            {
                continue;
            }

            for (var offset = -30; offset <= 14; offset++)
            {
                var day = today.AddDays(offset);
                if (!rule.IsWorkingDay(day.DayOfWeek))
                {
                    continue;
                }

                // Generate the full day's grid (lead-time disabled by pretending it is yesterday).
                var slots = _slotGenerator.Generate(rule, day, new Dictionary<DateTime, int>(), day.AddDays(-1).ToDateTime(TimeOnly.MinValue));
                if (slots.Count == 0)
                {
                    continue;
                }

                // Past days run at 55-85% of capacity; the future calendar thins out the further ahead it goes.
                var fill = offset <= 0
                    ? 0.55 + (_rng.NextDouble() * 0.30)
                    : Math.Max(0.08, 0.65 - (offset * 0.045));

                foreach (var doctor in doctors)
                {
                    var take = Math.Max(1, (int)Math.Round(slots.Count * fill));
                    var picks = slots.OrderBy(_ => _rng.Next()).Take(take).OrderBy(s => s.Start);
                    foreach (var slot in picks)
                    {
                        var patient = patients[_rng.Next(patients.Count)];
                        var appointment = new Appointment
                        {
                            PatientId = patient.Id,
                            DoctorId = doctor.Id,
                            BranchId = branch.Id,
                            ScheduledStart = slot.Start,
                            ScheduledEnd = slot.End,
                            Reason = reasons[_rng.Next(reasons.Length)],
                            CreatedAt = slot.Start.AddDays(-_rng.Next(1, 10))
                        };

                        var outcome = DecideOutcome(slot.Start, now);
                        ApplyOutcome(appointment, outcome, now);
                        await _appointments.CreateAsync(appointment, ct);
                        created++;

                        if (appointment.Status == AppointmentStatus.Completed)
                        {
                            await CreateInvoiceAsync(appointment, patient, branch, fees, ct);
                            if (_rng.NextDouble() < 0.5)
                            {
                                await CreatePrescriptionAsync(appointment, ct);
                            }
                        }
                    }
                }
            }
        }

        _logger.LogInformation("Seeded {Count} appointments", created);
    }

    private AppointmentStatus DecideOutcome(DateTime start, DateTime now)
    {
        if (start.Date < now.Date)
        {
            var roll = _rng.NextDouble();
            return roll < 0.78 ? AppointmentStatus.Completed
                 : roll < 0.88 ? AppointmentStatus.NoShow
                 : AppointmentStatus.Cancelled;
        }

        if (start.Date == now.Date)
        {
            if (start < now.AddMinutes(-45))
            {
                return AppointmentStatus.Completed;
            }

            if (start < now)
            {
                return _rng.NextDouble() < 0.5 ? AppointmentStatus.InConsultation : AppointmentStatus.CheckedIn;
            }

            return AppointmentStatus.Confirmed;
        }

        return _rng.NextDouble() < 0.7 ? AppointmentStatus.Confirmed : AppointmentStatus.Requested;
    }

    private void ApplyOutcome(Appointment appointment, AppointmentStatus outcome, DateTime now)
    {
        var start = appointment.ScheduledStart;
        if (outcome == AppointmentStatus.Requested)
        {
            return;
        }

        appointment.Confirm(appointment.CreatedAt.AddHours(1));
        switch (outcome)
        {
            case AppointmentStatus.Confirmed:
                return;
            case AppointmentStatus.Cancelled:
                appointment.Cancel(start.AddDays(-1), "Patient requested to reschedule");
                return;
            case AppointmentStatus.NoShow:
                appointment.MarkNoShow(start.AddMinutes(30));
                return;
        }

        var checkedIn = start.AddMinutes(-_rng.Next(0, 12));
        appointment.CheckIn(checkedIn);
        if (outcome == AppointmentStatus.CheckedIn)
        {
            return;
        }

        var consultStart = checkedIn.AddMinutes(_rng.Next(3, 30));
        appointment.StartConsultation(consultStart);
        if (outcome == AppointmentStatus.InConsultation)
        {
            return;
        }

        var completedAt = consultStart.AddMinutes(_rng.Next(10, 25));
        appointment.Complete(completedAt > now ? now : completedAt, "Examined, plan discussed with patient.");
    }

    private async Task CreateInvoiceAsync(Appointment appointment, Patient patient, Branch branch, IReadOnlyList<FeeScheduleItem> fees, CancellationToken ct)
    {
        var services = new List<ServiceLineRequest>
        {
            new(_rng.NextDouble() < 0.4 ? "CONSULT" : "FOLLOWUP")
        };
        if (_rng.NextDouble() < 0.35)
        {
            var extras = new[] { "LAB-CBC", "LAB-LIPID", "ECG", "XRAY", "VACCINE" };
            services.Add(new ServiceLineRequest(extras[_rng.Next(extras.Length)]));
        }

        var issuedAt = appointment.CompletedAt ?? appointment.ScheduledEnd;
        var discount = DiscountPolicy.ForPatientAge(patient.AgeOn(issuedAt));
        var calc = _feeCalculator.Calculate(fees, services, discount, branch.TaxRatePercent);
        var number = await _invoices.NextInvoiceNumberAsync(branch.Code, issuedAt, ct);
        var invoice = Invoice.FromCalculation(calc, appointment.Id, patient.Id, branch.Id, issuedAt, number);

        var payments = new List<Payment>();
        var roll = _rng.NextDouble();
        if (roll < 0.65)
        {
            payments.Add(NewPayment(invoice, invoice.Total, issuedAt.AddMinutes(15)));
        }
        else if (roll < 0.80)
        {
            payments.Add(NewPayment(invoice, FeeCalculator.Round(invoice.Total / 2), issuedAt.AddMinutes(15)));
        }

        foreach (var payment in payments)
        {
            invoice.ApplyPayment(payment.Amount, payment.PaidAt);
        }

        await _invoices.CreateAsync(invoice, ct);
        foreach (var payment in payments)
        {
            payment.InvoiceId = invoice.Id;
            await _invoices.AddPaymentAsync(payment, ct);
        }
    }

    private Payment NewPayment(Invoice invoice, decimal amount, DateTime paidAt)
    {
        var methods = new[] { PaymentMethod.Card, PaymentMethod.Cash, PaymentMethod.Insurance, PaymentMethod.Card };
        return new Payment
        {
            InvoiceId = invoice.Id,
            Amount = amount,
            Method = methods[_rng.Next(methods.Length)],
            Reference = $"RCPT-{_rng.Next(100000, 999999)}",
            PaidAt = paidAt,
            ReceivedBy = "reception"
        };
    }

    private async Task CreatePrescriptionAsync(Appointment appointment, CancellationToken ct)
    {
        var catalog = new (string Med, string Dose, string Freq, int Days)[]
        {
            ("Amoxicillin", "500 mg", "Three times daily", 7),
            ("Lisinopril", "10 mg", "Once daily", 30),
            ("Ibuprofen", "400 mg", "Every 8 hours as needed", 5),
            ("Cetirizine", "10 mg", "Once daily", 14),
            ("Metformin", "500 mg", "Twice daily with meals", 30),
            ("Omeprazole", "20 mg", "Once daily before breakfast", 14)
        };

        var prescription = new Prescription
        {
            AppointmentId = appointment.Id,
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            IssuedAt = appointment.CompletedAt ?? appointment.ScheduledEnd,
            Diagnosis = appointment.Reason,
            Instructions = "Take with water. Return if symptoms persist beyond the course."
        };

        foreach (var pick in catalog.OrderBy(_ => _rng.Next()).Take(_rng.Next(1, 3)))
        {
            prescription.Items.Add(new PrescriptionItem { Medication = pick.Med, Dosage = pick.Dose, Frequency = pick.Freq, DurationDays = pick.Days });
        }

        await _prescriptions.CreateAsync(prescription, ct);
    }
}
