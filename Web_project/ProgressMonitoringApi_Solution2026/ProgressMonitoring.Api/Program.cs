using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProgressMonitoring.Api.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----

builder.Services.AddControllers();
builder.Services.AddRazorPages(options => 
{ options.Conventions.AuthorizeFolder("/", "TeacherUi");                                                                                                                                                                                                                                                 
options.Conventions.AllowAnonymousToPage("/Account/Login");
options.Conventions.AllowAnonymousToPage("/Account/Register");
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Lets Swagger's "Authorize" button send a Bearer token on every request.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste your JWT here, e.g. Bearer eyJhbGciOi...",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Connect EF Core to your Supabase (PostgreSQL) database.
// The connection string comes from user-secrets locally (see README) —
// never hardcode it here or commit it to source control.
var connectionString = builder.Configuration.GetConnectionString("Supabase");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

// ---- Authentication ----
// This API issues and validates its own JWTs (see AuthController) rather
// than relying on an external identity provider — simplest option for a
// student project. "Jwt:Key" must be set via user-secrets (see README);
// it should NEVER live in appsettings.json or be committed to GitHub.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured. Run `dotnet user-secrets set \"Jwt:Key\" \"...\"` — see README.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options => { options.LoginPath = "/Account/Login"; options.AccessDeniedPath = "/Account/Login"; options.ExpireTimeSpan = TimeSpan.FromHours(8); });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TeacherOnly", policy => policy.RequireRole("teacher"));
    options.AddPolicy("ParentOnly", policy => policy.RequireRole("parent"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("TeacherUi", policy => { policy.AuthenticationSchemes.Add(CookieAuthenticationDefaults.AuthenticationScheme); policy.RequireRole("teacher"); });
});

// Emails the 6-digit code that parents use to verify their email (via Supabase Auth).
builder.Services.AddHttpClient<SupabaseEmailOtp>();

var app = builder.Build();

// ---- Middleware pipeline ----

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();   // serves wwwroot (logo + stylesheet)

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();
