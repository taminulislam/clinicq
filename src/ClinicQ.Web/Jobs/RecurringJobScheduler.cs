using Hangfire;

namespace ClinicQ.Web.Jobs;

/// <summary>
/// Registers the recurring Hangfire jobs. Cron expressions are evaluated in the clinic time zone.
/// </summary>
public static class RecurringJobScheduler
{
    public const string ReminderJobId = "appointment-reminders";
    public const string FollowUpJobId = "follow-up-alerts";
    public const string ReconciliationJobId = "nightly-billing-reconciliation";

    public static void Register(IRecurringJobManager manager, TimeZoneInfo timeZone)
    {
        var options = new RecurringJobOptions { TimeZone = timeZone };

        manager.AddOrUpdate<AppointmentReminderJob>(ReminderJobId, job => job.RunAsync(CancellationToken.None), Cron.Daily(8, 0), options);
        manager.AddOrUpdate<FollowUpAlertJob>(FollowUpJobId, job => job.RunAsync(CancellationToken.None), Cron.Daily(9, 30), options);
        manager.AddOrUpdate<BillingReconciliationJob>(ReconciliationJobId, job => job.RunAsync(CancellationToken.None), Cron.Daily(2, 0), options);
    }
}
