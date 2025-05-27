// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.StaticFiles;
// using System.IO;
// using System.Net.Http;
// using System.Threading.Tasks;

// public static class DownloadEndpoints
// {
//     public static void MapDownloadEndpoints(this IEndpointRouteBuilder routes)
//     {


//         routes.MapGet("/api/worksheets/{id}/download", [Authorize] async (int id, ApplicationDbContext context, HttpContext httpContext, [FromServices] IHttpClientFactory httpClientFactory) =>
//         {
//             // מציאת דף העבודה במסד הנתונים
//             var worksheet = await context.Worksheets.FindAsync(id);
//             if (worksheet == null)
//                 return Results.NotFound();

//             if (string.IsNullOrEmpty(worksheet.FileUrl))
//                 return Results.NotFound("קישור הקובץ אינו זמין");
                
//             try
//             {
//                 // יצירת HttpClient להורדת הקובץ מ-S3
//                var httpClient = httpClientFactory.CreateClient("S3Client");
                
//                 // הורדת הקובץ מה-S3
//                 var s3Response = await httpClient.GetAsync(worksheet.FileUrl);
                
//                 // בדיקה שהבקשה הצליחה
//                 if (!s3Response.IsSuccessStatusCode)
//                     return Results.Problem($"לא ניתן להוריד את הקובץ מהשרת: {s3Response.StatusCode}");
                
//                 // קריאת תוכן הקובץ
//                 var fileBytes = await s3Response.Content.ReadAsByteArrayAsync();
                
//                 // חילוץ שם הקובץ מה-URL
//                 string fileName = Path.GetFileName(new Uri(worksheet.FileUrl).LocalPath);
                
//                 // זיהוי סוג הקובץ (MIME type)
//                 var provider = new FileExtensionContentTypeProvider();
//                 if (!provider.TryGetContentType(fileName, out string contentType))
//                 {
//                     // אם לא הצלחנו לזהות את סוג הקובץ, ננסה לקבל אותו מהתגובה של S3
//                     contentType = s3Response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
//                 }

//                 // שם הקובץ להורדה - משתמשים בשם מותאם אישית אם קיים
//                 string downloadName = string.IsNullOrEmpty(worksheet.Title) 
//                     ? fileName 
//                     : worksheet.Title;
                
//                 // תיעוד ההורדה (אופציונלי)

//                 // החזרת הקובץ להורדה
//                 return Results.File(fileBytes, contentType, downloadName);
//             }
//             catch (HttpRequestException ex)
//             {
//                 return Results.Problem($"שגיאה בהורדת הקובץ: {ex.Message}");
//             }
//             catch (Exception ex)
//             {
//                 return Results.Problem($"שגיאה בלתי צפויה: {ex.Message}");
//             }
//         })
//         .WithName("DownloadWorksheet")
//         .WithTags("Worksheets")
//         .Produces(StatusCodes.Status200OK)
//         .Produces(StatusCodes.Status404NotFound)
//         .Produces(StatusCodes.Status500InternalServerError);
        
//         // נקודת קצה כללית להורדת קובץ מ-S3 לפי URL
//         routes.MapGet("/api/files/download", [Authorize] async ([FromQuery] string fileUrl, [FromQuery] string filename, HttpContext httpContext, [FromServices] IHttpClientFactory httpClientFactory) =>
//         {
//             if (string.IsNullOrEmpty(fileUrl))
//                 return Results.BadRequest("חסר קישור לקובץ");
                
//             try
//             {
//                 // יצירת HttpClient להורדת הקובץ
//                 var httpClient = httpClientFactory.CreateClient();
                
//                 // הורדת הקובץ מה-S3 או כל מקור אחר
//                 var response = await httpClient.GetAsync(fileUrl);
                
//                 // בדיקה שהבקשה הצליחה
//                 if (!response.IsSuccessStatusCode)
//                     return Results.Problem($"לא ניתן להוריד את הקובץ מהשרת: {response.StatusCode}");
                
//                 // קריאת תוכן הקובץ
//                 var fileBytes = await response.Content.ReadAsByteArrayAsync();
                
//                 // קביעת שם להורדה
//                 string downloadName = string.IsNullOrEmpty(filename)
//                     ? Path.GetFileName(new Uri(fileUrl).LocalPath)
//                     : filename;
                
//                 // זיהוי סוג הקובץ (MIME type)
//                 var provider = new FileExtensionContentTypeProvider();
//                 if (!provider.TryGetContentType(downloadName, out string contentType))
//                 {
//                     contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
//                 }
                
//                 // החזרת הקובץ להורדה
//                 return Results.File(fileBytes, contentType, downloadName);
//             }
//             catch (HttpRequestException ex)
//             {
//                 return Results.Problem($"שגיאה בהורדת הקובץ: {ex.Message}");
//             }
//             catch (Exception ex)
//             {
//                 return Results.Problem($"שגיאה בלתי צפויה: {ex.Message}");
//             }
//         })
//         .WithName("DownloadFileFromUrl")
//         .WithTags("Files")
//         .Produces(StatusCodes.Status200OK)
//         .Produces(StatusCodes.Status404NotFound)
//         .Produces(StatusCodes.Status400BadRequest)
//         .Produces(StatusCodes.Status500InternalServerError);
//     }
    
