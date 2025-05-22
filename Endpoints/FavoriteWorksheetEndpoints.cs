// מחלקה לניהול מועדפים
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

public static class FavoriteWorksheetEndpoints
{
    public static void MapFavoriteWorksheetEndpoints(this IEndpointRouteBuilder routes)
    {
        // קבלת כל דפי העבודה המועדפים עבור משתמש
        routes.MapGet("/api/favorites", [Authorize] async (ApplicationDbContext context, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            
            var favorites = await context.FavoriteWorksheets
                .Where(f => f.UserId == userId)
                .Include(f => f.Worksheet)
                    .ThenInclude(w => w.Category)
                .ToListAsync();
                
            return Results.Ok(favorites);
        })
        .WithName("GetUserFavorites")
        .WithTags("Favorites")
        .Produces<List<FavoriteWorksheet>>(StatusCodes.Status200OK);

        // הוספת דף עבודה למועדפים
        _ = routes.MapPost("/api/favorites", [Authorize] async (FavoriteWorksheetDto favoriteDto, ApplicationDbContext context, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // בדיקה אם דף העבודה כבר במועדפים
            var existingFavorite = await context.FavoriteWorksheets
                .FirstOrDefaultAsync(f => f.WorksheetId == favoriteDto.WorksheetId && f.UserId == userId);

            if (existingFavorite != null)
                return Results.BadRequest("דף העבודה כבר נמצא במועדפים שלך");

            // בדיקה אם דף העבודה קיים
            var worksheet = await context.Worksheets.FindAsync(favoriteDto.WorksheetId);
            if (worksheet == null)
                return Results.NotFound("דף העבודה לא נמצא");

            var favorite = new FavoriteWorksheet
            {
                UserId = userId,
                WorksheetId = favoriteDto.WorksheetId
            };

            await context.FavoriteWorksheets.AddAsync(favorite);
            await context.SaveChangesAsync();

            return Results.Created($"/api/favorites/{favorite.Id}", favorite);
        })
        .WithName("AddToFavorites")
        .WithTags("Favorites")
        .Produces<FavoriteWorksheet>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
        
        // הסרת דף עבודה מהמועדפים
        routes.MapDelete("/api/favorites/{worksheetId}", [Authorize] async (int worksheetId, ApplicationDbContext context, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            
            var favorite = await context.FavoriteWorksheets
                .FirstOrDefaultAsync(f => f.WorksheetId == worksheetId && f.UserId == userId);
                
            if (favorite == null)
                return Results.NotFound("דף העבודה לא נמצא במועדפים שלך");
                
            context.FavoriteWorksheets.Remove(favorite);
            await context.SaveChangesAsync();
            
            return Results.NoContent();
        })
        .WithName("RemoveFromFavorites")
        .WithTags("Favorites")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
        
        // בדיקה אם דף עבודה נמצא במועדפים של המשתמש
        routes.MapGet("/api/favorites/check/{worksheetId}", [Authorize] async (int worksheetId, ApplicationDbContext context, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            
            var favorite = await context.FavoriteWorksheets
                .FirstOrDefaultAsync(f => f.WorksheetId == worksheetId && f.UserId == userId);
                
            return Results.Ok(new { isFavorite = favorite != null });
        })
        .WithName("CheckIfFavorite")
        .WithTags("Favorites")
        .Produces<object>(StatusCodes.Status200OK);
    }
}

// מודל עזר להוספת מועדפים
public class FavoriteWorksheetDto
{
    public int WorksheetId { get; set; }
}

