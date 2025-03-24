using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public static class DownloadEndpoints
{
    public static void MapDownloadEndpoints(this IEndpointRouteBuilder routes)
    {


        routes.MapGet("/api/worksheets/{id}/download", [Authorize] async (int id, ApplicationDbContext context, HttpContext httpContext, [FromServices] IHttpClientFactory httpClientFactory) =>
        {
            // מציאת דף העבודה במסד הנתונים
            var worksheet = await context.Worksheets.FindAsync(id);
            if (worksheet == null)
                return Results.NotFound();

            if (string.IsNullOrEmpty(worksheet.FileUrl))
                return Results.NotFound("קישור הקובץ אינו זמין");
                
            try
            {
                // יצירת HttpClient להורדת הקובץ מ-S3
               var httpClient = httpClientFactory.CreateClient("S3Client");
                
                // הורדת הקובץ מה-S3
                var s3Response = await httpClient.GetAsync(worksheet.FileUrl);
                
                // בדיקה שהבקשה הצליחה
                if (!s3Response.IsSuccessStatusCode)
                    return Results.Problem($"לא ניתן להוריד את הקובץ מהשרת: {s3Response.StatusCode}");
                
                // קריאת תוכן הקובץ
                var fileBytes = await s3Response.Content.ReadAsByteArrayAsync();
                
                // חילוץ שם הקובץ מה-URL
                string fileName = Path.GetFileName(new Uri(worksheet.FileUrl).LocalPath);
                
                // זיהוי סוג הקובץ (MIME type)
                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(fileName, out string contentType))
                {
                    // אם לא הצלחנו לזהות את סוג הקובץ, ננסה לקבל אותו מהתגובה של S3
                    contentType = s3Response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                }

                // שם הקובץ להורדה - משתמשים בשם מותאם אישית אם קיים
                string downloadName = string.IsNullOrEmpty(worksheet.Title) 
                    ? fileName 
                    : worksheet.Title;
                
                // תיעוד ההורדה (אופציונלי)

                // החזרת הקובץ להורדה
                return Results.File(fileBytes, contentType, downloadName);
            }
            catch (HttpRequestException ex)
            {
                return Results.Problem($"שגיאה בהורדת הקובץ: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"שגיאה בלתי צפויה: {ex.Message}");
            }
        })
        .WithName("DownloadWorksheet")
        .WithTags("Worksheets")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status500InternalServerError);
        
        // נקודת קצה כללית להורדת קובץ מ-S3 לפי URL
        routes.MapGet("/api/files/download", [Authorize] async ([FromQuery] string fileUrl, [FromQuery] string filename, HttpContext httpContext, [FromServices] IHttpClientFactory httpClientFactory) =>
        {
            if (string.IsNullOrEmpty(fileUrl))
                return Results.BadRequest("חסר קישור לקובץ");
                
            try
            {
                // יצירת HttpClient להורדת הקובץ
                var httpClient = httpClientFactory.CreateClient();
                
                // הורדת הקובץ מה-S3 או כל מקור אחר
                var response = await httpClient.GetAsync(fileUrl);
                
                // בדיקה שהבקשה הצליחה
                if (!response.IsSuccessStatusCode)
                    return Results.Problem($"לא ניתן להוריד את הקובץ מהשרת: {response.StatusCode}");
                
                // קריאת תוכן הקובץ
                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                
                // קביעת שם להורדה
                string downloadName = string.IsNullOrEmpty(filename)
                    ? Path.GetFileName(new Uri(fileUrl).LocalPath)
                    : filename;
                
                // זיהוי סוג הקובץ (MIME type)
                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(downloadName, out string contentType))
                {
                    contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                }
                
                // החזרת הקובץ להורדה
                return Results.File(fileBytes, contentType, downloadName);
            }
            catch (HttpRequestException ex)
            {
                return Results.Problem($"שגיאה בהורדת הקובץ: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"שגיאה בלתי צפויה: {ex.Message}");
            }
        })
        .WithName("DownloadFileFromUrl")
        .WithTags("Files")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);
    }
    
    // פונקציה לתיעוד הורדות (אופציונלי)
}