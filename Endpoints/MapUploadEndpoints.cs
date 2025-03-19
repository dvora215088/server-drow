using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/upload/presigned-url", [Authorize] async (IAmazonS3 s3Client, string fileName, HttpContext httpContext) =>
        {
            // שם הדלי יכול להיות מוגדר בקונפיגורציה או כאן באופן סטטי
            string bucketName = "drows-testpnoren"; 
            
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = fileName,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(5),
                ContentType = "png/pdf" // או סוג הקובץ המתאים לדפי עבודה
            };

            string url = s3Client.GetPreSignedURL(request);
            
            return Results.Ok(new { url });
        })
        .WithName("GetPresignedUrl")
        .WithTags("Upload")
        .Produces<object>(StatusCodes.Status200OK);
    }
}