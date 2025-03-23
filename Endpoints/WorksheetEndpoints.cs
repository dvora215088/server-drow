using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
public static class WorksheetEndpoints
{
    public static void MapWorksheetEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/worksheets", [Authorize] async (Worksheet newWorksheet, ApplicationDbContext context, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);

    // משתמש רגיל תמיד מוסיף לקטגוריה 1
    const int defaultCategoryId = 1;

    // בדיקה אם הקטגוריה 1 קיימת
    var category = await context.Categories.FindAsync(defaultCategoryId);
    if (category == null)
        return Results.NotFound("קטגוריה ברירת מחדל לא נמצאה.");

    // יצירת דף עבודה חדש
    var worksheet = new Worksheet
    {
        Title = newWorksheet.Title,
        FileUrl = newWorksheet.FileUrl,
        AgeGroup = newWorksheet.AgeGroup,
        Difficulty = newWorksheet.Difficulty,
        CategoryId = defaultCategoryId, // תמיד קטגוריה 1
        UserId = userId
    };

    context.Worksheets.Add(worksheet);
    await context.SaveChangesAsync();

    return Results.Created($"/api/worksheets/{worksheet.Id}", worksheet);
})
.WithName("AddWorksheetForUser")
.WithTags("Worksheets")
.Produces<Worksheet>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status404NotFound);



// חיפוש דפי עבודה לפי כותרת בסדר א-ב
routes.MapGet("/api/worksheets/search/alphabetical", async (ApplicationDbContext context, string? startsWith = null, bool ascending = true) =>
{
    try
    {
        IQueryable<Worksheet> worksheetsQuery = context.Worksheets
            .Include(w => w.Category)
            .Include(w => w.User);
        
        // סינון לפי אות פותחת
        if (!string.IsNullOrWhiteSpace(startsWith))
        {
            worksheetsQuery = worksheetsQuery.Where(w => EF.Functions.Like(w.Title, startsWith + "%"));
        }
        
        // מיון לפי סדר אלפביתי
        worksheetsQuery = ascending 
            ? worksheetsQuery.OrderBy(w => w.Title)
            : worksheetsQuery.OrderByDescending(w => w.Title);
        
        var worksheets = await worksheetsQuery
            .Select(w => new
            {
                w.Id,
                w.Title,
                w.CategoryId,
                w.AgeGroup,
                w.Difficulty,
                w.UserId,
                FileCategory = w.Category != null ? w.Category.Id : 0,
                User = w.User != null ? w.User.FirstName : string.Empty
            })
            .ToListAsync();
        
        return Results.Ok(worksheets);
    }
    catch (Exception ex)
    {
        return Results.Problem($"שגיאה בחיפוש דפי עבודה: {ex.Message}", statusCode: 500);
    }
})
.WithName("SearchWorksheetsByAlphabet")
.WithTags("Worksheets")
.Produces<List<object>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);
        routes.MapPost("/api/worksheets/admin", [Authorize(Roles = "Admin")] async (Worksheet newWorksheet, ApplicationDbContext context, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // בדיקה אם הקטגוריה שהמנהל בחר קיימת
            var category = await context.Categories.FindAsync(newWorksheet.CategoryId);
            if (category == null)
                return Results.NotFound("קטגוריה לא נמצאה.");

            // יצירת דף עבודה חדש
            var worksheet = new Worksheet
            {
                Title = newWorksheet.Title,
                FileUrl = newWorksheet.FileUrl,
                AgeGroup = newWorksheet.AgeGroup,
                Difficulty = newWorksheet.Difficulty,
                CategoryId = newWorksheet.CategoryId, // המנהל בוחר קטגוריה
                UserId = userId
            };

            context.Worksheets.Add(worksheet);
            await context.SaveChangesAsync();

            return Results.Created($"/api/worksheets/{worksheet.Id}", worksheet);
        })
        .WithName("AddWorksheetAsAdmin")
        .WithTags("Worksheets")
        .Produces<Worksheet>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound);

        //קבלת דף עדפי עבודה לפי קטגוריה
        routes.MapGet("/api/worksheets/category/{categoryId}", async (int categoryId, ApplicationDbContext context) =>
        {
            var worksheets = await context.Worksheets
                .Where(w => w.CategoryId == categoryId)
                .Include(w => w.Category)
                .Include(w => w.User)
                .Select(w => new
                {
                    w.Id,
                    w.Title,
                    w.CategoryId,
                    w.AgeGroup,
                    w.Difficulty,
                    w.UserId,
                    FileCategory = w.Category.Id, // לא להחזיר את כל האובייקט של הקטגוריה
                    User = w.User.FirstName // לא להחזיר את כל האובייקט של המשתמש
                })
                .ToListAsync();

            return Results.Ok(worksheets);
        })
        .WithName("GetWorksheetsByCategory")
        .WithTags("Worksheets")
        .Produces<List<object>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        routes.MapGet("/api/worksheets/yourOwn", async (ApplicationDbContext context) =>
    {
        var worksheets = await context.Worksheets
            .Where(w => w.CategoryId == 1)
            .Include(w => w.Category)
            .Include(w => w.User)
            .Select(w => new
            {
                w.Id,
                w.Title,
                w.CategoryId,
                w.AgeGroup,
                w.Difficulty,
                w.UserId,
                FileCategory = w.Category.Id,
                User = w.User.FirstName
            })
            .ToListAsync();

        return Results.Ok(worksheets);
    })
