using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public static class AuthEndpoints
{
    private static string GenerateJwtToken(User user, IConfiguration configuration)
    {
        string jwtKeyStr = configuration["JwtSettings:SecretKey"];

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
                new Claim(ClaimTypes.Role, user.Role) // תפקיד כמו "User" או "Admin"
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        // התחברות משתמש רגיל
        routes.MapPost("/api/auth/login", async (LoginDto loginDto, ApplicationDbContext context, IConfiguration configuration) =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
                return Results.NotFound("משתמש לא נמצא");

            if (user.Password != loginDto.Password)
                return Results.BadRequest("סיסמה שגויה");

            if (user.Role != "User")
                return Results.Forbid(); // לא מאפשר התחברות למנהל דרך כאן

            var token = GenerateJwtToken(user, configuration);
            return Results.Ok(new { token, user });
        })
        .WithName("LoginUser")
        .WithTags("Auth");

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

        // רישום משתמש חדש (תמיד נרשמים כמשתמש רגיל)
        routes.MapPost("/api/auth/register", async (User user, ApplicationDbContext context) =>
        {
            var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (existingUser != null)
                return Results.BadRequest("משתמש עם אימייל זה כבר קיים");

            user.Role = "User"; // תמיד רושמים כמשתמש רגיל
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();

            return Results.Created($"/api/users/{user.Id}", user);
        })
        .WithName("RegisterUser")
        .WithTags("Auth");
    }

    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
