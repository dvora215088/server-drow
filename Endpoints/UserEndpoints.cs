using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

// מחלקה לניהול אימות משתמשים
public static class AuthEndpoints
{
    // פונקציה ליצירת JWT Token
    private static string GenerateJwtToken(User user, IConfiguration configuration)
    {
        string jwtKeyStr = configuration["JwtSettings:SecretKey"];

        // אם לא נמצא במשתנה הסביבה או בקובץ ההגדרות, השתמש במפתח קבוע (למטרות פיתוח בלבד)
        if (string.IsNullOrEmpty(jwtKeyStr))
        {
            jwtKeyStr = "SuperSecretKey12345678901234567890123456789012345678901234567890123456";
            Console.WriteLine("Warning: Using hardcoded JWT key. This is insecure for production.");
        }

        var key = Encoding.UTF8.GetBytes(jwtKeyStr);
        var tokenHandler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        if (user.Role == "Admin")
        {
            claims.Add(new Claim("IsAdmin", "true"));
            claims.Add(new Claim("AdminPrivileges", "FullAccess"));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = user.Role == "Admin" ? DateTime.UtcNow.AddHours(2) : DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    // מיפוי ה-Endpoints של אימות
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        // התחברות משתמש
        routes.MapPost("/api/auth/login", async (LoginDto loginDto, ApplicationDbContext context, IConfiguration configuration, IPasswordHasher<User> passwordHasher) =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
                return Results.NotFound("משתמש לא נמצא");

            // השוואת סיסמאות - השוואה עם הסיסמה המוצפנת
            var passwordVerificationResult = passwordHasher.VerifyHashedPassword(user, user.Password, loginDto.Password);
            if (passwordVerificationResult != PasswordVerificationResult.Success)
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
        routes.MapPost("/api/auth/admin/login", async (LoginDto loginDto, ApplicationDbContext context, IConfiguration configuration, IPasswordHasher<User> passwordHasher) =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
                return Results.NotFound("משתמש לא נמצא");

            // השוואת סיסמאות
            var passwordVerificationResult = passwordHasher.VerifyHashedPassword(user, user.Password, loginDto.Password);
            if (passwordVerificationResult != PasswordVerificationResult.Success)
                return Results.BadRequest("סיסמה שגויה");

            if (user.Role != "Admin")
                return Results.Forbid();

            // יצירת JWT Token עם תפקיד מנהל
            var token = GenerateJwtToken(user, configuration);

            return Results.Ok(new { token, user });
        })
        .WithName("AdminLogin")
        .WithTags("Auth")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden);

        // רישום משתמש חדש
        routes.MapPost("/api/auth/register", async (User user, ApplicationDbContext context, IPasswordHasher<User> passwordHasher) =>
        {
            // בדיקה אם המשתמש קיים
            var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (existingUser != null)
                return Results.BadRequest("משתמש עם אימייל זה כבר קיים");

            // הצפנת סיסמה לפני שמירה
            user.Password = passwordHasher.HashPassword(user, user.Password);
            user.Role = "User"; // ברירת מחדל של תפקיד

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
    
    // מודל משתמש
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }

    // דוגמת ApplicationDbContext
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
    }
}
