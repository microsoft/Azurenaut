namespace SampleAgent.Services.DataUpload;

/// <summary>
/// Represents the result of a file upload operation
/// </summary>
public class DataUploadResult
{
    /// <summary>
    /// Unique identifier for the uploaded file
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Original filename
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Size of the uploaded file in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Content type of the uploaded file
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the file was uploaded
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Status of the upload operation
    /// </summary>
    public UploadStatus Status { get; set; }

    /// <summary>
    /// Error message if upload failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Agent ID if the file was associated with an agent
    /// </summary>
    public string? AssociatedAgentId { get; set; }

    /// <summary>
    /// Additional metadata about the file
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents the result of a vector store creation operation
/// </summary>
public class VectorStoreResult
{
    /// <summary>
    /// Unique identifier for the created vector store
    /// </summary>
    public string VectorStoreId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the vector store
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// File IDs included in the vector store
    /// </summary>
    public IEnumerable<string> FileIds { get; set; } = Enumerable.Empty<string>();

    /// <summary>
    /// Status of the vector store creation
    /// </summary>
    public VectorStoreStatus Status { get; set; }

    /// <summary>
    /// Timestamp when the vector store was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Error message if creation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents information about an uploaded file
/// </summary>
public class FileInfo
{
    /// <summary>
    /// Unique identifier for the file
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Original filename
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Size of the file in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Content type of the file
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the file was created/uploaded
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Purpose of the file (e.g., "assistants")
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// Status of the file
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Request model for uploading multiple files
/// </summary>
public class MultiFileUploadRequest
{
    /// <summary>
    /// Optional agent ID to associate the files with
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Whether to create a vector store with the uploaded files
    /// </summary>
    public bool CreateVectorStore { get; set; } = false;

    /// <summary>
    /// Name for the vector store if CreateVectorStore is true
    /// </summary>
    public string? VectorStoreName { get; set; }

    /// <summary>
    /// Maximum allowed file size in bytes (default: 100MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Allowed file extensions (if empty, all types allowed)
    /// </summary>
    public HashSet<string> AllowedExtensions { get; set; } = new();
}

/// <summary>
/// Response model for multiple file uploads
/// </summary>
public class MultiFileUploadResponse
{
    /// <summary>
    /// Collection of individual file upload results
    /// </summary>
    public IEnumerable<DataUploadResult> FileResults { get; set; } = Enumerable.Empty<DataUploadResult>();

    /// <summary>
    /// Vector store result if one was created
    /// </summary>
    public VectorStoreResult? VectorStore { get; set; }

    /// <summary>
    /// Overall success status
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Count of successfully uploaded files
    /// </summary>
    public int SuccessfulUploads { get; set; }

    /// <summary>
    /// Count of failed uploads
    /// </summary>
    public int FailedUploads { get; set; }

    /// <summary>
    /// General error messages
    /// </summary>
    public IEnumerable<string> ErrorMessages { get; set; } = Enumerable.Empty<string>();
}

/// <summary>
/// Status enumeration for upload operations
/// </summary>
public enum UploadStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Status enumeration for vector store operations
/// </summary>
public enum VectorStoreStatus
{
    InProgress,
    Completed,
    Failed,
    Expired
}

public record FileUploadMetadata
{
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
}

public enum StoreFileStatus
{
    in_progress,
    complete,
    cancelled,
    failed
}
/// <summary>
/// Represents a vectore store file attached to a vectore store
/// </summary>
public class StoreFile
{
    /// <summary>
    /// Associated vectore store ID
    /// </summary>
    public string VectorStoreId { get; set; } = string.Empty;
    /// <summary>
    /// Associated file ID
    /// </summary>
    public string FileId { get; set; } = string.Empty;
    /// <summary>
    /// Timestamp when the vectore store file was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Status of the vectore store file
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Last error message if any
    /// </summary>
    public string? LastErrorMessage { get; set; }

}