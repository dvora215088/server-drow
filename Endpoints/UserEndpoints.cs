using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

// מחלקה לניהול אימות משתמשים
public static class AuthEndpoints
{
    // פונקציה ליצירת טוקן JWT
    private static string GenerateJwtToken(User user)
    {
        var key = Encoding.UTF8.GetBytes("SuperSecretKey12345678901234567890123456789012345678901234567890123456"); // מפתח 256 ביט
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
        routes.MapPost("/api/auth/login", async (LoginDto loginDto, ApplicationDbContext context) =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);
            
            if (user == null)
                return Results.NotFound("משתמש לא נמצא");
                
            if (user.Password != loginDto.Password) // בפרויקט אמיתי יש להשתמש בהצפנה
                return Results.BadRequest("סיסמה שגויה");
            
            // יצירת JWT Token
            var token = GenerateJwtToken(user);
            
            return Results.Ok(new { token, user });
        })
        .WithName("LoginUser")
        .WithTags("Auth")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
        
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
}

// מודל עזר להתחברות
public class LoginDto
{
    public string Email { get; set; }
    public string Password { get; set; }
}

// מחלקה לניהול קטגוריות
