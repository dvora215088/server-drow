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

// Add services to the container
builder.Services.AddEndpointsApiExplorer();

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
    var builder = WebApplication.CreateBuilder(args);

// טוען את קובץ ה-configurations כמו appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

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
        throw new InvalidOperationException("AWS credentials are missing.");
    }

    var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

    return new AmazonS3Client(credentials, options);
});

// Add DbContext with MySQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

// Configure JWT Secret Key dynamically based on environment
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

// אם לא נמצא במשתני סביבה, ננסה להוציא מקובץ ההגדרות (appsettings.json)
if (string.IsNullOrEmpty(jwtKey))
{
    jwtKey = builder.Configuration["JwtSettings:SecretKey"];
}

// אם עדיין לא נמצא, נשתמש במפתח קבוע (למטרות פיתוח בלבד)
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

// הוספת שירותי הרשאה
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// Add authorization services
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
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
app.UseCors("AllowAll");

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map all endpoints
app.MapGet("/", () => "welcome!:)");

app.MapAuthEndpoints();
app.MapCategoryEndpoints();
app.MapWorksheetEndpoints();
app.MapDownloadEndpoints();
app.MapRatingEndpoints();
app.MapFavoriteWorksheetEndpoints();
app.MapUploadEndpoints();

app.Run();
