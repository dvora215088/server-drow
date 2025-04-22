using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/upload/presigned-url", [Authorize] async (IAmazonS3 s3Client, string fileName, HttpContext httpContext) =>
        {
            string bucketName = "drows.testpnoren";

            var contentType = GetMimeType(fileName); // ✨ קביעת סוג MIME לפי סיומת

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = fileName,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(5),
                ContentType = contentType
            };

            string url = s3Client.GetPreSignedURL(request);
            return Results.Ok(new { url });
        })
        .WithName("GetPresignedUrl")
        .WithTags("Upload")
        .Produces<object>(StatusCodes.Status200OK);
    }

    // ✨ פונקציה חדשה לזיהוי סוג MIME לפי סיומת הקובץ
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
