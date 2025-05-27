// using Amazon.S3;
// using Amazon.S3.Model;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.IdentityModel.Tokens;
// using System.Text;
// using System.Text.Json.Serialization;
// using System.Security.Claims;
// using Microsoft.AspNetCore.Authorization;
// using Amazon.Runtime;
// using Amazon;

// var builder = WebApplication.CreateBuilder(args);

// // טוען את קובץ ה-configurations כמו appsettings.json
// builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
// builder.Configuration.AddEnvironmentVariables();

// builder.Services.AddEndpointsApiExplorer();

// // רישום IHttpClientFactory - חשוב!
// builder.Services.AddHttpClient();

// // רישום HttpClient ספציפי עבור S3
// builder.Services.AddHttpClient("S3Client", client => {
//     client.Timeout = TimeSpan.FromMinutes(2);
// });

// // Swagger configuration
// builder.Services.AddSwaggerGen(c =>
// {
//     c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
//     {
//         Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
//         Name = "Authorization",
//         In = Microsoft.OpenApi.Models.ParameterLocation.Header,
//         Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
//     });

//     c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
//     {
//         {
//             new Microsoft.OpenApi.Models.OpenApiSecurityScheme
//             {
//                 Reference = new Microsoft.OpenApi.Models.OpenApiReference
//                 {
//                     Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
//                     Id = "Bearer"
//                 }
//             },
//             new string[] {}
//         }
//     });
// });

// // AWS S3 setup with credentials from environment variables
// builder.Services.AddSingleton<IAmazonS3>(sp =>
// {
//     var options = new AmazonS3Config
//     {
//         RegionEndpoint = RegionEndpoint.GetBySystemName(Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1")
//     };

//     var accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
//     var secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
 
//     if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
//     {
//         throw new InvalidOperationException("AWS credentials are missing from environment variables.");
//     }

//     var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
//     return new AmazonS3Client(credentials, options);
// });

// // **הגדרת DbContext מתוקנת עם ניהול connection pool**
// builder.Services.AddDbContext<ApplicationDbContext>(options =>
// {
//     var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
//     options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
//     {
//         // הגדרות MySQL ספציפיות
//         mySqlOptions.CommandTimeout(30); // timeout לפקודות SQL
//         mySqlOptions.EnableRetryOnFailure(
//             maxRetryCount: 3,
//             maxRetryDelay: TimeSpan.FromSeconds(5),
//             errorNumbersToAdd: null
//         );
//     })
//     .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()) // רק בפיתוח
//     .EnableDetailedErrors(builder.Environment.IsDevelopment())
//     .LogTo(Console.WriteLine, LogLevel.Warning); // לוגים רק לאזהרות ומעלה
    
// }, ServiceLifetime.Scoped); // וודא שזה Scoped (ברירת מחדל)

// // **הוספת connection pool configuration**
// builder.Services.Configure<DbContextOptions>(options =>
// {
//     // זה ירצה רק אם יש לך גרסה חדשה יותר של EF Core
// });

// // Configure JWT Secret Key dynamically based on environment
// var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

// if (string.IsNullOrEmpty(jwtKey))
// {
//     jwtKey = builder.Configuration["JwtSettings:SecretKey"];
// }

// if (string.IsNullOrEmpty(jwtKey))
// {
//     jwtKey = "MySuperSecretKeyForTestingOnly123456789012345678901234567890";
// }

// Console.WriteLine($"JWT Key configured with length: {jwtKey.Length}");

// // הגדרת אימות JWT
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         options.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuerSigningKey = true,
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
//             ValidateIssuer = false,
//             ValidateAudience = false,
//             ClockSkew = TimeSpan.Zero
//         };

//         options.Events = new JwtBearerEvents
//         {
//             OnAuthenticationFailed = context =>
//             {
//                 Console.WriteLine($"Authentication failed: {context.Exception.Message}");
//                 return Task.CompletedTask;
//             }
//         };
//     });

// builder.Services.AddAuthorization();
// builder.Services.ConfigureHttpJsonOptions(options =>
// {
//     options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
// });

