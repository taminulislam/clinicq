using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Account;

public sealed class LoginModel : PageModel
{
    private readonly IUserRepository _users;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(IUserRepository users, ILogger<LoginModel> logger)
    {
        _users = users;
        _logger = logger;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _users.GetByUsernameAsync(Input.Username, cancellationToken);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(Input.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed portal sign-in for {Username}", Input.Username);
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        if (user.BranchId.HasValue)
        {
            claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = Input.RememberMe });

        _logger.LogInformation("{Username} signed in to the portal", user.Username);
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }

    public sealed class LoginInput
    {
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }
    }
}
