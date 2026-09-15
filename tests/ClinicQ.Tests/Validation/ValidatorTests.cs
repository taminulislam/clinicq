using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Api.Validators;
using ClinicQ.Domain.Scheduling;

namespace ClinicQ.Tests.Validation;

public class ValidatorTests
{
    private static CreateAppointmentRequest ValidAppointment() => new()
    {
        PatientId = 1,
        DoctorId = 2,
        BranchId = 3,
        Start = new DateTime(2026, 4, 1, 9, 30, 0),
        Reason = "Follow-up"
    };

    private static CreatePatientRequest ValidPatient() => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        DateOfBirth = new DateTime(1990, 12, 10),
        Gender = "Female",
        Email = "ada@example.com",
        Phone = "(217) 555-0123"
    };

    [Fact]
    public void A_complete_appointment_request_passes()
        => Assert.True(new CreateAppointmentRequestValidator().Validate(ValidAppointment()).IsValid);

    [Theory]
    [InlineData(0, 2, 3, "Follow-up")]
    [InlineData(1, 0, 3, "Follow-up")]
    [InlineData(1, 2, 0, "Follow-up")]
    [InlineData(1, 2, 3, "")]
    public void Appointment_requests_need_ids_and_a_reason(int patientId, int doctorId, int branchId, string reason)
    {
        var request = ValidAppointment();
        request.PatientId = patientId;
        request.DoctorId = doctorId;
        request.BranchId = branchId;
        request.Reason = reason;

        Assert.False(new CreateAppointmentRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Appointment_start_must_be_supplied_and_a_whole_minute()
    {
        var missing = ValidAppointment();
        missing.Start = default;
        Assert.False(new CreateAppointmentRequestValidator().Validate(missing).IsValid);

        var seconds = ValidAppointment();
        seconds.Start = new DateTime(2026, 4, 1, 9, 30, 15);
        Assert.False(new CreateAppointmentRequestValidator().Validate(seconds).IsValid);
    }

    [Fact]
    public void Cancelling_requires_a_meaningful_reason()
    {
        var validator = new CancelAppointmentRequestValidator();

        Assert.False(validator.Validate(new CancelAppointmentRequest { Reason = "" }).IsValid);
        Assert.False(validator.Validate(new CancelAppointmentRequest { Reason = "no" }).IsValid);
        Assert.True(validator.Validate(new CancelAppointmentRequest { Reason = "Patient rescheduled" }).IsValid);
    }

    [Fact]
    public void A_complete_patient_passes()
        => Assert.True(new CreatePatientRequestValidator().Validate(ValidPatient()).IsValid);

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void Patient_email_must_be_valid(string email)
    {
        var patient = ValidPatient();
        patient.Email = email;

        var result = new CreatePatientRequestValidator().Validate(patient);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(patient.Email));
    }

    [Fact]
    public void Patient_date_of_birth_must_be_in_the_past_and_plausible()
    {
        var future = ValidPatient();
        future.DateOfBirth = DateTime.Today.AddDays(1);
        Assert.False(new CreatePatientRequestValidator().Validate(future).IsValid);

        var ancient = ValidPatient();
        ancient.DateOfBirth = DateTime.Today.AddYears(-131);
        Assert.False(new CreatePatientRequestValidator().Validate(ancient).IsValid);
    }

    [Theory]
    [InlineData("Female", true)]
    [InlineData("male", true)]
    [InlineData("Martian", false)]
    [InlineData("", false)]
    public void Patient_gender_comes_from_a_fixed_list(string gender, bool expected)
    {
        var patient = ValidPatient();
        patient.Gender = gender;

        Assert.Equal(expected, new CreatePatientRequestValidator().Validate(patient).IsValid);
    }

    [Fact]
    public void Patient_phone_rejects_letters()
    {
        var patient = ValidPatient();
        patient.Phone = "call me";

        Assert.False(new CreatePatientRequestValidator().Validate(patient).IsValid);
    }

    [Fact]
    public void Invoice_generation_needs_at_least_one_valid_service_line()
    {
        var validator = new GenerateInvoiceRequestValidator();

        Assert.False(validator.Validate(new GenerateInvoiceRequest { AppointmentId = 1 }).IsValid);
        Assert.False(validator.Validate(new GenerateInvoiceRequest
        {
            AppointmentId = 1,
            Services = { new ServiceLineDto { ServiceCode = "CONSULT", Quantity = 0 } }
        }).IsValid);
        Assert.True(validator.Validate(new GenerateInvoiceRequest
        {
            AppointmentId = 1,
            Services = { new ServiceLineDto { ServiceCode = "CONSULT", Quantity = 1 } }
        }).IsValid);
    }

    [Fact]
    public void Invoice_discount_override_must_be_a_percentage()
    {
        var validator = new GenerateInvoiceRequestValidator();
        var request = new GenerateInvoiceRequest { AppointmentId = 1, Services = { new ServiceLineDto { ServiceCode = "CONSULT" } } };

        request.DiscountPercent = 150m;
        Assert.False(validator.Validate(request).IsValid);

        request.DiscountPercent = 12.5m;
        Assert.True(validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(50, "Card", true)]
    [InlineData(50, "bankTransfer", true)]
    [InlineData(0, "Card", false)]
    [InlineData(-1, "Card", false)]
    [InlineData(50, "Bitcoin", false)]
    public void Payment_amount_and_method_are_checked(decimal amount, string method, bool expected)
    {
        var result = new RecordPaymentRequestValidator().Validate(new RecordPaymentRequest { Amount = amount, Method = method });

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public void Prescriptions_need_at_least_one_complete_medication()
    {
        var validator = new CreatePrescriptionRequestValidator();

        Assert.False(validator.Validate(new CreatePrescriptionRequest { AppointmentId = 1 }).IsValid);
        Assert.False(validator.Validate(new CreatePrescriptionRequest
        {
            AppointmentId = 1,
            Items = { new PrescriptionItemDto { Medication = "Amoxicillin" } } // missing dosage/frequency/days
        }).IsValid);
        Assert.True(validator.Validate(new CreatePrescriptionRequest
        {
            AppointmentId = 1,
            Items = { new PrescriptionItemDto { Medication = "Amoxicillin", Dosage = "500 mg", Frequency = "Three times daily", DurationDays = 7 } }
        }).IsValid);
    }

    [Fact]
    public void Slot_rules_must_close_after_they_open()
    {
        var validator = new UpdateSlotRuleRequestValidator();
        var rule = new UpdateSlotRuleRequest { OpenTime = "09:00", CloseTime = "17:00", SlotDurationMinutes = 30, WorkingDaysMask = WorkingDays.MondayToFriday };
        Assert.True(validator.Validate(rule).IsValid);

        rule.CloseTime = "08:00";
        Assert.False(validator.Validate(rule).IsValid);
    }

    [Theory]
    [InlineData("9:00")]
    [InlineData("25:00")]
    [InlineData("morning")]
    public void Slot_rule_times_must_be_hh_mm(string openTime)
    {
        var rule = new UpdateSlotRuleRequest { OpenTime = openTime, CloseTime = "17:00", SlotDurationMinutes = 30, WorkingDaysMask = WorkingDays.MondayToFriday };

        Assert.False(new UpdateSlotRuleRequestValidator().Validate(rule).IsValid);
    }

    [Theory]
    [InlineData(4)]     // below the 5-minute minimum
    [InlineData(300)]   // above the 240-minute maximum
    public void Slot_duration_has_sane_bounds(int minutes)
    {
        var rule = new UpdateSlotRuleRequest { OpenTime = "09:00", CloseTime = "17:00", SlotDurationMinutes = minutes, WorkingDaysMask = WorkingDays.MondayToFriday };

        Assert.False(new UpdateSlotRuleRequestValidator().Validate(rule).IsValid);
    }

    [Fact]
    public void Token_requests_need_a_username_and_a_long_enough_password()
    {
        var validator = new TokenRequestValidator();

        Assert.True(validator.Validate(new TokenRequest { Username = "admin", Password = "Passw0rd!" }).IsValid);
        Assert.False(validator.Validate(new TokenRequest { Username = "", Password = "Passw0rd!" }).IsValid);
        Assert.False(validator.Validate(new TokenRequest { Username = "admin", Password = "short" }).IsValid);
    }
}
