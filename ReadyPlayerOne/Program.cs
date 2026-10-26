using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.Data.Seeders;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;
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
            document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
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
        ?? throw new InvalidOperationException("Database connection string is missing.")));

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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

// APPLICATION SERVICES
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

// CORS
var clientOrigin = builder.Configuration["Client:Origin"]
    ?? throw new InvalidOperationException("React client origin is missing.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactClient", policy =>
    {
        policy.WithOrigins(clientOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// DATABASE INITIALIZATION
await IdentitySeeder.SeedRolesAsync(app.Services);

// OPENAPI & SCALAR
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.AddPreferredSecuritySchemes("Bearer");
    });
}

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
        if (!string.Equals(origin, clientOrigin, StringComparison.Ordinal))
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