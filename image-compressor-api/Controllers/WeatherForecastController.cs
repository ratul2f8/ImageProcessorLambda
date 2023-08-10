using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace image_compressor_api.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing",
        "Bracing",
        "Chilly",
        "Cool",
        "Mild",
        "Warm",
        "Balmy",
        "Hot",
        "Sweltering",
        "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable
            .Range(1, 5)
            .Select(
                index =>
                    new WeatherForecast
                    {
                        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        TemperatureC = Random.Shared.Next(-20, 55),
                        Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                    }
            )
            .ToArray();
    }

    [HttpGet("fire-and-forget")]
    public async Task<string> fireAndForgetController()
    {
        var httpTestService = new HttpTestService();
        await httpTestService.fireAndForget(
            "http://localhost:8080/v1/media-trace/update-status",
            new
            {
                key = "test",
                status = "SUCCESS",
                objectURL = "https://www.google.com"
            },
            "ff8dd99b-abe0-44c6-89c5-d7b4d47924a4"
        );
        return "Done";
    }
}

public class HttpTestService
{
    public async Task fireAndForget(string uri, object payload, string? signature)
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