// }
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public static class DownloadEndpoints
{
    private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
    private static readonly string[] ALLOWED_S3_DOMAINS = { "your-bucket.s3.amazonaws.com", "your-cdn-domain.com" };
    
    public static void MapDownloadEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/worksheets/{id}/download", [Authorize] async (int id, ApplicationDbContext context, HttpContext httpContext, [FromServices] IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
        {
            var worksheet = await context.Worksheets.FindAsync(new object[] { id }, cancellationToken);
            if (worksheet == null)
                return Results.NotFound();

            if (string.IsNullOrEmpty(worksheet.FileUrl))
                return Results.NotFound("קישור הקובץ אינו זמין");

            // בדיקת תקינות URL
            if (!IsValidS3Url(worksheet.FileUrl))
                return Results.BadRequest("קישור קובץ לא תקין");
                
            try
            {
                using var httpClient = httpClientFactory.CreateClient("S3Client");
                
                // הגדרת timeout ו-cancellation
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMinutes(2));
                
                using var s3Response = await httpClient.GetAsync(worksheet.FileUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                
                if (!s3Response.IsSuccessStatusCode)
                    return Results.Problem($"לא ניתן להוריד את הקובץ מהשרת: {s3Response.StatusCode}");
                
                // בדיקת גודל הקובץ
                if (s3Response.Content.Headers.ContentLength.HasValue && 
                    s3Response.Content.Headers.ContentLength.Value > MAX_FILE_SIZE)
                {
                    return Results.BadRequest("הקובץ גדול מדי להורדה");
                }

                string fileName = Path.GetFileName(new Uri(worksheet.FileUrl).LocalPath);
                
                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(fileName, out string contentType))
                {
                    contentType = s3Response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                }

                string downloadName = SanitizeFileName(string.IsNullOrEmpty(worksheet.Title) 
                    ? fileName 
                    : worksheet.Title);
                
                // הורדה באמצעות stream במקום טעינה לזיכרון
                var stream = await s3Response.Content.ReadAsStreamAsync(cts.Token);
                
                return Results.Stream(stream, contentType, downloadName);
            }
            catch (OperationCanceledException)
            {
                return Results.Problem("פג הזמן להורדת הקובץ");
            }
            catch (HttpRequestException ex)
            {
                return Results.Problem($"שגיאה בהורדת הקובץ: {ex.Message}");
            }
            catch (Exception ex)
            {
                // לוג מפורט לשרת, הודעה כללית למשתמש
                // logger.LogError(ex, "Unexpected error downloading worksheet {Id}", id);
                return Results.Problem("שגיאה בהורדת הקובץ");
            }
        })
        .WithName("DownloadWorksheet")
        .WithTags("Worksheets")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);
        
        // נקודת קצה בטוחה להורדת קבצים (רק עם validation מחמיר)
        routes.MapGet("/api/files/download", [Authorize] async ([FromQuery] string fileUrl, [FromQuery] string filename, HttpContext httpContext, [FromServices] IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(fileUrl))
                return Results.BadRequest("חסר קישור לקובץ");

            // בדיקת תקינות URL - רק S3 מאושר
            if (!IsValidS3Url(fileUrl))
                return Results.BadRequest("קישור קובץ לא תקין או לא מאושר");
                
            try
            {
                using var httpClient = httpClientFactory.CreateClient("S3Client");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMinutes(2));
                
                using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                
                if (!response.IsSuccessStatusCode)
                    return Results.Problem($"לא ניתן להוריד את הקובץ מהשרת: {response.StatusCode}");
                
                // בדיקת גודל הקובץ
                if (response.Content.Headers.ContentLength.HasValue && 
                    response.Content.Headers.ContentLength.Value > MAX_FILE_SIZE)
                {
                    return Results.BadRequest("הקובץ גדול מדי להורדה");
                }
                
                string downloadName = string.IsNullOrEmpty(filename)
                    ? SanitizeFileName(Path.GetFileName(new Uri(fileUrl).LocalPath))
                    : SanitizeFileName(filename);
                
                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(downloadName, out string contentType))
                {
                    contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                }
                
                var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                return Results.Stream(stream, contentType, downloadName);
            }
            catch (OperationCanceledException)
            {
                return Results.Problem("פג הזמן להורדת הקובץ");
            }
            catch (HttpRequestException ex)
            {
                return Results.Problem($"שגיאה בהורדת הקובץ: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Results.Problem("שגיאה בהורדת הקובץ");
            }
        })
        .WithName("DownloadFileFromUrl")
        .WithTags("Files")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);
    }
    
    private static bool IsValidS3Url(string url)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
            
        return uri.Scheme == "https" && 
               ALLOWED_S3_DOMAINS.Any(domain => uri.Host.Contains(domain, StringComparison.OrdinalIgnoreCase));
    }
    
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "download";
            
        // הסרת תווים לא חוקיים משם הקובץ
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        return string.IsNullOrEmpty(sanitized) ? "download" : sanitized;
    }
}