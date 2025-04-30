using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

public static class RatingEndpoints
{
    public static void MapRatingEndpoints(this IEndpointRouteBuilder routes)
    {
       routes.MapPost("/api/ratings", [Authorize] async (Rating rating, ApplicationDbContext context, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);

    var existingRating = await context.Ratings
        .FirstOrDefaultAsync(r => r.WorksheetId == rating.WorksheetId && r.UserId == userId);

    if (existingRating != null)
    {
        // עדכון דירוג קיים
        existingRating.RatingValue = rating.RatingValue;
        existingRating.Review = rating.Review;
        await context.SaveChangesAsync();
        return Results.Ok(existingRating); // מחזיר סטטוס 200
    }

    // יצירת דירוג חדש
    rating.UserId = userId;
    rating.CreatedAt = DateTime.Now;

    await context.Ratings.AddAsync(rating);
    await context.SaveChangesAsync();

    return Results.Created($"/api/ratings/{rating.Id}", rating);
})
.WithName("CreateOrUpdateRating")
.WithTags("Ratings")
.Produces<Rating>(StatusCodes.Status200OK)
.Produces<Rating>(StatusCodes.Status201Created);

        // קבלת כל הדירוגים עבור דף עבודה
        routes.MapGet("/api/worksheets/{worksheetId}/ratings", async (int worksheetId, ApplicationDbContext context) =>
        {
            var ratings = await context.Ratings
                .Where(r => r.WorksheetId == worksheetId)
                .Include(r => r.User)
                .ToListAsync();
                
            return Results.Ok(ratings);
        })
        .WithName("GetRatingsByWorksheet")
        .WithTags("Ratings")
        .Produces<List<Rating>>(StatusCodes.Status200OK);
        
        // מחיקת דירוג
        routes.MapDelete("/api/ratings/{id}", [Authorize] async (int id, ApplicationDbContext context, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var isAdmin = httpContext.User.IsInRole("Admin");
            
            var rating = await context.Ratings.FindAsync(id);
            if (rating == null)
                return Results.NotFound();
                
            // רק המשתמש שיצר את הדירוג או מנהל יכולים למחוק
            if (rating.UserId != userId && !isAdmin)
                return Results.Forbid();
                
            context.Ratings.Remove(rating);
            await context.SaveChangesAsync();
            
            return Results.NoContent();
        })
        .WithName("DeleteRating")
        .WithTags("Ratings")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
