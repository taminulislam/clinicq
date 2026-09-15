using ClinicQ.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Ui;

/// <summary>TempData flash messages rendered by Shared/_StatusMessage.cshtml.</summary>
public static class PageModelExtensions
{
    public const string SuccessKey = "StatusSuccess";
    public const string ErrorKey = "StatusError";

    public static void Flash(this PageModel page, string message) => page.TempData[SuccessKey] = message;

    public static void FlashError(this PageModel page, string message) => page.TempData[ErrorKey] = message;

    /// <summary>
    /// Runs a domain operation and converts business-rule violations into a flash error instead of a 500.
    /// Returns true when the action succeeded.
    /// </summary>
    public static async Task<bool> TryDomainAsync(this PageModel page, Func<Task> action, string? successMessage = null)
    {
        try
        {
            await action();
            if (successMessage is not null)
            {
                page.Flash(successMessage);
            }

            return true;
        }
        catch (DomainException ex)
        {
            page.FlashError(ex.Message);
            return false;
        }
    }
}
