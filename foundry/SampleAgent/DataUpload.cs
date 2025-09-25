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

            if (!TryGetBoundary(req.ContentType, out var boundary))
            {
                return new BadRequestObjectResult("Request must be multipart/form-data with a boundary.");
            }

            var reader = new MultipartReader(boundary, req.Body);

            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync()) != null)
            {
                var contentDisposition = section.GetContentDispositionHeader();
                if (contentDisposition == null)
                {
                    continue;
                }

                // JSON form-data part (e.g., name="metadata"; filename absent)
                if (IsFormField(contentDisposition))
                {
                    // Process form field
                    var fieldName = contentDisposition.Name.Value;
                    using var streamReader = new StreamReader(section.Body);
                    var fieldValue = await streamReader.ReadToEndAsync();
                    _logger.LogInformation("Form field: {FieldName} = {FieldValue}", fieldName, fieldValue);
                }
                else if (IsFile(contentDisposition))
                {
                    // Process file
                    var fileName = contentDisposition.FileName.Value ?? contentDisposition.FileNameStar.Value;
                    if (string.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }

                    using var memoryStream = new MemoryStream();
                    await section.Body.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var formFile = new FormFile(memoryStream, 0, memoryStream.Length, contentDisposition.Name.Value, fileName)
                    {
                        Headers = new HeaderDictionary(),
                        ContentType = section.ContentType
                    };

                    // For this example, we assume no agent association
                    var uploadResult = await _dataUploadService.UploadFileAsync(formFile);
                    _logger.LogInformation("Uploaded file: {FileName}, FileId: {FileId}", uploadResult.FileName, uploadResult.FileId);
                }
            }

            // parse http request with content type application/json with T<AgentThread>
            string requestBody = await new StreamReader(req.).ReadToEndAsync();
            AgentThread requestAgentThread = JsonSerializer.Deserialize<AgentThread>(requestBody.);

            // Upload files
            var uploadResults = await _dataUploadService.UploadFilesAsync(req.Form.Files, requestAgentThread);
            var uploadResultsList = uploadResults.ToList();

            return new OkObjectResult(uploadResultsList);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload request");
            return new ObjectResult(new { Error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static bool TryGetBoundary(string? contentType, out string boundary)
    {
        boundary = string.Empty;

        if (string.IsNullOrWhiteSpace(contentType) ||
            !MediaTypeHeaderValue.TryParse(contentType, out var mediaType) ||
            string.IsNullOrEmpty(mediaType.Boundary.Value))
        {
            return false;
        }

        boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(boundary);
    }
    
    private static bool IsFormField(ContentDispositionHeaderValue disposition) =>
        disposition.DispositionType.Equals("form-data") &&
        string.IsNullOrEmpty(disposition.FileName.Value) &&
        string.IsNullOrEmpty(disposition.FileNameStar.Value);

    private static bool IsFile(ContentDispositionHeaderValue disposition) =>
        disposition.DispositionType.Equals("form-data") &&
        (!string.IsNullOrEmpty(disposition.FileName.Value) ||
         !string.IsNullOrEmpty(disposition.FileNameStar.Value));
}
