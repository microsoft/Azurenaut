using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Text.Json;
using System.IO;

using Azurenaut.Services.DataUpload;
using Azurenaut.Services.Foundry;

namespace Azurenaut;

public class DataUpload
{
    private readonly ILogger<DataUpload> _logger;
    private readonly IDataUploadService _dataUploadService;
    private FileUploadMetadata _fileUploadMetadata;
    private AgentThread _agentThread;

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
                return new BadRequestObjectResult(new { Error = "Only GET and POST methods are supported" });

            var r = await req.ReadFormAsync();
            _agentThread = JsonSerializer.Deserialize<AgentThread>(r["agentThread"]);
            _logger.LogInformation("Processing file upload for ThreadID: {ThreadId} -- AgentId: {AgenetId}", _agentThread.AgentId, _agentThread.ThreadId);

            VectorStoreResult vectorStoreResult = await _dataUploadService.GetOrCreateVectorStoreAsync(_agentThread);
            _logger.LogInformation("Vector Store ID: {VectorStoreId}", vectorStoreResult.VectorStoreId);

            _logger.LogInformation("Starting file upload processing for {FileCount} files", req.Form.Files.Count);
            var uploadResult = await _dataUploadService.UploadFilesAsync(req.Form.Files, _agentThread);
            var uploadResultList = uploadResult.ToList();
            
            _logger.LogInformation("Completed file upload processing. Success: {SuccessCount}, Total: {TotalCount}",
                uploadResultList.Count(r => r.Status == UploadStatus.Completed),
                uploadResultList.Count);
            int successCount = uploadResultList.Count(r => r.Status == UploadStatus.Completed);
            var successfulUploads = uploadResultList.Where(r => r.Status == UploadStatus.Completed)
                                                    .Select(r => r.FileId)
                                                    .ToList();


            if (successfulUploads.Count() > 0)
            {
                var createVectorStoreFileTasks = successfulUploads.Select(id => _dataUploadService.CreateVectorStoreFilesAync(id, vectorStoreResult.VectorStoreId)); ;
                var createdStoreFiles = await Task.WhenAll(createVectorStoreFileTasks);
            }

            var updateAgent = await _dataUploadService.UpdateAgentAsync(_agentThread, vectorStoreResult);
            _logger.LogInformation("AgentThread updated with Vector Store ID: {VectorStoreId}", vectorStoreResult.VectorStoreId);

            return new OkResult( );

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload request");
            return new ObjectResult(new { Error = ex.Message }) { StatusCode = 500 };
        }
    }
    
    
}
