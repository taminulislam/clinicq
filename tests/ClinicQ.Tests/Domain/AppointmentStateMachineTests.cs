using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Exceptions;

namespace ClinicQ.Tests.Domain;

public class AppointmentStateMachineTests
{
    [Theory]
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.InConsultation)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.InConsultation, AppointmentStatus.Completed)]
    public void CanTransition_allows_the_documented_moves(AppointmentStatus from, AppointmentStatus to)
    {
        Assert.True(AppointmentStateMachine.CanTransition(from, to));
        AppointmentStateMachine.EnsureTransition(from, to);
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.CheckedIn)]      // must be confirmed first
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.NoShow)]         // only confirmed visits can no-show
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.InConsultation)] // must check in first
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.Completed)]      // consultation must start
    [InlineData(AppointmentStatus.InConsultation, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Confirmed)]      // terminal
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.CheckedIn)]
    public void CanTransition_rejects_everything_else(AppointmentStatus from, AppointmentStatus to)
    {
        Assert.False(AppointmentStateMachine.CanTransition(from, to));

        var ex = Assert.Throws<InvalidAppointmentTransitionException>(() => AppointmentStateMachine.EnsureTransition(from, to));
        Assert.Equal(from, ex.From);
        Assert.Equal(to, ex.To);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Terminal_states_have_no_successors(AppointmentStatus status)
    {
        Assert.True(AppointmentStateMachine.IsTerminal(status));
        Assert.Empty(AppointmentStateMachine.NextStates(status));
    }

    [Fact]
    public void Happy_path_walks_from_requested_to_completed()
    {
        var path = new[]
        {
            AppointmentStatus.Requested, AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn,
            AppointmentStatus.InConsultation, AppointmentStatus.Completed
        };

        for (var i = 0; i < path.Length - 1; i++)
        {
            Assert.True(AppointmentStateMachine.CanTransition(path[i], path[i + 1]), $"{path[i]} -> {path[i + 1]}");
        }

        Assert.False(AppointmentStateMachine.IsTerminal(AppointmentStatus.Requested));
    }

    [Fact]
    public void Every_status_is_reachable_from_the_graph_or_is_the_entry_point()
    {
        var reachable = Enum.GetValues<AppointmentStatus>()
            .SelectMany(AppointmentStateMachine.NextStates)
            .Distinct()
            .ToHashSet();
        reachable.Add(AppointmentStatus.Requested);

        Assert.Equal(Enum.GetValues<AppointmentStatus>().ToHashSet(), reachable);
    }
}
