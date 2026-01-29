# Agent Service Client - API Documentation

This Azure Functions application provides a set of HTTP endpoints for managing AI agent conversations and file uploads for vector store integration.

## Table of Contents
- [Base URL](#base-url)
- [Authentication](#authentication)
- [Endpoints](#endpoints)
  - [StartAgent](#1-startagent)
  - [AssistantConversation](#2-assistantconversation)
  - [DataUpload](#3-dataupload)
- [Common Data Models](#common-data-models)

---

## Base URL

```
https://<your-function-app-name>.azurewebsites.net/api
```

For local development:
```
http://localhost:7071/api
```

---

## Authentication

- **StartAgent**: Requires function-level authorization (function key)
- **AssistantConversation**: Anonymous access
- **DataUpload**: Requires function-level authorization (function key)

Function keys can be obtained from the Azure Portal or using Azure CLI.

---

## Endpoints

### 1. StartAgent

Initializes or retrieves an AI agent and creates a conversation thread.

#### Endpoint
```
POST /api/StartAgent
```

#### Authorization
Function-level (requires function key)

#### Request Headers
```
Content-Type: application/json
x-functions-key: <your-function-key>
```

#### Request Body
```json
{
  "AgentId": "string",
  "ThreadId": "string"
}
```

**Fields:**
- `AgentId` (string, optional): The ID of an existing agent. If empty or invalid, a new agent will be created.
- `ThreadId` (string, optional): The ID of an existing thread. If empty or invalid, a new thread will be created.

#### Response
**Status Code:** `200 OK`

```json
{
  "Response": "Agent configured successfully.",
  "AgentThread": {
    "AgentId": "agent-abc123",
    "ThreadId": "thread-xyz789",
    "RunId": null,
    "Message": null,
    "Messages": null
  }
}
```

**Response Fields:**
- `Response` (string): Status message indicating the result of the operation
- `AgentThread` (object): Contains the agent and thread information
  - `AgentId` (string): The ID of the agent (newly created or existing)
  - `ThreadId` (string): The ID of the thread (newly created or existing)

#### Example Usage

```bash
curl -X POST https://<your-function-app>.azurewebsites.net/api/StartAgent \
  -H "Content-Type: application/json" \
  -H "x-functions-key: <your-function-key>" \
  -d '{
    "AgentId": "",
    "ThreadId": ""
  }'
```

---

### 2. AssistantConversation

Sends a message to an existing agent thread and retrieves the conversation history.

#### Endpoint
```
POST /api/AssistantConversation
```

#### Authorization
Anonymous (no function key required)

#### Request Headers
```
Content-Type: application/json
```

#### Request Body
```json
{
  "ThreadId": "thread-xyz789",
  "AgentId": "agent-abc123",
  "Message": "What is the weather like today?"
}
```

**Fields:**
- `ThreadId` (string, required): The ID of the conversation thread
- `AgentId` (string, required): The ID of the agent
- `Message` (string, required): The user's message to send to the agent

#### Response
**Status Code:** `200 OK`

```json
{
  "Response": "Message processed successfully",
  "AgentThread": {
    "ThreadId": "thread-xyz789",
    "AgentId": "agent-abc123",
    "RunId": "run-def456",
    "Message": null,
    "Messages": [
      {
        "role": "user",
        "content": "What is the weather like today?",
        "timestamp": "2026-01-29T10:30:00Z"
      },
      {
        "role": "assistant",
        "content": "I don't have access to real-time weather data...",
        "timestamp": "2026-01-29T10:30:05Z"
      }
    ]
  }
}
```

**Response Fields:**
- `Response` (string): Status message from the conversation
- `AgentThread` (object): Updated thread information
  - `ThreadId` (string): The thread ID
  - `AgentId` (string): The agent ID
  - `RunId` (string): The ID of the run that processed the message
  - `Messages` (array): Complete conversation history with all messages

#### Error Responses

**Status Code:** `400 Bad Request`
```json
{
  "error": "Failed to send message or start run."
}
```

**Status Code:** `404 Not Found`
```json
{
  "error": "No messages found in the thread."
}
```

#### Example Usage

```bash
curl -X POST https://<your-function-app>.azurewebsites.net/api/AssistantConversation \
  -H "Content-Type: application/json" \
  -d '{
    "ThreadId": "thread-xyz789",
    "AgentId": "agent-abc123",
    "Message": "Hello, can you help me?"
  }'
```

---

### 3. DataUpload

Uploads files to create or update a vector store for the agent. Supports file attachments for RAG (Retrieval-Augmented Generation) scenarios.

#### Endpoint
```
GET  /api/DataUpload  (Health Check)
POST /api/DataUpload  (File Upload)
```

#### Authorization
Function-level (requires function key)

---

#### GET Request (Health Check)

##### Request Headers
```
x-functions-key: <your-function-key>
```

##### Response
**Status Code:** `200 OK`

```json
{
  "Message": "Sample Upload service is running",
  "Timestamp": "2026-01-29T10:30:00Z",
  "Version": "1.0.0"
}
```

##### Example Usage
```bash
curl -X GET https://<your-function-app>.azurewebsites.net/api/DataUpload \
  -H "x-functions-key: <your-function-key>"
```

---

#### POST Request (File Upload)

##### Request Headers
```
Content-Type: multipart/form-data
x-functions-key: <your-function-key>
```

##### Request Body (multipart/form-data)

The request must include:
1. **agentThread** (form field): JSON string containing AgentThread information
2. **files** (form field): One or more files to upload

**agentThread field structure:**
```json
{
  "AgentId": "agent-abc123",
  "ThreadId": "thread-xyz789"
}
```

##### Response
**Status Code:** `200 OK`

The function returns an HTTP 200 status upon successful upload and vector store creation.

##### Error Response
**Status Code:** `500 Internal Server Error`

```json
{
  "Error": "Error message details"
}
```

**Status Code:** `400 Bad Request`

```json
{
  "Error": "Only GET and POST methods are supported"
}
```

##### Example Usage

```bash
curl -X POST https://<your-function-app>.azurewebsites.net/api/DataUpload \
  -H "x-functions-key: <your-function-key>" \
  -F 'agentThread={"AgentId":"agent-abc123","ThreadId":"thread-xyz789"}' \
  -F "file1=@/path/to/document1.pdf" \
  -F "file2=@/path/to/document2.txt"
```

##### Process Flow

1. Files are uploaded and stored
2. A vector store is created or retrieved for the agent thread
3. Files are processed and added to the vector store
4. The agent is updated with the vector store ID
5. Files become available for RAG queries in subsequent conversations

---

## Common Data Models

### AgentThread

```json
{
  "AgentId": "string",
  "ThreadId": "string",
  "RunId": "string",
  "Message": "string",
  "Messages": [
    {
      "role": "string",
      "content": "string",
      "timestamp": "string"
    }
  ]
}
```

### ClientResponse

```json
{
  "Response": "string",
  "AgentThread": {
    "AgentId": "string",
    "ThreadId": "string",
    "RunId": "string",
    "Message": "string",
    "Messages": []
  }
}
```

---

## Typical Workflow

1. **Initialize Agent**: Call `StartAgent` to create or retrieve an agent and thread
2. **Upload Files** (Optional): Use `DataUpload` to upload documents for RAG
3. **Start Conversation**: Send messages using `AssistantConversation`
4. **Continue Conversation**: Keep using the same `ThreadId` and `AgentId` for context

### Example Workflow

```bash
# Step 1: Initialize agent
RESPONSE=$(curl -X POST http://localhost:7071/api/StartAgent \
  -H "Content-Type: application/json" \
  -d '{"AgentId":"","ThreadId":""}')

AGENT_ID=$(echo $RESPONSE | jq -r '.AgentThread.AgentId')
THREAD_ID=$(echo $RESPONSE | jq -r '.AgentThread.ThreadId')

# Step 2: Upload supporting documents (optional)
curl -X POST http://localhost:7071/api/DataUpload \
  -F "agentThread={\"AgentId\":\"$AGENT_ID\",\"ThreadId\":\"$THREAD_ID\"}" \
  -F "file=@document.pdf"

# Step 3: Start conversation
curl -X POST http://localhost:7071/api/AssistantConversation \
  -H "Content-Type: application/json" \
  -d "{
    \"ThreadId\":\"$THREAD_ID\",
    \"AgentId\":\"$AGENT_ID\",
    \"Message\":\"Hello, can you analyze the uploaded document?\"
  }"
```

---

## Error Handling

All endpoints may return the following error responses:

- `400 Bad Request`: Invalid request payload or missing required fields
- `404 Not Found`: Resource not found (e.g., thread or agent doesn't exist)
- `500 Internal Server Error`: Server-side error during processing

Error responses typically include an `Error` or `error` field with a descriptive message.

---

## Additional Notes

- **Thread Persistence**: Threads maintain conversation history. Reuse the same `ThreadId` to continue a conversation.
- **File Formats**: The DataUpload endpoint supports various file formats for vector store ingestion (PDF, TXT, DOCX, etc.).
- **Rate Limiting**: Consider implementing rate limiting in production environments.
- **Logging**: All endpoints log diagnostic information for troubleshooting.

---

## Development

To run locally:
```bash
func start
```

To deploy to Azure:
```bash
func azure functionapp publish <your-function-app-name>
```

---

## License

See LICENSE file for details.
