using Azurenaut.Services.Foundry;
using Microsoft.AspNetCore.Http;

namespace Azurenaut.Services.DataUpload;

/// <summary>
/// Interface for file upload operations to Azure AI Foundry Agent service
/// </summary>
public interface IDataUploadService
{
    /// <summary>
    /// Uploads a single file to Azure AI Foundry and optionally associates it with an agent
    /// </summary>
    /// <param name="file">The file to upload</param>
    /// <param name="agentId">Optional agent ID to associate the file with</param>
    /// <param name="fileName">Optional custom filename, uses original if not provided</param>
    /// <returns>File upload result with file ID and metadata</returns>
    Task<DataUploadResult> UploadFileAsync(IFormFile file, string? agentId = null, string? fileName = null);

    /// <summary>
    /// Uploads multiple files to Azure AI Foundry and optionally associates them with an agent
    /// </summary>
    /// <param name="files">The files to upload</param>
    /// <param name="agentId">Optional agent ID to associate the files with</param>
    /// <returns>Collection of file upload results</returns>
    Task<IEnumerable<DataUploadResult>> UploadFilesAsync(IFormFileCollection files, AgentThread agentThread);

    /// <summary>
    /// Uploads a file from a stream to Azure AI Foundry
    /// </summary>
    /// <param name="stream">The stream containing the file data</param>
    /// <param name="fileName">The name of the file</param>
    /// <param name="contentType">The content type of the file</param>
    /// <param name="agentId">Optional agent ID to associate the file with</param>
    /// <returns>File upload result with file ID and metadata</returns>
    Task<DataUploadResult> UploadFileFromStreamAsync(Stream stream, string fileName, string contentType, string? agentId = null);

    Task<string> ProcessMultipartReaderAsync(string boundry, Stream contentStream);
    Task<VectorStoreResult> GetOrCreateVectorStoreAsync(AgentThread agentThread);
    Task<StoreFile> CreateVectorStoreFilesAync(string fileId, string vectorStoreId);
    Task<AgentThread> UpdateAgentAsync(AgentThread agentThread, VectorStoreResult storeResult);
}