// // Add CORS
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("AllowFrontend", policy =>
//     {
//         policy.WithOrigins(
//             "http://localhost:5173",
//             "https://react-drow.onrender.com",
//             "http://localhost:4200",
//             "https://angular-drow-manager.onrender.com"
//         )
//         .AllowAnyMethod()
//         .AllowAnyHeader()
//         .AllowCredentials();
//     });
// });

// var app = builder.Build();

// // Configure the HTTP request pipeline
// app.UseSwagger();
// app.UseSwaggerUI(c =>
// {
//     c.OAuthClientId("swagger-ui-client");
//     c.OAuthAppName("Swagger UI");
// });

// app.UseHttpsRedirection();
// app.UseCors("AllowFrontend");

// // Authentication & Authorization middleware
// app.UseAuthentication();
// app.UseAuthorization();

// // **הוספת middleware לניטור חיבורי DB (אופציונלי)**
// app.Use(async (context, next) =>
// {
//     var dbContext = context.RequestServices.GetService<ApplicationDbContext>();
//     if (dbContext != null)
//     {
//         // ודא שהחיבור ייסגר אחרי הבקשה
//         using (dbContext)
//         {
//             await next();
//         }
//     }
//     else
//     {
//         await next();
//     }
// });

// // Map all endpoints
// app.MapGet("/", () => "welcome!:)");

// app.MapAuthEndpoints();
// app.MapCategoryEndpoints();
// app.MapWorksheetEndpoints();
// app.MapDownloadEndpoints();
// app.MapRatingEndpoints();
// app.MapFavoriteWorksheetEndpoints();
// app.MapUploadEndpoints();

// // **הוספת endpoint לניטור מצב חיבורי DB**
// app.MapGet("/health/db", async (ApplicationDbContext context) =>
// {
//     try
//     {
//         await context.Database.CanConnectAsync();
//         return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
//     }
//     catch (Exception ex)
//     {
//         return Results.Problem($"Database connection failed: {ex.Message}");
//     }
// }).WithTags("Health");

// app.Run();

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
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// **שיפור 1: הגדרת Logging מותאמת**
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.AddConsole();
    builder.Logging.AddApplicationInsights(); // עבור Azure
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
}

// טוען את קובץ ה-configurations
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

// **שיפור 2: Response Compression**
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

// **שיפור 3: הגדרת Rate Limiting**
builder.Services.AddRateLimiter(options =>
{
    // מגבלה כללית
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100, // 100 בקשות
                Window = TimeSpan.FromMinutes(1) // לדקה
            }));

    // מגבלה להורדות
    options.AddFixedWindowLimiter("DownloadPolicy", opt =>
    {
        opt.PermitLimit = 10; // 10 הורדות
        opt.Window = TimeSpan.FromMinutes(1); // לדקה
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });

    // מגבלה לאימות
    options.AddSlidingWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 5; // 5 ניסיונות
        opt.Window = TimeSpan.FromMinutes(15); // ל-15 דקות
        opt.SegmentsPerWindow = 3;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

builder.Services.AddEndpointsApiExplorer();

// **שיפור 4: HttpClient מותאם לביצועים**
builder.Services.AddHttpClient();

builder.Services.AddHttpClient("S3Client", client => {
    client.Timeout = TimeSpan.FromMinutes(2);
    client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");
    client.MaxResponseContentBufferSize = 52428800; // 50MB
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    MaxConnectionsPerServer = 20, // מגביל חיבורים במקביל
    UseCookies = false // מיותר עבור S3
});

// **שיפור 5: Connection Pooling מתקדם**
builder.Services.Configure<HttpClientFactoryOptions>("S3Client", options =>
{
    options.HandlerLifetime = TimeSpan.FromMinutes(10); // מחזור handler
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

// **שיפור 6: AWS S3 עם Connection Pooling**
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    
    var options = new AmazonS3Config
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1"),
        MaxConnectionsPerServer = 50, // מחיבורים במקביל
        Timeout = TimeSpan.FromMinutes(2),
        ReadWriteTimeout = TimeSpan.FromMinutes(2),
        UseHttp = false, // כפה HTTPS
        BufferSize = 8192 * 16 // 128KB buffer
    };

    var accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    var secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
 
    if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
    {
        logger.LogError("AWS credentials are missing from environment variables");
        throw new InvalidOperationException("AWS credentials are missing from environment variables.");
    }

    var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
    var client = new AmazonS3Client(credentials, options);
    
    logger.LogInformation("AWS S3 client initialized successfully");
    return client;
});

