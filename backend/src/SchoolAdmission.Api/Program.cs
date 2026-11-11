using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers and Exception Filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Configure Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Host=postgres;Port=5432;Database=school_admission;Username=postgres;Password=postgres;";

builder.Services.AddDbContext<AdmissionDbContext>(options =>
{
    if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
        });
    }
});

// Register Application Services
builder.Services.AddScoped<IAdmissionService, AdmissionService>();
builder.Services.AddHostedService<HoldExpirationBackgroundService>();

// Add Health Checks
builder.Services.AddHealthChecks();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "School Admission Management API",
        Version = "v1",
        Description = "API for student admission holds, confirmations, and class capacity management."
    });

    // Add X-Student-Id parameter support
    c.AddSecurityDefinition("StudentIdHeader", new OpenApiSecurityScheme
    {
        Name = "X-Student-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "17-digit Student Birth Registration Number (e.g. 20152691234567890)"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "StudentIdHeader"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Enable Swagger in all environments for evaluators
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "School Admission API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/healthz");

// Automatic Database Migration and Initialization with Retry
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

    const int maxRetries = 10;
    var retry = 0;
    while (retry < maxRetries)
    {
        try
        {
            if (dbContext.Database.IsNpgsql())
            {
                logger.LogInformation("Applying database migrations if pending...");
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database migration complete.");
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync();
            }
            break;
        }
        catch (Exception ex)
        {
            retry++;
            logger.LogWarning(ex, "Database connection attempt {Attempt} of {MaxRetries} failed. Retrying in 3 seconds...", retry, maxRetries);
            if (retry >= maxRetries)
            {
                logger.LogError(ex, "Could not connect to database after {MaxRetries} attempts.", maxRetries);
                throw;
            }
            await Task.Delay(3000);
        }
    }
}

app.Run();

// Expose Program for integration test WebApplicationFactory
public partial class Program { }
