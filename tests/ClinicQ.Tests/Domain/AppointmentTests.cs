using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Exceptions;

namespace ClinicQ.Tests.Domain;

public class AppointmentTests
{
    private static readonly DateTime Start = new(2026, 3, 10, 9, 0, 0);

    private static Appointment NewAppointment() => new()
    {
        Id = 1,
        PatientId = 1,
        DoctorId = 1,
        BranchId = 1,
        ScheduledStart = Start,
        ScheduledEnd = Start.AddMinutes(30),
        Reason = "Annual physical",
        CreatedAt = Start.AddDays(-3)
    };

    [Fact]
    public void Lifecycle_records_a_timestamp_at_every_step()
    {
        var a = NewAppointment();

        a.Confirm(Start.AddDays(-2));
        a.CheckIn(Start.AddMinutes(-5));
        a.StartConsultation(Start.AddMinutes(7));
        a.Complete(Start.AddMinutes(25), "Reviewed blood pressure.");

        Assert.Equal(AppointmentStatus.Completed, a.Status);
        Assert.Equal(Start.AddDays(-2), a.ConfirmedAt);
        Assert.Equal(Start.AddMinutes(-5), a.CheckedInAt);
        Assert.Equal(Start.AddMinutes(7), a.ConsultationStartedAt);
        Assert.Equal(Start.AddMinutes(25), a.CompletedAt);
        Assert.Equal("Reviewed blood pressure.", a.Notes);
    }

    [Fact]
    public void WaitMinutes_measures_check_in_to_consultation()
    {
        var a = NewAppointment();
        Assert.Null(a.WaitMinutes);

        a.Confirm(Start.AddDays(-1));
        a.CheckIn(Start.AddMinutes(-10));
        Assert.Null(a.WaitMinutes); // still waiting

        a.StartConsultation(Start.AddMinutes(2));
        Assert.Equal(12, a.WaitMinutes!.Value, 3);
    }

    [Fact]
    public void Skipping_a_step_throws_and_leaves_the_status_untouched()
    {
        var a = NewAppointment();

        Assert.Throws<InvalidAppointmentTransitionException>(() => a.CheckIn(Start));
        Assert.Equal(AppointmentStatus.Requested, a.Status);
        Assert.Null(a.CheckedInAt);
    }

    [Fact]
    public void Cancel_requires_a_reason()
    {
        var a = NewAppointment();

        var ex = Assert.Throws<DomainException>(() => a.Cancel(Start.AddDays(-1), "   "));
        Assert.Contains("reason is required", ex.Message);
        Assert.Equal(AppointmentStatus.Requested, a.Status);
    }

    [Fact]
    public void Cancel_stores_a_trimmed_reason()
    {
        var a = NewAppointment();
        a.Cancel(Start.AddDays(-1), "  Patient travelling  ");

        Assert.Equal(AppointmentStatus.Cancelled, a.Status);
        Assert.Equal("Patient travelling", a.CancellationReason);
        Assert.Equal(Start.AddDays(-1), a.CancelledAt);
    }

    [Fact]
    public void A_completed_appointment_cannot_be_cancelled()
    {
        var a = NewAppointment();
        a.Confirm(Start.AddDays(-1));
        a.CheckIn(Start);
        a.StartConsultation(Start.AddMinutes(5));
        a.Complete(Start.AddMinutes(20));

        Assert.Throws<InvalidAppointmentTransitionException>(() => a.Cancel(Start.AddMinutes(30), "changed mind"));
    }

    [Fact]
    public void NoShow_is_rejected_before_the_scheduled_start()
    {
        var a = NewAppointment();
        a.Confirm(Start.AddDays(-1));

        var ex = Assert.Throws<DomainException>(() => a.MarkNoShow(Start.AddMinutes(-1)));
        Assert.Contains("before its scheduled start", ex.Message);
        Assert.Equal(AppointmentStatus.Confirmed, a.Status);
    }

    [Fact]
    public void NoShow_is_allowed_once_the_slot_has_started()
    {
        var a = NewAppointment();
        a.Confirm(Start.AddDays(-1));
        a.MarkNoShow(Start.AddMinutes(15));

        Assert.Equal(AppointmentStatus.NoShow, a.Status);
    }
}
