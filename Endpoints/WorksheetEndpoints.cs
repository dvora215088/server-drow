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
      // יצירת דף עבודה חדש
routes.MapPost("/api/worksheets", [Authorize] async (Worksheet newWorksheet, ApplicationDbContext context, HttpContext httpContext) =>
{
    var userId = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);

    const int defaultCategoryId = 1;

    var category = await context.Categories.FindAsync(defaultCategoryId);
    if (category == null)
        return Results.NotFound("קטגוריה ברירת מחדל לא נמצאה.");

    var worksheet = new Worksheet
    {
        Title = newWorksheet.Title,
        FileUrl = newWorksheet.FileUrl,  // הקישור שיתקבל מ-S3
        AgeGroup = newWorksheet.AgeGroup,
        Difficulty = newWorksheet.Difficulty,
        CategoryId = defaultCategoryId,
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



        // חיפוש דפי עבודה לפי קטגוריה בסדר א-ב
        routes.MapGet("/api/worksheets/search/category", async (ApplicationDbContext context, int? categoryId = null, string? startsWith = null, bool ascending = true) =>
        {
            try
            {
                IQueryable<Worksheet> worksheetsQuery = context.Worksheets
                    .Include(w => w.Category)
                    .Include(w => w.User);

                // סינון לפי קטגוריה
                if (categoryId.HasValue && categoryId > 0)
                {
                    worksheetsQuery = worksheetsQuery.Where(w => w.CategoryId == categoryId.Value);
                }

                // סינון לפי אות פותחת (אופציונלי)
                if (!string.IsNullOrWhiteSpace(startsWith))
                {
                    worksheetsQuery = worksheetsQuery.Where(w => EF.Functions.Like(w.Title, startsWith + "%"));
                }

                // מיון לפי סדר אלפביתי של הכותרת
                worksheetsQuery = ascending
                    ? worksheetsQuery.OrderBy(w => w.Title)
                    : worksheetsQuery.OrderByDescending(w => w.Title);

                var worksheets = await worksheetsQuery
                    .Select(w => new
                    {
                        w.Id,
                        w.Title,
                        w.CategoryId,
                        CategoryName = w.Category != null ? w.Category.CategoryName : string.Empty,
                        w.AgeGroup,
                        w.Difficulty,
                        w.UserId,
                        UserName = w.User != null ? w.User.FirstName : string.Empty,
                        w.FileUrl
                    })
                    .ToListAsync();

                return Results.Ok(worksheets);
            }
            catch (Exception ex)
            {
                return Results.Problem($"שגיאה בחיפוש דפי עבודה לפי קטגוריה: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("SearchWorksheetsByCategory")
        .WithTags("Worksheets")
        .Produces<List<object>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status500InternalServerError);



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
                    w.FileUrl,
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
        .Select(w => new 
        {
            Id=w.Id,
            Title = w.Title,
            CategoryName = w.Category.CategoryName,
            AverageRating = w.Ratings.Any() ? w.Ratings.Average(r => r.RatingValue) : 0,
            Url=w.FileUrl
        })
        .ToListAsync();

    return Results.Ok(worksheets);
})
.WithName("GetAllWorksheetsWithRatings")
.WithTags("Worksheets")
.Produces<List<object>>(StatusCodes.Status200OK);
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

    routes.MapGet("/api/worksheets/by-categories", async (ApplicationDbContext context) =>
{
    try
    {
        var categories = await context.Categories.ToListAsync();
        
        var results = new List<object>();
        
        foreach (var category in categories)
        {
            // בדיקה אם יש דפי עבודה בקטגוריה
            var worksheetCount = await context.Worksheets
                .Where(w => w.CategoryId == category.Id)
                .CountAsync();
                
            if (worksheetCount == 0)
            {
                // אין דפי עבודה בקטגוריה זו - נוסיף אובייקט עם מידע בסיסי
                results.Add(new
                {
                    CategoryId = category.Id,
                    CategoryName = category.CategoryName,
                    HasWorksheet = false,
                    Worksheet = (object)null
                });
                continue;
            }
            
            // בחירת מספר רנדומלי או פשוט לקחת את הראשון
            int skipCount = 0;
            if (worksheetCount > 1)
            {
                var random = new Random();
                skipCount = random.Next(0, worksheetCount);
            }
            
            var worksheet = await context.Worksheets
                .Where(w => w.CategoryId == category.Id)
                .Include(w => w.User)
                .Skip(skipCount)
                .Take(1)
                .Select(w => new
                {
                    w.Id,
                    w.Title,
                    w.FileUrl,
                    w.CategoryId,
                    w.AgeGroup,
                    w.Difficulty,
                    w.UserId,
                    UserName = w.User.FirstName
                })
                .FirstOrDefaultAsync();
                
            // הוספת התוצאה למיכל התוצאות
            results.Add(new
            {
                CategoryId = category.Id,
                CategoryName = category.CategoryName,
                HasWorksheet = true,
                Worksheet = worksheet,
                
            });
        }
        
        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        return Results.Problem($"שגיאה בקבלת דפי עבודה: {ex.Message}", statusCode: 500);
    }
})
.WithName("GetWorksheetsByCategories")
.WithTags("Worksheets")
.Produces<object>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);}

}