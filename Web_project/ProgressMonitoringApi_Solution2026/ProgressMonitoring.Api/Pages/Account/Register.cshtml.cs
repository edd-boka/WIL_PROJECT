using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly AppDbContext _db;

    public RegisterModel(AppDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    [Required(ErrorMessage = "First name is required.")]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Last name is required.")]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Staff ID is required.")]
    [MaxLength(5)]
    [RegularExpression("^[0-9]{5}$", ErrorMessage = "Enter the 5-digit staff ID provided by your school.")]
    public string StaffId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Email = Email.Trim();
        StaffId = StaffId.Trim();
        if (StaffId.Length == 0)
        {
            ModelState.AddModelError(nameof(StaffId), "Staff ID is required.");
            return Page();
        }

        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == Email.ToLower()))
        {
            ErrorMessage = "An account with that email already exists.";
            return Page();
        }

        var teacher = await _db.Teachers.FirstOrDefaultAsync(item => item.StaffId == StaffId);
        if (teacher?.UserId is not null)
        {
            ErrorMessage = "That staff ID is already linked to an account.";
            return Page();
        }

        if (teacher?.Email is not null &&
            !string.Equals(teacher.Email, Email, StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Use the email address assigned to this staff ID.";
            return Page();
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),   // keep whatever hashing your code already does
            Role = "teacher"
        };
        _db.Users.Add(user);

        if (teacher is null)
        {
            teacher = new Teacher
            {
                Id = Guid.NewGuid(),
                StaffId = StaffId,
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim()
            };
            _db.Teachers.Add(teacher);
        }

        teacher.UserId = user.Id;
        teacher.Email = Email;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            });

        return RedirectToPage("/Index");
    }
}
