using ClinicQ.Domain.Entities;
using Hangfire.Dashboard;

namespace ClinicQ.Web.Jobs;

/// <summary>
/// Only signed-in administrators may open /hangfire. Local requests are allowed in Development.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly bool _allowLocalRequests;

    public HangfireDashboardAuthorizationFilter(bool allowLocalRequests)
    {
        _allowLocalRequests = allowLocalRequests;
    }

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (http.User.Identity?.IsAuthenticated == true && http.User.IsInRole(Roles.Admin))
        {
            return true;
        }

        return _allowLocalRequests && context.Request.LocalIpAddress == context.Request.RemoteIpAddress;
    }
}
