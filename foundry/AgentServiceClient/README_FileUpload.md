# Azure AI Foundry File Upload Service

A comprehensive .NET 8.0 solution for uploading files to Azure AI Foundry Agent service, built as Azure Functions with support for single and multiple file uploads, vector store creation, and file management.

## Features

- ✅ **Single and Multiple File Uploads**: Upload one or more files in a single request
- ✅ **Vector Store Integration**: Automatically create vector stores from uploaded files for enhanced AI search
- ✅ **File Type Validation**: Support for common file types (txt, md, pdf, docx, json, csv, etc.)
- ✅ **Size Validation**: Configurable file size limits (default: 500MB)
- ✅ **Agent Association**: Associate uploaded files with specific AI agents
- ✅ **File Management**: List, retrieve, and delete uploaded files
- ✅ **Comprehensive Error Handling**: Detailed error messages and status codes
- ✅ **Async/Await Pattern**: Fully asynchronous operations for better performance

## Architecture

### Core Components

1. **IFileUploadService** - Interface defining file upload operations
2. **FileUploadService** - Main service implementation with Azure AI Foundry integration
3. **FileUploadModels** - Request/response models and enums
4. **UploadData** - Azure Functions HTTP triggers for API endpoints

### Supported File Types

```
Text Files: .txt, .md, .rtf, .html, .htm, .xml
Documents: .pdf, .docx, .doc, .pptx, .ppt
Data Files: .json, .csv, .xlsx, .xls
Code Files: .py, .js, .ts, .cs, .java, .cpp, .h, .c, .rb, .go, .php, .sql, .sh
Config Files: .yaml, .yml, .toml, .ini, .cfg
```

## Installation and Setup

### 1. Prerequisites

- .NET 8.0 SDK
- Azure Functions Core Tools
- Azure AI Foundry workspace
- Visual Studio Code or Visual Studio 2022

### 2. Configuration

Update your `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AIFoundry__Endpoint": "https://your-foundry-endpoint.cognitiveservices.azure.com/",
    "AIFoundry__ApiKey": "your-api-key",
    "AIFoundry__Model": "gpt-4",
    "AIFoundry__Instructions": "You are a helpful assistant.",
    "AIFoundry__AgentNamePrefix": "FileUploadAgent",
    "AIFoundry__ThreadNamePrefix": "FileThread"
  }
}
```

### 3. Build and Run

```bash
# Build the project
dotnet build

# Run locally
func start

# Or run with Visual Studio Code
# Press F5 or use Run and Debug
```

## API Endpoints

### 1. Health Check
```http
GET http://localhost:7071/api/UploadData
```

**Response:**
```json
{
  "message": "Azure AI Foundry File Upload Service is running",
  "timestamp": "2025-09-23T10:30:00Z",
  "version": "1.0.0"
}
```

### 2. Upload Single File
```http
POST http://localhost:7071/api/UploadData
Content-Type: multipart/form-data

[file data]
```

**Optional Query Parameters:**
- `agentId` - Associate file with specific agent
- `createVectorStore` - Create vector store from uploaded files
- `vectorStoreName` - Custom name for vector store

### 3. Upload Multiple Files
```http
POST http://localhost:7071/api/UploadData?createVectorStore=true&vectorStoreName=my_knowledge_base
Content-Type: multipart/form-data

[multiple files data]
```

### 4. List Files
```http
GET http://localhost:7071/api/GetFiles
```

### 5. Get File Information
```http
GET http://localhost:7071/api/GetFiles?fileId={file-id}
```

### 6. Delete File
```http
DELETE http://localhost:7071/api/DeleteFile?fileId={file-id}
```

## Usage Examples

### C# Code Example

