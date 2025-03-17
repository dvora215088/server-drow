using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
// יצירת קטגוריית קובץ חדשה
public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder routes)
    {
        // קבלת כל הקטגוריות
        routes.MapGet("/api/categories", async (ApplicationDbContext context) =>
        {
            var categories = await context.Categories.ToListAsync();
            return Results.Ok(categories);
        })
        .WithName("GetAllCategories")
        .WithTags("Categories")
        .Produces<List<Category>>(StatusCodes.Status200OK);
          // קבלת קטגוריה לפי מזהה
        routes.MapGet("/api/categories/{id}", async (int id, ApplicationDbContext context) =>
        {
            var category = await context.Categories.FindAsync(id);
            if (category == null)
                return Results.NotFound();
                
            return Results.Ok(category);
        })
        .WithName("GetCategoryById")
        .WithTags("Categories")
        .Produces<Category>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        
        // יצירת קטגוריה חדשה - רק למנהל
        routes.MapPost("/api/categories", [Authorize(Roles = "Admin")] async (Category category, ApplicationDbContext context) =>
        {
            await context.Categories.AddAsync(category);
            await context.SaveChangesAsync();
            
            return Results.Created($"/api/categories/{category.Id}", category);
        })
        .WithName("CreateCategory")
        .WithTags("Categories")
        .Produces<Category>(StatusCodes.Status201Created);
        
        // עדכון קטגוריה - רק למנהל
        routes.MapPut("/api/categories/{id}", [Authorize(Roles = "Admin")] async (int id, Category updatedCategory, ApplicationDbContext context) =>
        {
            var category = await context.Categories.FindAsync(id);
            if (category == null)
                return Results.NotFound();
                
            category.CategoryName = updatedCategory.CategoryName;
            category.Description = updatedCategory.Description;
            
            await context.SaveChangesAsync();
            return Results.Ok(category);
        })
        .WithName("UpdateCategory")
        .WithTags("Categories")
        .Produces<Category>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        
        // מחיקת קטגוריה - רק למנהל
        routes.MapDelete("/api/categories/{id}", [Authorize(Roles = "Admin")] async (int id, ApplicationDbContext context) =>
        {
            var category = await context.Categories.FindAsync(id);
            if (category == null)
                return Results.NotFound();
                
            context.Categories.Remove(category);
            await context.SaveChangesAsync();
            
            return Results.NoContent();
        })
        .WithName("DeleteCategory")
        .WithTags("Categories")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
    }

