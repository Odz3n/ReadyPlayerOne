using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ReadyPlayerOne.Configurations;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.Data.Seeders;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;
using ReadyPlayerOne.Repositories;
using ReadyPlayerOne.Services;
using Scalar.AspNetCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// CONTROLLERS & OPENAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT Access Token."
        };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var allowAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();
        if (!requiresAuthorization || allowAnonymous)
            return Task.CompletedTask;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        return Task.CompletedTask;
    });
});

// DATABASE
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Database connection string is missing."),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(15),
                errorNumbersToAdd: null);
        }));

// IDENTITY
builder.Services.AddDataProtection();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;

    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;

    // Account lockout
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
    .AddRoles<IdentityRole>()
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// JWT SETTINGS
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"]
    ?? throw new InvalidOperationException("JWT key is missing.");
var jwtIssuer = jwtSettings["Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is missing.");
var jwtAudience = jwtSettings["Audience"]
    ?? throw new InvalidOperationException("JWT audience is missing.");
var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwtKey));

// AUTHENTICATION
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = signingKey,

            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role,

            ClockSkew = TimeSpan.Zero
        };
    });

// AUTHORIZATION
builder.Services.AddAuthorization();

// AUTHENTICATION SERVICES
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

// REPOSITORIES
builder.Services.AddScoped<IRepository<Game, Guid>, GameRepository>();
builder.Services.AddScoped<IRepository<GameSession, Guid>, GameSessionRepository>();
builder.Services.AddScoped<IRepository<Team, Guid>, TeamRepository>();
builder.Services.AddScoped<IRepository<SessionParticipant, Guid>, SessionParticipantRepository>();
builder.Services.AddScoped<IRepository<Genre, Guid>, GenreRepository>();

// APPLICATION SERVICES
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IGenreService, GenreService>();

builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailService, EmailService>();

// CORS
var allowedOrigins = builder.Configuration
    .GetSection("Client:AllowedOrigins")
    .Get<string[]>()
    ?? throw new InvalidOperationException(
        "React client allowed origins are missing.");

if (allowedOrigins.Length == 0)
    throw new InvalidOperationException(
        "At least one React client origin must be configured.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// DATABASE INITIALIZATION
await IdentitySeeder.SeedRolesAsync(app.Services);
await IdentitySeeder.SeedAdminAsync(app.Services);

// OPENAPI & SCALAR
app.MapOpenApi();

app.MapScalarApiReference(options =>
{
    options.AddPreferredSecuritySchemes("Bearer");
});

// MIDDLEWARE
app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("ReactClient");

// CSRF PROTECTION FOR COOKIE-BASED ENDPOINTS
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    var isCookieEndpoint =
        path.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase);

    if (isCookieEndpoint && HttpMethods.IsPost(context.Request.Method))
    {
        var origin = context.Request.Headers.Origin.ToString();

        app.Logger.LogInformation(
            "Auth cookie request: Origin={Origin}, AllowedOrigins={AllowedOrigins}",
            origin,
            string.Join(", ", allowedOrigins));

        if (!allowedOrigins.Contains(origin, StringComparer.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();