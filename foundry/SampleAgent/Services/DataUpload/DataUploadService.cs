using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Runtime.InteropServices;
using Azure.AI.Projects;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Azure;
using OpenAI.VectorStores;
using System.Text.Json;
using Foundry;
using Microsoft.AspNetCore.Http.Features;

namespace SampleAgent.Services.DataUpload;

public static class DataUploadServiceExtension
{
    public static void AddDataUploadService(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentConfig>(configuration.GetSection("AIFoundry"));
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = Int32.MaxValue;
            options.MultipartBoundaryLengthLimit = Int32.MaxValue;
            options.MultipartHeadersCountLimit = Int32.MaxValue;
            options.MultipartHeadersLengthLimit = Int32.MaxValue;
            options.BufferBodyLengthLimit = Int32.MaxValue;
            options.KeyLengthLimit = Int32.MaxValue;
            options.MemoryBufferThreshold = Int32.MaxValue;
            options.ValueCountLimit = Int32.MaxValue;
            options.ValueLengthLimit = Int32.MaxValue;
        });
        services.AddSingleton<IDataUploadService, DataUploadService>();
    }
}

/// <summary>
/// Service for uploading files to Azure AI Foundry Agent service
/// </summary>
public class DataUploadService : IDataUploadService
{
    private readonly ILogger<DataUploadService> _logger;
    private readonly PersistentAgentsClient _client;
    private readonly AgentConfig _config;

