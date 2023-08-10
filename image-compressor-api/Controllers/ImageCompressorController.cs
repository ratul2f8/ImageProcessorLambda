using System;
using image_processor_classlib.utils;
using Microsoft.AspNetCore.Mvc;

namespace image_compressor_api.Controllers;

[ApiController]
[Route("image-processing")]
public class ImageCompressorController : ControllerBase
{
    [HttpPost("compress-and-convert")]
    public async Task<FileContentResult?> GetCompressedWebP(
        [FromForm(Name = "image")] IFormFile file
    )
    {
        using var inputStream = file.OpenReadStream();
        using var outputStream = new MemoryStream();
        return await ImageProcessorUtils.ConvertImageToWebP(inputStream, outputStream, true);
    }
}