.WithName("GetMishelachemWorksheets")
.WithTags("Worksheets")
.Produces<List<object>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);
        //לקבלת כל דפי העבודה
        routes.MapGet("/api/worksheets", async (ApplicationDbContext context) =>
        {
            var worksheets = await context.Worksheets
                .Include(w => w.Category)
                .Include(w => w.User)
                .Select(w => new
                {
                    w.Id,
                    w.Title,
                    w.CategoryId,
                    w.AgeGroup,
                    w.Difficulty,
                    w.UserId,
                    FileCategory = w.Category.Id, // לא להחזיר את כל האובייקט של הקטגוריה
                    User = w.User.FirstName // לא להחזיר את כל האובייקט של המשתמש
                })
                .ToListAsync();

            return Results.Ok(worksheets);
        })
        .WithName("GetAllWorksheets")
        .WithTags("Worksheets")
        .Produces<List<object>>(StatusCodes.Status200OK);



        // // עדכון דף עבודה
        // routes.MapPut("/api/worksheets/{id}", [Authorize] async (int id, Worksheet updatedWorksheet, ApplicationDbContext context, HttpContext httpContext) =>
        // {
        //     var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
        //     var isAdmin = httpContext.User.IsInRole("Admin");

        //     var worksheet = await context.Worksheets.FindAsync(id);
        //     if (worksheet == null)
        //         return Results.NotFound();

        //     // רק בעל הדף או מנהל יכולים לעדכן
        //     if (worksheet.UserId != userId && !isAdmin)
        //         return Results.Forbid();

        //     worksheet.Title = updatedWorksheet.Title;
        //     worksheet.FileUrl = updatedWorksheet.FileUrl;
        //     worksheet.AgeGroup = updatedWorksheet.AgeGroup;
        //     worksheet.Difficulty = updatedWorksheet.Difficulty;
        //     worksheet.CategoryId = updatedWorksheet.CategoryId;

        //     await context.SaveChangesAsync();
        //     return Results.Ok(worksheet);
        // })
        // .WithName("UpdateWorksheet")
        // .WithTags("Worksheets")
        // .Produces<Worksheet>(StatusCodes.Status200OK)
        // .Produces(StatusCodes.Status404NotFound)
        // .Produces(StatusCodes.Status403Forbidden);

        // מחיקת דף עבודה - רק למנהל או למשתמש שיצר את הדף
        routes.MapDelete("/api/worksheets/{id}", [Authorize] async (int id, ApplicationDbContext context, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var isAdmin = httpContext.User.IsInRole("Admin");

            var worksheet = await context.Worksheets.FindAsync(id);
            if (worksheet == null)
                return Results.NotFound();

            // רק בעל הדף או מנהל יכולים למחוק
            if (worksheet.UserId != userId && !isAdmin)
                return Results.Forbid();

            context.Worksheets.Remove(worksheet);
            await context.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("DeleteWorksheet")
        .WithTags("Worksheets")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden);

        // קבלת כל דפי העבודה כולל משובים ודירוגים - רק למנהל
        routes.MapGet("/api/worksheets/admin/all-with-ratings", [Authorize(Roles = "Admin")] async (ApplicationDbContext context) =>
        {
            var worksheets = await context.Worksheets
                .Include(w => w.Category)
                .Include(w => w.User)
                .Include(w => w.Ratings)
                    .ThenInclude(r => r.User)
                .ToListAsync();

            return Results.Ok(worksheets);
        })
        .WithName("GetAllWorksheetsWithRatings")
        .WithTags("Worksheets")
        .Produces<List<Worksheet>>(StatusCodes.Status200OK);

        // הוספת דף עבודה לקטגוריה - רק למנהל
        routes.MapPut("/api/worksheets/{id}/category/{categoryId}", [Authorize(Roles = "Admin")] async (int id, int categoryId, ApplicationDbContext context) =>
        {
            var worksheet = await context.Worksheets.FindAsync(id);
            if (worksheet == null)
                return Results.NotFound("דף עבודה לא נמצא");

            var category = await context.Categories.FindAsync(categoryId);
            if (category == null)
                return Results.NotFound("קטגוריה לא נמצאה");

            worksheet.CategoryId = categoryId;
            await context.SaveChangesAsync();

            return Results.Ok(worksheet);
        })
        .WithName("AssignWorksheetToCategory")
        .WithTags("Worksheets")
        .Produces<Worksheet>(StatusCodes.Status200OK);
    }

    public static void MapDownloadEndpoints(this IEndpointRouteBuilder routes)
    {
        // הורדת דף עבודה
        routes.MapGet("/api/worksheets/{id}/download", [Authorize] async (int id, ApplicationDbContext context) =>
        {
            var worksheet = await context.Worksheets.FindAsync(id);
            if (worksheet == null)
                return Results.NotFound();

            // בפועל כאן אפשר לממש לוגיקת הורדה, אך כרגע רק מחזירים את הקישור
            return Results.Ok(new { DownloadUrl = worksheet.FileUrl });
        })
        .WithName("DownloadWorksheet")
        .WithTags("Worksheets")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);


    }

}