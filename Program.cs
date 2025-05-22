using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Amazon.Runtime;
using Amazon;

var builder = WebApplication.CreateBuilder(args);

// טוען את קובץ ה-configurations כמו appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();

// רישום IHttpClientFactory - חשוב!
builder.Services.AddHttpClient();

// רישום HttpClient ספציפי עבור S3
builder.Services.AddHttpClient("S3Client", client => {
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Swagger configuration
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

// AWS S3 setup with credentials from environment variables
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var options = new AmazonS3Config
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1")
    };

    var accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    var secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
 
    if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
    {
        throw new InvalidOperationException("AWS credentials are missing from environment variables.");
    }

    var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
    return new AmazonS3Client(credentials, options);
});

// **הגדרת DbContext מתוקנת עם ניהול connection pool**
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
    {
        // הגדרות MySQL ספציפיות
        mySqlOptions.CommandTimeout(30); // timeout לפקודות SQL
        mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        );
    })
    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()) // רק בפיתוח
    .EnableDetailedErrors(builder.Environment.IsDevelopment())
    .LogTo(Console.WriteLine, LogLevel.Warning); // לוגים רק לאזהרות ומעלה
    
}, ServiceLifetime.Scoped); // וודא שזה Scoped (ברירת מחדל)

// **הוספת connection pool configuration**
builder.Services.Configure<DbContextOptions>(options =>
{
    // זה ירצה רק אם יש לך גרסה חדשה יותר של EF Core
});

// Configure JWT Secret Key dynamically based on environment
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

if (string.IsNullOrEmpty(jwtKey))
{
    jwtKey = builder.Configuration["JwtSettings:SecretKey"];
}

if (string.IsNullOrEmpty(jwtKey))
{
    jwtKey = "MySuperSecretKeyForTestingOnly123456789012345678901234567890";
}

Console.WriteLine($"JWT Key configured with length: {jwtKey.Length}");

// הגדרת אימות JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "https://react-drow.onrender.com",
            "http://localhost:4200",
            "https://angular-drow-manager.onrender.com"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.OAuthClientId("swagger-ui-client");
    c.OAuthAppName("Swagger UI");
});

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// **הוספת middleware לניטור חיבורי DB (אופציונלי)**
app.Use(async (context, next) =>
{
    var dbContext = context.RequestServices.GetService<ApplicationDbContext>();
    if (dbContext != null)
    {
        // ודא שהחיבור ייסגר אחרי הבקשה
        using (dbContext)
        {
            await next();
        }
    }
    else
    {
        await next();
    }
});

// Map all endpoints
app.MapGet("/", () => "welcome!:)");

app.MapAuthEndpoints();
app.MapCategoryEndpoints();
app.MapWorksheetEndpoints();
app.MapDownloadEndpoints();
app.MapRatingEndpoints();
app.MapFavoriteWorksheetEndpoints();
app.MapUploadEndpoints();

// **הוספת endpoint לניטור מצב חיבורי DB**
app.MapGet("/health/db", async (ApplicationDbContext context) =>
{
    try
    {
        await context.Database.CanConnectAsync();
        return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database connection failed: {ex.Message}");
    }
}).WithTags("Health");

app.Run();