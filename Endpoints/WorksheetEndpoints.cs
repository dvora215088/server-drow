using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

public static class WorksheetEndpoints
{
    public static void MapWorksheetEndpoints(this IEndpointRouteBuilder routes)
    {// קבלת דפי עבודה לפי קטגוריה
    
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
            FileCategory = w.Category.Id, // לא להחזיר את כל האובייקט של הקטגוריה
            User = w.User.FirstName // לא להחזיר את כל האובייקט של המשתמש
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


       // העלאת דף עבודה חדש לקטגוריה "משלכם" (קטגוריה עם ID = 1)
routes.MapPost("/api/worksheets/yourOwn", [Authorize] async (Worksheet worksheet, ApplicationDbContext context, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
    
    // יצירת אובייקט חדש כדי למנוע שינויים לא רצויים בנתונים הנכנסים
    var newWorksheet = new Worksheet
    {
        Title = worksheet.Title,
        FileUrl = worksheet.FileUrl,
        AgeGroup = worksheet.AgeGroup,
        Difficulty = worksheet.Difficulty,
        CategoryId = 1, // שייכות אוטומטית לקטגוריה "משלכם"
        UserId = userId
    };

    await context.Worksheets.AddAsync(newWorksheet);
    await context.SaveChangesAsync();

    return Results.Created($"/api/worksheets/{newWorksheet.Id}", newWorksheet);
})
.WithName("CreateMishelachemWorksheet")
.WithTags("Worksheets")
.Produces<Worksheet>(StatusCodes.Status201Created);

        // עדכון דף עבודה
        routes.MapPut("/api/worksheets/{id}", [Authorize] async (int id, Worksheet updatedWorksheet, ApplicationDbContext context, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var isAdmin = httpContext.User.IsInRole("Admin");

            var worksheet = await context.Worksheets.FindAsync(id);
            if (worksheet == null)
                return Results.NotFound();

            // רק בעל הדף או מנהל יכולים לעדכן
            if (worksheet.UserId != userId && !isAdmin)
                return Results.Forbid();

            worksheet.Title = updatedWorksheet.Title;
            worksheet.FileUrl = updatedWorksheet.FileUrl;
            worksheet.AgeGroup = updatedWorksheet.AgeGroup;
            worksheet.Difficulty = updatedWorksheet.Difficulty;
            worksheet.CategoryId = updatedWorksheet.CategoryId;

            await context.SaveChangesAsync();
            return Results.Ok(worksheet);
        })
        .WithName("UpdateWorksheet")
        .WithTags("Worksheets")
        .Produces<Worksheet>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden);

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