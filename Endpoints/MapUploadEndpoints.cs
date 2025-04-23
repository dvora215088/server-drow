using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.IO;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/upload/presigned-url", [Authorize] async (IAmazonS3 s3Client, string fileName, HttpContext httpContext) =>
        {
            string bucketName = "drows.testpnoren";  // שם ה-bucket ב-S3
            var contentType = GetMimeType(fileName);  // זיהוי סוג הקובץ

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = fileName,  // שם הקובץ ב-S3
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(5),  // תוקף ה-presigned URL
                ContentType = contentType
            };

            // יצירת ה-presigned URL
            string url = s3Client.GetPreSignedURL(request);

            // החזרת ה-URL ל-Frontend
            return Results.Ok(new { url, key = fileName });
        })
        .WithName("GetPresignedUrl")
        .WithTags("Upload")
        .Produces<object>(StatusCodes.Status200OK);
    }

    // ✨ פונקציה לזיהוי סוג MIME לפי סיומת הקובץ
    private static string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLower();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}
