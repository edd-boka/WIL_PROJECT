using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly SupabaseEmailOtp _otp;

    public AuthController(AppDbContext db, IConfiguration config, SupabaseEmailOtp otp)
    {
        _db = db;
        _config = config;
        _otp = otp;
    }

    public record LoginRequest(string Email, string Password);
    public record VerifyEmailRequest(string Email, string Code);
    public record ResendCodeRequest(string Email);
    public record RegisterRequest(string Email, string Password, string Role, string FirstName, string LastName);

    // POST /api/auth/login
    // Matches what lib/services/api_service.dart expects back:
    // { "token": "...", "role": "..." }
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // A parent must verify their email first; after that, every sign-in links any new children.
        var linkedChildren = 0;
        if (user.Role == "parent")
        {
            var parent = await _db.Parents.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (parent is not null)
            {
                if (!parent.EmailVerified)
                {
                    return StatusCode(403, new
                    {
                        message = "Please verify your email address with the code we sent you.",
                        requiresVerification = true
                    });
                }
                linkedChildren = await _db.LinkChildrenByEmailAsync(parent, user.Email);
            }
        }

        var token = GenerateJwtToken(user);
        return Ok(new { token, role = user.Role, newlyLinkedChildren = linkedChildren });
    }

    // POST /api/auth/register
    // Dev-only convenience endpoint for creating test accounts — it also
    // creates the matching Teacher or Parent profile row, since the
    // parent-learner access check in LearnersController depends on that
    // profile existing. Lock this down or remove it before real use.
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        // Anyone can sign up as a parent; teacher and admin accounts can only be created by an admin.
        var role = (request.Role ?? string.Empty).Trim().ToLowerInvariant();
        if (role != "parent" && !(User.Identity?.IsAuthenticated == true && User.IsInRole("admin")))
        {
            return StatusCode(403, new { message = "Only parent accounts can be created here." });
        }
        if (role != "parent" && role != "teacher" && role != "admin")
        {
            return BadRequest(new { message = "Role must be parent, teacher or admin." });
        }
        request = request with { Role = role, Email = (request.Email ?? string.Empty).Trim() };

        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
        {
            return Conflict(new { message = "A user with that email already exists." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
        };
        _db.Users.Add(user);

        Parent? newParent = null;
        if (request.Role == "teacher")
        {
            _db.Teachers.Add(new Teacher
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
            });
        }
        else if (request.Role == "parent")
        {
            newParent = new Parent
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmailVerified = false,   // linked to children only after the emailed code is entered
            };
            _db.Parents.Add(newParent);
        }
        // "admin" role has no separate profile table in this template.

        await _db.SaveChangesAsync();

        // A new parent gets a 6-digit code by email. Their children are linked when they enter it.
        if (newParent is not null)
        {
            var (sent, error) = await _otp.SendCodeAsync(user.Email);
            return Ok(new
            {
                message = sent ? "Account created. We emailed you a 6-digit code to verify your email."
                               : "Account created, but the code could not be sent. Use resend-code to try again.",
                user.Id, user.Email, user.Role,
                requiresVerification = true,
                emailSent = sent,
                emailError = error
            });
        }

        return Ok(new { message = "User created.", user.Id, user.Email, user.Role });
    }

    // POST /api/auth/verify-email   { "email": "...", "code": "123456" }
    // Checks the code with Supabase, marks the parent verified, links every child that lists their
    // email, and returns a token so the app can sign the parent straight in.
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var email = (request.Email ?? string.Empty).Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.Role == "parent");
        var parent = user is null ? null : await _db.Parents.FirstOrDefaultAsync(p => p.UserId == user.Id);

        // One generic message for every failure so this can't be used to discover which emails exist.
        if (user is null || parent is null || !await _otp.VerifyCodeAsync(user.Email, request.Code))
        {
            return BadRequest(new { message = "That code is invalid or has expired." });
        }

        parent.EmailVerified = true;
        await _db.SaveChangesAsync();

        var linkedChildren = await _db.LinkChildrenByEmailAsync(parent, user.Email);
        return Ok(new { token = GenerateJwtToken(user), role = user.Role, linkedChildren });
    }

    // POST /api/auth/resend-code   { "email": "..." }
    // Always answers the same way, whether or not the email has an account.
    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode(ResendCodeRequest request)
    {
        var email = (request.Email ?? string.Empty).Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.Role == "parent");
        var parent = user is null ? null : await _db.Parents.FirstOrDefaultAsync(p => p.UserId == user.Id);

        if (user is not null && parent is not null && !parent.EmailVerified)
        {
            var (sent, error) = await _otp.SendCodeAsync(user.Email);
            if (!sent) return StatusCode(429, new { message = error });
        }

        return Ok(new { message = "If that email needs verifying, we have sent a new code." });
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
