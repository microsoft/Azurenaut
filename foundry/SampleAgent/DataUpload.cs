using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Text.Json;
using System.IO;


using Foundry;

namespace SampleAgent.Services.DataUpload;

public class DataUpload
{
    private readonly ILogger<DataUpload> _logger;
    private readonly IDataUploadService _dataUploadService;
    private FileUploadMetadata _fileUploadMetadata;

    public DataUpload(ILogger<DataUpload> logger, IDataUploadService dataUploadService)
    {
        _logger = logger;
        _dataUploadService = dataUploadService;
    }

    [Function("DataUpload")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("File upload request received: {Method} {Path}", req.Method, req.Path);

        try
        {
            // Handle GET request for health check
            if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return new OkObjectResult(new
                {
                    Message = "Sample Upload service is running",
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0"
                });
            }

            // Handle POST request for file uploads
            if (!req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult(new { Error = "Only GET and POST methods are supported" });
            }

            var boundry = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(req.ContentType).Boundary).Value;
            if (string.IsNullOrWhiteSpace(boundry))
            {
                return new BadRequestObjectResult("Request must be multipart/form-data with a boundary.");
            }

            _logger.LogInformation("@@@@@@@@@@ Boundry: {Boundry}", boundry);
            var upd = _dataUploadService.ProcessMultipartReaderAsync(boundry, req.Body);

            return new OkObjectResult(upd);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload request");
            return new ObjectResult(new { Error = ex.Message }) { StatusCode = 500 };
        }
    }
    
    
}
