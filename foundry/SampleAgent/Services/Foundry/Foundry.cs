using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using Azure.AI.Projects;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Azure;
using System.IO;
using OpenAI.VectorStores;

namespace Foundry
{
    public static class AgentServiceExtension
    {
        /// <summary>
        /// Adds the AI Foundry agent service to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add the service to.</param>
        /// <param name="configuration">The configuration containing the AI Foundry settings.</param>
       /// <remarks>
        /// This extension method is used to add the AI Foundry agent service to the ASP.NET Core dependency injection container.
        /// It reads the configuration settings from the "AIFoundry" section of the provided configuration.
        /// The settings include the endpoint, API key, model, instructions, agent name prefix, and thread name prefix.
        /// The service is registered as a singleton, meaning that a single instance will be used throughout the application.
        /// If the endpoint is not configured, an exception is thrown to indicate that the AI Foundry client cannot be initialized.
        /// </remarks>
        /// <exception cref="System.Exception">Thrown if the AI Foundry client is not initialized due to missing configuration.</exception>
        /// <example>
        /// <code>
        /// services.AddAgentService(Configuration);
        /// </code>
        /// </example>
        /// <returns>The updated service collection.</returns>
        public static void AddAgentService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AgentConfig>(configuration.GetSection("AIFoundry"));
            services.AddSingleton<IAgentService, AgentService>();
        }
    }

    public class AgentService : IAgentService
    {
        private readonly ILogger<AgentService> _logger;
        private string _endpoint;
        private string _apiKey;
        private string _model;
        private string _agentName;
        private string _instructions;
        private string _agentNamePrefix;
        private string _threadNamePrefix;
        private string _blobUri;

        private PersistentAgentsClient _client;
        private PersistentAgent _agent;
        private PersistentAgentThread _thread;
        private PersistentThreadMessage _message;
        private ThreadRun _run;

        private ThreadMessages _threadMessages;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentService"/> class.
        /// </summary>
        /// <param name="options">The options containing the AI Foundry configuration.</param>
        /// <remarks>
        /// This constructor initializes the AI Foundry agent service with the provided configuration options.
        /// It sets up the endpoint, API key, model, instructions, agent name prefix, and thread name prefix.
        /// If the endpoint is not configured, it throws an exception to indicate that the AI Foundry client cannot be initialized.
        /// </remarks>
        /// <exception cref="System.Exception">Thrown if the AI Foundry client is not initialized due to missing configuration.</exception>
        /// <returns>An instance of the <see cref="AgentService"/> class.</returns>     
        public AgentService(ILogger<AgentService> logger, IOptions<AgentConfig> options)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));

            var config = options.Value;
            if (!string.IsNullOrEmpty(config.Endpoint))
            {
                _endpoint = config.Endpoint;
                _client = new PersistentAgentsClient(_endpoint, new DefaultAzureCredential());
            }
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                _apiKey = config.ApiKey;
            }
            if (!string.IsNullOrEmpty(config.Model))
            {
                _model = config.Model;
            }
            if (!string.IsNullOrEmpty(config.Instructions))
            {
                _instructions = config.Instructions;
            }
            if (!string.IsNullOrEmpty(config.AgentNamePrefix))
            {
                _agentNamePrefix = config.AgentNamePrefix;
            }
            if (!string.IsNullOrEmpty(config.ThreadNamePrefix))
            {
                _threadNamePrefix = config.ThreadNamePrefix;
            }
            if (!string.IsNullOrEmpty(config.BlobUri))
            {
                _blobUri = config.BlobUri;
            }
            if (_client == null)
            {
                throw new System.Exception("AI Foundry client is not initialized. Please check your configuration.");
            }
        }

        /// <summary>
        /// Configures the assistant environment by creating or retrieving an agent and a thread.
        /// </summary>
        /// <param name="agentId">The ID of the agent to retrieve or create.</param>
        /// <param name="threadId">The ID of the thread to retrieve or create.</param>
        /// <returns>A `ClientResponse` object containing the response message and the agent's thread information.</returns>
        /// <remarks>
        /// This method is used to set up the assistant environment by either creating a new agent and thread or retrieving existing ones.
        /// It logs the start of the configuration process and checks if the agent and thread IDs are provided.
        /// If the IDs are not provided, it creates new ones using the AI Foundry service's administration client.
        /// If the IDs are provided, it retrieves the existing agent and thread.
        /// The method returns a `ClientResponse` object containing the response message and the agent's thread information.
        /// </remarks>
        /// <exception cref="System.Exception">Thrown if the agent or thread cannot be created or retrieved.</exception>
        /// <returns>A `ClientResponse` object containing the response message and the agent's thread information.</returns>
        public async Task<ClientResponse> ConfigureAssistantEnvironment(string agentId, string threadId)
        {
            _logger.LogInformation("Starting Agent configuration.");

            var agentFoundryResponse = await GetOrCreateAgentAsync(agentId);
            if (string.IsNullOrEmpty(agentFoundryResponse.AgentThread.AgentId))
            {
                _logger.LogError("Failed to create or retrieve agent.");
                return new ClientResponse
                {
                    Response = "Failed to create or retrieve agent.",
                    AgentThread = null
                };
                
            }

            var threadFoundryResponse = await GetOrCreateThreadAsync(threadId);
            if (string.IsNullOrEmpty(threadFoundryResponse.AgentThread.ThreadId))
            {
                _logger.LogError("Failed to create or retrieve thread.");
                return new ClientResponse
                {
                    Response = "Failed to create or retrieve thread.",
                    AgentThread = null
                };
            }

            _logger.LogInformation("Agent and Thread retrieved or created.");
            // You can implement logic for GET requests here if needed
            return new ClientResponse
            {
                Response = "Agent and thread successfully configured.",
                AgentThread = new AgentThread
                {
                    AgentId = agentFoundryResponse.AgentThread.AgentId,
                    ThreadId = threadFoundryResponse.AgentThread.ThreadId
                }
            };
        }

        /// <summary>
        /// Creates or retrieves an agent with the specified ID.
        /// If no ID is provided, a new agent is created with a unique name.
        /// If an ID is provided, the existing agent is retrieved.
        /// </summary>
        /// <param name="agentId">The agent ID to retrive</param>
        /// <returns>A string response from the AI Foundry service.</returns>
        /// <remarks>
        /// This method checks if an agent ID is provided. If not, it creates a new agent with a unique name based on 
        /// the configured prefix and a GUID. If an ID is provided, it retrieves the existing agent.
        /// The agent is created or retrieved using the AI Foundry service's administration client.
        /// The agent's model and instructions are set based on the configuration.
        /// The method returns a `ClientResponse` object containing the response message and the agent's thread information.
        /// </remarks>
        private async Task<ClientResponse> GetOrCreateAgentAsync([Optional] string agentId)
        {
            if (string.IsNullOrEmpty(agentId))
            {
                // Console.WriteLine("File URI: " + _blobUri);
                // var vectorStoreDataSource = new VectorStoreDataSource(
                //     assetIdentifier: _blobUri,
                //     assetType: VectorStoreDataSourceAssetType.UriAsset
                // ); 

                // PersistentAgentsVectorStore vectorStore = await _client.VectorStores.CreateVectorStoreAsync(
                //     name: "sample_vector_store",
                //     storeConfiguration: new VectorStoreConfiguration(
                //         dataSources: [vectorStoreDataSource]
                //     ));

                // FileSearchToolResource fileSearchResource = new([vectorStore.Id], null);

                // List<ToolDefinition> tools = [new FileSearchToolDefinition()];

                _agent = await _client.Administration.CreateAgentAsync(
                    name: $"{_agentNamePrefix}_{System.Guid.NewGuid()}",
                    model: _model,
                    instructions: _instructions
                //                   tools: tools,
                //                   toolResources: new ToolResources() { FileSearch = fileSearchResource }
                );
            }
            else
                _agent = await _client.Administration.GetAgentAsync(agentId);

            return new ClientResponse
            {
                Response = $"Agent created or retrieved: {_agent.Name} with ID {_agent.Id}",
                AgentThread = new AgentThread
                {
                    AgentId = _agent.Id,
                    ThreadId = _thread?.Id // Assuming thread is set elsewhere
                }
            };
        }

        /// <summary>
        /// Creates or retrieves a thread with the specified ID.
        /// If no ID is provided, a new thread is created.
        /// If an ID is provided, the existing thread is retrieved.
        /// </summary>
        /// <param name="threadId">The thread ID to retrieve</param>
        /// <returns>A string response from the AI Foundry service.</returns>
        /// <remarks>
        /// This method checks if a thread ID is provided. If not, it creates a new thread.
        /// If an ID is provided, it retrieves the existing thread.
        /// The thread is created or retrieved using the AI Foundry service's threads client.
        /// The method returns a `ClientResponse` object containing the response message and the agent's thread information.
        /// </remarks>
        private async Task<ClientResponse> GetOrCreateThreadAsync([Optional] string threadId)
        {
            if (string.IsNullOrEmpty(threadId))
                _thread = await _client.Threads.CreateThreadAsync();
            else
                _thread = await _client.Threads.GetThreadAsync(threadId);

            return new ClientResponse
            {
                Response = $"Thread created or retrieved: with ID {_thread.Id}",
                AgentThread = new AgentThread
                {
                    AgentId = _agent.Id,
                    ThreadId = _thread.Id
                }
            };
        }

        /// <summary>
        /// Creates a message in the specified thread with the provided content.
        /// </summary>
        /// <param name="threadId">The ID of the thread where the message will be created.</param>
        /// <param name="messageContent">The content of the message to be created.</param>
        /// <returns>A `ClientResponse` object containing the response message and the agent's thread information.</returns>
        /// <remarks>
        /// This method checks if the thread ID is provided. If not, it throws an exception.
        /// If the thread ID is valid, it creates a message in the specified thread using the AI Foundry service's messages client. 
        /// The message is created with the role of `User` and the provided content.
        /// The method returns a `ClientResponse` object containing the response message and the agent's thread information.
        /// </remarks>
        public async Task<ClientResponse> CreateThreadMessage(string threadId, string messageContent)
        {
            if (threadId == null)
            {
                throw new System.Exception("Thread is not initialized. Please create or retrieve a thread first.");
            }

            _message = await _client.Messages.CreateMessageAsync(
                threadId,
                MessageRole.User,
                messageContent
            );

            return new ClientResponse
            {
                Response = $"Message created in thread:  with ID {_message.Id}",
                AgentThread = new AgentThread
                {
                    ThreadId = threadId
                }
            };
        }
    
        /// <summary>
        /// Starts a run for the specified thread and agent.
        /// </summary>
        /// <param name="threadId">The ID of the thread to start the run in.</param>
        /// <param name="agentId">The ID of the agent to start the run with.</param>
        /// <param name="additionalInstructions">Optional additional instructions for the run.</param>
        /// <returns>A `ClientResponse` object containing the response message and the agent's thread information.</returns>
        /// <remarks>
        /// This method checks if the thread ID and agent ID are provided. If not, it throws an exception.
        /// If both IDs are valid, it starts a run using the AI Foundry service's runs client.
        /// The run is created with the specified thread ID and agent ID.
        /// The method waits for the run to complete by polling the run status every 500 milliseconds.
        /// Once the run is complete, it returns a `ClientResponse` object containing the response message and the agent's thread information.
        /// </remarks>
        public async Task<ClientResponse> StartRunAsync(string threadId, string agentId, [Optional] string additionalInstructions)
        {
            
            if (string.IsNullOrEmpty(threadId) || string.IsNullOrEmpty(agentId))
            {
                throw new System.Exception("Thread ID or Agent ID is not provided. Please provide valid IDs.");
            }

            _run = await _client.Runs.CreateRunAsync(
                threadId,
                agentId
            );

            // Wait for the run to complete
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                _run = await _client.Runs.GetRunAsync(threadId, _run.Id);
            }
            while (_run.Status == RunStatus.Queued
                || _run.Status == RunStatus.InProgress
                || _run.Status == RunStatus.RequiresAction);

            return new ClientResponse
            {
                Response = $"Run comepleted for thread: {threadId}. Run message: {_run}",
                AgentThread = new AgentThread
                {
                    AgentId = agentId,
                    ThreadId = threadId,
                    RunId = _run.Id
                }
            };
        }

        /// <summary> 
        /// Retrieves all messages from a specified thread.
        /// </summary>
        /// <param name="threadId">The ID of the thread from which to retrieve messages.</param>
        /// <returns>A `ClientResponse` object containing the first message from the thread and the thread information.</returns>   
        /// <remarks>
        /// This method checks if the thread ID is provided. If not, it throws an exception.
        /// If the thread ID is valid, it retrieves all messages from the specified thread using the AI Foundry service's messages client.
        /// The messages are retrieved in ascending order.
        /// The method iterates through the messages and adds their content to a `ThreadMessages` object.
        /// Finally, it returns a `ClientResponse` object containing the first message from the thread and the thread information.
        /// </remarks>
        public async Task<ClientResponse> GetThreadMessagesAsync(string threadId)
        {
            if (string.IsNullOrEmpty(threadId))
            {
                throw new System.Exception("Thread ID is not provided. Please provide a valid thread ID.");
            }

            Console.WriteLine($"Retrieving messages for thread ID: {threadId}");
            AsyncPageable<PersistentThreadMessage> messages = _client.Messages.GetMessagesAsync(
                threadId: threadId,
                order: ListSortOrder.Ascending);

            _threadMessages = new ThreadMessages();
            _threadMessages.Messages = new List<string>();

            await foreach (PersistentThreadMessage threadMessage in messages)
            {
                foreach (MessageContent contentItem in threadMessage.ContentItems)
                {
                    if (contentItem is MessageTextContent textItem)
                        _threadMessages.Messages.Add(textItem.Text);
                }

            }

            return new ClientResponse
            {
                Response =  _threadMessages.Messages[0], // Return the first message for simplicity
                AgentThread = new AgentThread
                {
                    ThreadId = threadId,
                    Messages = _threadMessages.Messages
                }
            };
        }

    }
}