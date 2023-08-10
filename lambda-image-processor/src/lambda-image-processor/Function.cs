using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using image_processor_classlib.utils;
using Newtonsoft.Json;

[assembly: LambdaSerializer(
    typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer)
)]

namespace lambda_image_processor;

public class Function
{
    private static readonly string _prefix = "assets";
    private static readonly string _bucketLocationName = "us-east-2";
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
            string signatureSecret = Environment.GetEnvironmentVariable("SIGNATURE_SECRET");
            string postProcessorURI = "";
            string mediaKey = "";
            string objectURL = "";

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
                // context.Logger.LogInformation($"Key is: {objectKey}. Content Type is: {contentType}");
                // Console.WriteLine($"Key is: {objectKey}. Content Type is: {contentType}");
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
                var guid = Guid.NewGuid().ToString();
                mediaKey = response.Metadata["x-amz-meta-external-id"] ?? "";
                postProcessorURI = response.Metadata["x-amz-meta-post-processing-url"] ?? "";
                var key = response.Metadata["x-amz-meta-preferred-key"] ?? $"{_prefix}/{guid}.webp";
                // var bucketLocationResponse = await S3Client.GetBucketLocationAsync(
                //     new GetBucketLocationRequest { BucketName = bucketName }
                // );
                // objectURL =
                //     $"https://{bucketName}.s3.{bucketLocationResponse.Location.Value}.amazonaws.com/{key}";

                objectURL = $"https://{bucketName}.s3.{_bucketLocationName}.amazonaws.com/{key}";
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

                await this.FireAndForget(
                    postProcessorURI,
                    new
                    {
                        key = mediaKey,
                        status = "SUCCESS",
                        objectURL
                    },
                    signatureSecret
                );
            }
            catch (Exception e)
            {
                // context.Logger.LogError(
                //     $"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function."
                // );
                context.Logger.LogError(e.Message);
                // context.Logger.LogError(e.StackTrace);
                await this.FireAndForget(
                    postProcessorURI,
                    new
                    {
                        key = mediaKey,
                        status = "FAILED",
                        objectURL
                    },
                    signatureSecret
                );
                throw;
            }
        }
    }

    private async Task FireAndForget(string uri, object payload, string? signature)
    {
        try
        {
            Uri parsedUri;
            if (
                Uri.TryCreate(uri, UriKind.Absolute, out parsedUri)
                && (parsedUri.Scheme == Uri.UriSchemeHttp || parsedUri.Scheme == Uri.UriSchemeHttps)
                && signature?.Length > 1
            )
            {
                string payloadJSON = JsonConvert.SerializeObject(payload);
                byte[] secretBytes = Encoding.UTF8.GetBytes(signature);
                using HMACSHA256 hmac = new HMACSHA256(secretBytes);
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJSON));
                string xsignature =
                    "sha256=" + BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                StringContent content = new StringContent(
                    payloadJSON,
                    Encoding.UTF8,
                    "application/json"
                );
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                httpClient.DefaultRequestHeaders.Add("X-Signature", xsignature);
                var response = await httpClient.PostAsync(uri, content);
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
