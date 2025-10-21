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
using System.Text.Json;
using Foundry;
using Microsoft.AspNetCore.Http.Features;

namespace SampleAgent.Services.DataUpload;

public static class DataUploadServiceExtension
{
    /// <summary>
    /// Add Data Upload Service to DI container
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
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
    private PersistentAgent _agent;

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

    /// <summary>
    /// Upload a single file to Azure AI Foundry
    /// </summary>
    /// <param name="file"></param>
    /// <param name="agentId"></param>
    /// <param name="fileName"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Upload multiple files to Azure AI Foundry
    /// </summary>
    /// <param name="files"></param>
    /// <param name="agentThread"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Upload a file from a stream to Azure AI Foundry
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="fileName"></param>
    /// <param name="contentType"></param>
    /// <param name="agentId"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Process multipart reader to extract files and form data
    /// </summary>
    /// <param name="boundry"></param>
    /// <param name="contentStream"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Create a vector store in Azure AI Foundry
    /// </summary>
    /// <returns></returns>
    public async Task<VectorStoreResult> CreateVectorStoreAsync()
    {
        VectorStoreDataSource dataSource = new VectorStoreDataSource(
            assetIdentifier: "sd",
            assetType: VectorStoreDataSourceAssetType.UriAsset
        );

        var vectorStore = await _client.VectorStores.CreateVectorStoreAsync(
            name: System.Guid.NewGuid().ToString(),
            storeConfiguration: new VectorStoreConfiguration(
                dataSources: [dataSource]
            )
        );

        return new VectorStoreResult
        {
            VectorStoreId = vectorStore.Value.Id,
            Name = vectorStore.Value.Name
        };
    }

    /// <summary>
    /// Update the agent to include the file search tool with the new vector store
    /// </summary>
    /// <param name="agentThread"></param>
    /// <param name="storeResult"></param>
    /// <returns></returns>
    public async Task<AgentThread> UpdateAgent(AgentThread agentThread, VectorStoreResult storeResult)
    {
        FileSearchToolResource fileSearchResource = new([storeResult.VectorStoreId], null);
        List<ToolDefinition> tools = [new FileSearchToolDefinition()];

        _agent = await _client.Administration.UpdateAgentAsync(
            assistantId: agentThread.AgentId,
            tools: tools,
            toolResources: new ToolResources
            {
                FileSearch = fileSearchResource
            }

        );

        return agentThread;
    }

}