```csharp
// Inject the service
private readonly IFileUploadService _fileUploadService;

// Upload a single file
public async Task<FileUploadResult> UploadDocument(IFormFile file, string agentId)
{
    var result = await _fileUploadService.UploadFileAsync(file, agentId);
    
    if (result.Status == UploadStatus.Completed)
    {
        Console.WriteLine($"File uploaded successfully: {result.FileId}");
    }
    else
    {
        Console.WriteLine($"Upload failed: {result.ErrorMessage}");
    }
    
    return result;
}

// Upload multiple files and create vector store
public async Task<MultiFileUploadResponse> UploadDocuments(
    IFormFileCollection files, 
    string vectorStoreName)
{
    var uploadResults = await _fileUploadService.UploadMultipleFilesAsync(files);
    
    var successfulFileIds = uploadResults
        .Where(r => r.Status == UploadStatus.Completed)
        .Select(r => r.FileId);
    
    var vectorStore = await _fileUploadService.CreateVectorStoreAsync(
        successfulFileIds, 
        vectorStoreName);
    
    return new MultiFileUploadResponse
    {
        FileResults = uploadResults,
        VectorStore = vectorStore,
        Success = successfulFileIds.Any()
    };
}
```

### PowerShell Example

```powershell
# Upload a single file
$uri = "http://localhost:7071/api/UploadData"
$filePath = "C:\\temp\\document.pdf"

$form = @{
    file = Get-Item -Path $filePath
}

$response = Invoke-RestMethod -Uri $uri -Method Post -Form $form
Write-Output "File ID: $($response.fileResults[0].fileId)"
```

### curl Example

```bash
# Upload file with vector store creation
curl -X POST "http://localhost:7071/api/UploadData?createVectorStore=true&vectorStoreName=my_docs" \
  -F "file=@./document.pdf" \
  -F "agentId=my-agent-123"
```

## Response Models

### FileUploadResult
```json
{
  "fileId": "file_abc123",
  "fileName": "document.pdf",
  "fileSizeBytes": 1048576,
  "contentType": "application/pdf",
  "uploadedAt": "2025-09-23T10:30:00Z",
  "status": "Completed",
  "errorMessage": null,
  "associatedAgentId": "agent-123",
  "metadata": {
    "purpose": "assistants",
    "originalContentType": "application/pdf"
  }
}
```

### MultiFileUploadResponse
```json
{
  "fileResults": [...],
  "vectorStore": {
    "vectorStoreId": "vs_xyz789",
    "name": "my_knowledge_base",
    "fileIds": ["file_abc123", "file_def456"],
    "status": "InProgress",
    "createdAt": "2025-09-23T10:30:00Z"
  },
  "success": true,
  "successfulUploads": 2,
  "failedUploads": 0,
  "errorMessages": []
}
```

## Error Handling

The service provides comprehensive error handling with appropriate HTTP status codes:

- **200 OK** - Successful operation
- **207 Multi-Status** - Partial success (some files uploaded, some failed)
- **400 Bad Request** - Invalid request (no files, unsupported type, etc.)
- **404 Not Found** - File not found
- **500 Internal Server Error** - Server error

## Testing

Use the included `fileupload.http` file with Visual Studio Code REST Client extension or similar tool to test all endpoints.

## Important Notes

### Current Implementation Status

⚠️ **This is a foundation implementation** - The actual Azure AI Foundry API calls are currently placeholder implementations. You'll need to:

1. **Replace placeholder API calls** with actual Azure AI Foundry SDK calls
2. **Verify correct enum values** for `PersistentAgentFilePurpose`
3. **Update vector store creation** with proper `VectorStoreConfiguration`
4. **Test with your specific Azure AI Foundry workspace**

### TODOs for Production Use

```csharp
// Replace these placeholder sections in FileUploadService.cs:

// 1. Actual file upload
var uploadedFile = await _client.Files.UploadFileAsync(
    data: stream,
    purpose: /* correct enum value */,
    filename: fileName
);

// 2. Vector store creation
var vectorStore = await _client.VectorStores.CreateVectorStoreAsync(
    name: vectorStoreName,
    fileIds: fileIdList,
    configuration: new VectorStoreConfiguration(/* proper config */)
);

// 3. File operations
var file = await _client.Files.GetFileAsync(fileId);
var files = await _client.Files.GetFilesAsync(/* correct purpose */);
await _client.Files.DeleteFileAsync(fileId);
```

## Security Considerations

- Use Azure Key Vault for storing API keys in production
- Implement proper authentication and authorization
- Validate file content in addition to type and size
- Consider virus scanning for uploaded files
- Implement rate limiting for production use

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.