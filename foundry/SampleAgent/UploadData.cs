using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SampleAgent.Services.UploadData;
using System.Text.Json;

namespace SampleAgent;

/// <summary>
/// Azure Function for handling file uploads to Azure AI Foundry Agent service
/// </summary>
public class UploadData
{
    private readonly ILogger<UploadData> _logger;
    private readonly IDataUploadService _fileUploadService;

    public UploadData(ILogger<UploadData> logger, IDataUploadService fileUploadService)
    {
        _logger = logger;
        _fileUploadService = fileUploadService;
    }

    /// <summary>
    /// HTTP trigger function for uploading single or multiple files to Azure AI Foundry
    /// Supports GET for health check and POST for file uploads
    /// </summary>
    /// <param name="req">HTTP request containing files and optional parameters</param>
    /// <returns>File upload results</returns>
    [Function("UploadData")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("File upload request received: {Method} {Path}", req.Method, req.Path);

        try
        {
            // Handle GET request for health check
            if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return new OkObjectResult(new { 
                    Message = "Azure AI Foundry File Upload Service is running", 
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0"
                });
            }

            // Handle POST request for file uploads
            if (!req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult(new { Error = "Only GET and POST methods are supported" });
            }

            // Check if request has files
            if (req.Form?.Files == null || req.Form.Files.Count == 0)
            {
                return new BadRequestObjectResult(new { Error = "No files provided in the request" });
            }

            // Extract optional parameters from query string or form data
            var agentId = req.Query["agentId"].FirstOrDefault() ?? req.Form["agentId"].FirstOrDefault();
            var createVectorStore = bool.TryParse(req.Query["createVectorStore"].FirstOrDefault() ?? req.Form["createVectorStore"].FirstOrDefault(), out var cvs) && cvs;
            var vectorStoreName = req.Query["vectorStoreName"].FirstOrDefault() ?? req.Form["vectorStoreName"].FirstOrDefault() ?? $"vectorstore_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            _logger.LogInformation("Processing {FileCount} files. AgentId: {AgentId}, CreateVectorStore: {CreateVectorStore}", 
                req.Form.Files.Count, agentId ?? "None", createVectorStore);

            // Upload files
            var uploadResults = await _fileUploadService.UploadMultipleFilesAsync(req.Form.Files, agentId);
            var uploadResultsList = uploadResults.ToList();

            // Create response
            var response = new MultiFileUploadResponse
            {
                FileResults = uploadResultsList,
                Success = uploadResultsList.Any(r => r.Status == UploadStatus.Completed),
                SuccessfulUploads = uploadResultsList.Count(r => r.Status == UploadStatus.Completed),
                FailedUploads = uploadResultsList.Count(r => r.Status == UploadStatus.Failed),
                ErrorMessages = uploadResultsList.Where(r => r.Status == UploadStatus.Failed)
                                               .Select(r => $"{r.FileName}: {r.ErrorMessage}")
                                               .ToList()
            };

            // Create vector store if requested and there are successful uploads
            if (createVectorStore && response.SuccessfulUploads > 0)
            {
                var successfulFileIds = uploadResultsList.Where(r => r.Status == UploadStatus.Completed)
                                                        .Select(r => r.FileId)
                                                        .ToList();

                if (successfulFileIds.Any())
                {
                    _logger.LogInformation("Creating vector store '{VectorStoreName}' with {FileCount} files", 
                        vectorStoreName, successfulFileIds.Count);

                    var vectorStoreResult = await _fileUploadService.CreateVectorStoreAsync(successfulFileIds, vectorStoreName);
                    response.VectorStore = vectorStoreResult;
                }
            }

            // Log summary
            _logger.LogInformation("File upload completed. Success: {SuccessCount}, Failed: {FailedCount}, VectorStore: {VectorStoreCreated}", 
                response.SuccessfulUploads, response.FailedUploads, response.VectorStore != null ? "Created" : "None");

            // Return appropriate status code
            if (response.Success)
            {
                return new OkObjectResult(response);
            }
            else if (response.SuccessfulUploads > 0)
            {
                // Partial success
                return new ObjectResult(response) { StatusCode = 207 }; // Multi-Status
            }
            else
            {
                // Complete failure
                return new BadRequestObjectResult(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload request");
            return new ObjectResult(new { 
                Error = "Internal server error occurred while processing file upload", 
                Details = ex.Message,
                Timestamp = DateTime.UtcNow
            }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// HTTP trigger function for getting information about uploaded files
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>List of files or specific file information</returns>
    [Function("GetFiles")]
    public async Task<IActionResult> GetFiles([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _logger.LogInformation("Get files request received");

        try
        {
            var fileId = req.Query["fileId"].FirstOrDefault();

            if (!string.IsNullOrEmpty(fileId))
            {
                // Get specific file information
                var fileInfo = await _fileUploadService.GetFileInfoAsync(fileId);
                if (fileInfo == null)
                {
                    return new NotFoundObjectResult(new { Error = $"File with ID '{fileId}' not found" });
                }
                return new OkObjectResult(fileInfo);
            }
            else
            {
                // List all files
                var files = await _fileUploadService.ListFilesAsync();
                return new OkObjectResult(new { Files = files, Count = files.Count() });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file information");
            return new ObjectResult(new { 
                Error = "Internal server error occurred while retrieving file information", 
                Details = ex.Message 
            }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// HTTP trigger function for deleting uploaded files
    /// </summary>
    /// <param name="req">HTTP request with fileId parameter</param>
    /// <returns>Deletion result</returns>
    [Function("DeleteFile")]
    public async Task<IActionResult> DeleteFile([HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequest req)
    {
        _logger.LogInformation("Delete file request received");

        try
        {
            var fileId = req.Query["fileId"].FirstOrDefault();
            if (string.IsNullOrEmpty(fileId))
            {
                return new BadRequestObjectResult(new { Error = "fileId parameter is required" });
            }

            var success = await _fileUploadService.DeleteFileAsync(fileId);
            
            if (success)
            {
                return new OkObjectResult(new { Message = $"File '{fileId}' deleted successfully" });
            }
            else
            {
                return new ObjectResult(new { Error = $"Failed to delete file '{fileId}'" }) { StatusCode = 500 };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file");
            return new ObjectResult(new { 
                Error = "Internal server error occurred while deleting file", 
                Details = ex.Message 
            }) { StatusCode = 500 };
        }
    }
}
