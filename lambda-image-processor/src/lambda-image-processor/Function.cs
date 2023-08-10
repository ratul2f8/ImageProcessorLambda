using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using image_processor_classlib.utils;

[assembly: LambdaSerializer(
    typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer)
)]

namespace lambda_image_processor;

public class Function
{
    private static readonly string prefix = "assets";
    IAmazonS3 S3Client { get; set; }

    public Function()
    {
        S3Client = new AmazonS3Client();
    }

    public Function(IAmazonS3 s3Client)
    {
        this.S3Client = s3Client;
    }

    public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        var eventRecords = evnt.Records ?? new List<S3Event.S3EventNotificationRecord>();
        foreach (var record in eventRecords)
        {
            var s3Event = record.S3;
            if (s3Event == null)
            {
                continue;
            }

            try
            {
                var bucketName = s3Event.Bucket.Name;
                var objectKey = s3Event.Object.Key;
                var response = await S3Client.GetObjectMetadataAsync(bucketName, objectKey);
                var contentType = response.Headers.ContentType;
                // context.Logger.LogDebug($"Key is: {objectKey}. Content Type is: {contentType}");
                Console.WriteLine($"Key is: {objectKey}. Content Type is: {contentType}");
                if (
                    !response.Headers.ContentType.Contains("image/")
                    || response.Metadata["x-amz-meta-processed"] == true.ToString()
                )
                {
                    continue;
                }
                using var inputStream = await S3Client.GetObjectStreamAsync(
                    bucketName,
                    objectKey,
                    new Dictionary<string, object>()
                );
                using var outputStream = new MemoryStream();
                await ImageProcessorUtils.ConvertImageToWebP(inputStream, outputStream);
                var key =
                    response.Metadata["x-amz-preferred-key"] ?? $"{prefix}/{Guid.NewGuid()}.webp";
                await S3Client.PutObjectAsync(
                    new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = key,
                        Metadata = { ["x-amz-meta-processed"] = true.ToString() },
                        ContentType = "image/webp",
                        InputStream = outputStream
                    }
                );
            }
            catch (Exception e)
            {
                // context.Logger.LogError(
                //     $"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function."
                // );
                context.Logger.LogError(e.Message);
                // context.Logger.LogError(e.StackTrace);
                throw;
            }
        }
    }
}
