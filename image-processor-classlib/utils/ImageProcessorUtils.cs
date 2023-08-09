using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.Formats.Webp;

namespace image_processor_classlib.utils;

public class ImageProcessorUtils
{
    public static async Task<FileContentResult?> ConvertImageToWebP(
        Stream inputStream,
        MemoryStream outputStream,
        bool returnFileResponse = false
    )
    {
        await Task.CompletedTask;
        using var originalImage = await Image.LoadAsync(inputStream);
        var webPEncoder = new WebpEncoder();
        await originalImage.SaveAsync(outputStream, webPEncoder);

        if (returnFileResponse)
        {
            return new FileContentResult(outputStream.ToArray(), "image/webp");
        }
        return null;
    }
}