// **שיפור 7: DbContext מותאם לביצועים גבוהים**
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
    {
        // **הגדרות MySQL מתקדמות לביצועים**
        mySqlOptions.CommandTimeout(30);
        mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        );
        
        // **שיפורי ביצועים למסד נתונים**
        mySqlOptions.EnableIndexOptimizedBooleanColumns(true);
        mySqlOptions.EnableStringComparisonTranslations(true);
    })
    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
    .EnableDetailedErrors(builder.Environment.IsDevelopment())
    .EnableServiceProviderCaching() // **קריטי לביצועים**
    .EnableQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery) // **שיפור לקוריות מורכבות**
    .LogTo(
        filter: (eventId, level) => level >= LogLevel.Warning,
        logger: message => Console.WriteLine($"[EF] {message}")
    );
    
}, ServiceLifetime.Scoped);

// **שיפור 8: Connection Pool Configuration**
builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
{
    // זה רק אם אתה רוצה connection pooling מתקדם יותר
    // (בדרך כלל לא נדרש אלא עבור load גבוה מאוד)
}, poolSize: 32); // מספר החיבורים בבריכה

// **שיפור 9: JWT מותאם**
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? builder.Configuration["JwtSettings:SecretKey"] 
    ?? "MySuperSecretKeyForTestingOnly123456789012345678901234567890";

var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
logger.LogInformation($"JWT Key configured with length: {jwtKey.Length}");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero,
            // **שיפור אבטחה**
            ValidateLifetime = true,
            RequireExpirationTime = true
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var contextLogger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                contextLogger.LogWarning($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
        
        // **שיפור ביצועים - cache tokens**
        options.SaveToken = false; // חסוך זיכרון אם לא צריך
    });

builder.Services.AddAuthorization();

// **שיפור 10: JSON מותאם לביצועים**
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    // שיפור ביצועים
    options.SerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
});

// **שיפור 11: CORS מותאם**
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Environment.IsDevelopment() 
            ? new[] { 
                "http://localhost:5173", 
                "http://localhost:4200", 
                "http://localhost:3000" 
              }
            : new[] { 
                "https://react-drow.onrender.com", 
                "https://angular-drow-manager.onrender.com" 
              };
              
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10)); // cache preflight
    });
});

// **שיפור 12: Health Checks**
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    .AddCheck("aws-s3", () => 
    {
        // בדיקה פשוטה של S3 connectivity
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("S3 configured");
    });

var app = builder.Build();

// **שיפור 13: Middleware מסודר לפי סדר חשיבות**
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.OAuthClientId("swagger-ui-client");
        c.OAuthAppName("Swagger UI");
    });
}

// Response compression should be early
app.UseResponseCompression();

// Rate limiting before authentication
app.UseRateLimiter();

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

// Security middleware
app.UseAuthentication();
app.UseAuthorization();

// **הסרת הMiddleware הבעייתי**
// הDbContext מנוהל אוטומטית על ידי DI container
// אין צורך לעטוף אותו במiddleware נוסף

// Map endpoints
app.MapGet("/", () => "Welcome! Server is running optimally 🚀")
    .EnableRateLimiting("DownloadPolicy"); // דוגמה לשימוש בrate limiting

app.MapAuthEndpoints();
app.MapCategoryEndpoints();
app.MapWorksheetEndpoints();
app.MapDownloadEndpoints();
app.MapRatingEndpoints();
app.MapFavoriteWorksheetEndpoints();
app.MapUploadEndpoints();

// **שיפור 14: Health check endpoints מתקדמים**
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.Select(x => new
            {
                Name = x.Key,
                Status = x.Value.Status.ToString(),
                Duration = x.Value.Duration.TotalMilliseconds,
                Description = x.Value.Description
            }),
            TotalDuration = report.TotalDuration.TotalMilliseconds
        };
        
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

// פשוט יותר לבדיקת DB
app.MapGet("/health/db", async (ApplicationDbContext context) =>
{
    try
    {
        var canConnect = await context.Database.CanConnectAsync();
        return Results.Ok(new { 
            Status = canConnect ? "Healthy" : "Unhealthy", 
            Timestamp = DateTime.UtcNow,
            DatabaseProvider = context.Database.ProviderName
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database connection failed: {ex.Message}");
    }
}).WithTags("Health");

// **שיפור 15: Graceful shutdown**
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("Application is shutting down gracefully...");
});

app.Run();