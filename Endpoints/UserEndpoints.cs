using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;  // צריך להוסיף את ה-namespace הזה כדי לגשת לקונפיגורציה

// מחלקה לניהול אימות משתמשים
public static class AuthEndpoints
{
    private static string GenerateJwtToken(User user, IConfiguration configuration)
    {
        // קריאת המפתח הסודי מתוך הקונפיגורציה (appsettings.json)
        string jwtKeyStr = configuration["JwtSettings:SecretKey"];

        // אם לא נמצא במשתנה הסביבה או בקובץ ההגדרות, השתמש במפתח קבוע (למטרות פיתוח בלבד)
        if (string.IsNullOrEmpty(jwtKeyStr))
        {
            jwtKeyStr = "SuperSecretKey12345678901234567890123456789012345678901234567890123456";
            Console.WriteLine("Warning: Using hardcoded JWT key. This is insecure for production.");
        }

        var key = Encoding.UTF8.GetBytes(jwtKeyStr);
        var tokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    // מיפוי ה-Endpoints של אימות
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        // התחברות משתמש
        routes.MapPost("/api/auth/login", async (LoginDto loginDto, ApplicationDbContext context, IConfiguration configuration) =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
                return Results.NotFound("משתמש לא נמצא");

            if (user.Password != loginDto.Password) // בפרויקט אמיתי יש להשתמש בהצפנה
                return Results.BadRequest("סיסמה שגויה");

            // יצירת JWT Token
            var token = GenerateJwtToken(user, configuration);

            return Results.Ok(new { token, user });
        })
        .WithName("LoginUser")
        .WithTags("Auth")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
// התחברות מנהל
        routes.MapPost("/api/auth/admin-login", async (LoginDto loginDto, ApplicationDbContext context, IConfiguration configuration) =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
                return Results.NotFound("משתמש לא נמצא");

            if (user.Password != loginDto.Password)
                return Results.BadRequest("סיסמה שגויה");

            if (user.Role != "Admin")
                return Results.Forbid(); // לא ניתן להתחבר כמנהל אם הוא לא מוגדר ככזה

            var token = GenerateJwtToken(user, configuration);
            return Results.Ok(new { token, user });
        })
        .WithName("LoginAdmin")
        .WithTags("Auth");

        // רישום משתמש חדש
        routes.MapPost("/api/auth/register", async (User user, ApplicationDbContext context) =>
        {
            // בדיקה אם המשתמש קיים
            var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (existingUser != null)
                return Results.BadRequest("משתמש עם אימייל זה כבר קיים");

            user.Role = "User";

            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();

            return Results.Created($"/api/users/{user.Id}", user);
        })
        .WithName("RegisterUser")
        .WithTags("Auth")
        .Produces<User>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);
    }

    // מודל עזר להתחברות
    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