    // Supported file types for Azure AI Foundry
    private static readonly HashSet<string> SupportedFileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".pdf", ".docx", ".doc", ".rtf", ".html", ".htm", ".xml", ".json", ".csv",
        ".xlsx", ".xls", ".pptx", ".ppt", ".py", ".js", ".ts", ".cs", ".java", ".cpp", ".h",
        ".c", ".rb", ".go", ".php", ".sql", ".sh", ".yaml", ".yml", ".toml", ".ini", ".cfg"
    };

    private const long MaxFileSize = 500 * 1024 * 1024; // 500MB max file size for Azure AI Foundry

    public DataUploadService(ILogger<DataUploadService> logger, IOptions<AgentConfig> options)
    {
        _logger = logger;
        _config = options.Value;

        if (!string.IsNullOrEmpty(_config.Endpoint))
            _client = new PersistentAgentsClient(_config.Endpoint, new DefaultAzureCredential());

    }

    public async Task<DataUploadResult> UploadFileAsync(IFormFile file, string? agentId = null, string? fileName = null)
    {
        if (file == null || file.Length == 0)
        {
            return new DataUploadResult
            {
                Status = UploadStatus.Failed,
                ErrorMessage = "File is null or empty"
            };
        }

        var actualFileName = file.FileName;
        var fileExtension = Path.GetExtension(actualFileName).ToLowerInvariant();

        // Validate file type
        if (!SupportedFileTypes.Contains(fileExtension))
        {
            return new DataUploadResult
            {
                FileName = file.FileName,
                Status = UploadStatus.Failed,
                ErrorMessage = $"File type '{fileExtension}' is not supported"
            };
        }

        // Validate file size
        if (file.Length > MaxFileSize)
        {
            return new DataUploadResult
            {
                FileName = actualFileName,
                FileSizeBytes = file.Length,
                Status = UploadStatus.Failed,
                ErrorMessage = $"File size ({file.Length} bytes) exceeds maximum allowed size ({MaxFileSize} bytes)"
            };
        }

        try
        {
            _logger.LogInformation("Starting upload of file: {FileName} (Size: {FileSize} bytes)",
                actualFileName, file.Length);

            using var stream = file.OpenReadStream();
            return await UploadFileFromStreamAsync(stream, actualFileName, file.ContentType, agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", actualFileName);
            return new DataUploadResult
            {
                FileName = actualFileName,
                FileSizeBytes = file.Length,
                ContentType = file.ContentType,
                Status = UploadStatus.Failed,
                ErrorMessage = $"Upload failed: {ex.Message}"
            };
        }
    }

    public async Task<IEnumerable<DataUploadResult>> UploadFilesAsync(IFormFileCollection files, AgentThread agentThread)
    {
        if (files == null || files.Count == 0)
        {
            return new[] { new DataUploadResult { Status = UploadStatus.Failed, ErrorMessage = "No files provided" } };
        }

        _logger.LogInformation("Starting upload of {FileCount} files", files.Count);

        var uploadTasks = files.Select(file => UploadFileAsync(file, agentThread.AgentId));
        var results = await Task.WhenAll(uploadTasks);

        var successCount = results.Count(r => r.Status == UploadStatus.Completed);
        var failureCount = results.Length - successCount;

        _logger.LogInformation("Completed upload of {FileCount} files. Success: {SuccessCount}, Failed: {FailureCount}",
            files.Count, successCount, failureCount);

        return results;
    }

    public async Task<DataUploadResult> UploadFileFromStreamAsync(Stream stream, string fileName, string contentType, string? agentId = null)
    {
        try
        {
            _logger.LogInformation("Uploading file from stream: {FileName}", fileName);

            var uploadData = await _client.Files.UploadFileAsync(
                data: stream,
                purpose: "assistants",
                filename: fileName
            );

            var result = new DataUploadResult
            {
                FileId = uploadData.Value.Id,
                FileName = uploadData.Value.Filename,
                FileSizeBytes = uploadData.Value.Size,
                ContentType = contentType,
                UploadedAt = DateTime.UtcNow,
                Status = uploadData.Value.Status == "uploaded" ? UploadStatus.Completed : UploadStatus.Pending,
                AssociatedAgentId = agentId
            };

            _logger.LogInformation("Successfully uploaded file: {FileId} - {FileName}",
                result.FileId, fileName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file from stream: {FileName}", fileName);
            return new DataUploadResult
            {
                FileName = fileName,
                ContentType = contentType,
                Status = UploadStatus.Failed,
                ErrorMessage = $"Upload failed: {ex.Message}"
            };
        }
    }

    public async Task<string> ProcessMultipartReaderAsync(string boundry, Stream contentStream)
    {
        var reader = new MultipartReader(boundry, contentStream);

        MultipartSection? section;

        //process each section in multipart body
        while ((section = await reader.ReadNextSectionAsync()) != null)
        {
            var contentDisposition = section.GetContentDispositionHeader();
            if (contentDisposition != null && contentDisposition.IsFileDisposition())
            {
                _logger.LogInformation($"Processing file: {contentDisposition.FileName.Value}");
            }
            else if (contentDisposition != null && contentDisposition.IsFormDisposition())
            {
                AgentThread requestAgentThread = JsonSerializer.Deserialize<AgentThread>(section.Body);
                _logger.LogInformation("ThreadID: {ThreadId} -- AgentId: {AgenetId}", requestAgentThread.ThreadId, requestAgentThread.AgentId);
            }
        }

        return "good";
    }

}

/* /**
MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync()) != null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition))
                {
                    _logger.LogWarning("Skipping section with invalid content-disposition header.");
                    continue;
                }

                var fieldName = HeaderUtilities.RemoveQuotes(contentDisposition.Name).Value;

                // JSON form-data part (e.g., name="metadata"; filename absent)
                if (IsFormField(contentDisposition) && fieldName.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    requestAgentThread = JsonSerializer.Deserialize<AgentThread>(section.Body);
                    _logger.LogInformation("ThreadID: {ThreadId} -- AgentId: {AgenetId}", requestAgentThread.ThreadId, requestAgentThread.AgentId);
                }
                else if (IsFile(contentDisposition))
                {
                    // Process file
                    var fileName = contentDisposition.FileName.Value ?? contentDisposition.FileNameStar.Value;
                    _logger.LogInformation("#########Uploaded file: {FileName})", fileName);

                    using var memoryStream = new MemoryStream();
                    await section.Body.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var formFile = new FormFile(memoryStream, 0, memoryStream.Length, contentDisposition.Name.Value, fileName)
                    {
                        Headers = new HeaderDictionary(),
                        ContentType = section.ContentType
                    };
                    _logger.LogInformation(" file: {FileName}, FileId: {FileId}", formFile.FileName, formFile.Name);

                    var uploadResult = await _dataUploadService.UploadFileAsync(formFile);
                    _fileUploadMetadata = new FileUploadMetadata
                    {
                        FileName = uploadResult.FileName,
                        ContentType = uploadResult.ContentType,
                        FileSizeBytes = uploadResult.FileSizeBytes
                    };
                    _logger.LogInformation("Uploaded file: {FileName}, FileId: {FileId}", uploadResult.FileName, uploadResult.FileId);
                }
            } */