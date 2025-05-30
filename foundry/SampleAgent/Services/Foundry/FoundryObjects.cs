using System.Collections.Generic;
using System.Threading.Tasks;

namespace Foundry
{
    public class ClientRequest
    {
        public string Request { get; set; }
        public AgentThread AgentThread { get; set; }
    }
    public class ClientResponse
    {
        public string Response { get; set; }
        public AgentThread AgentThread { get; set; }
    }

    public class AgentThread
    {
        public string ThreadId { get; set; }
        public string AgentId { get; set; }
        public string RunId { get; set; }
        public string Message { get; set; }
    }

    public class ThreadMessages
    {
        public List<string> Messages { get; set; } = new List<string>();
    }

    public class AgentConfig
    {
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public string Instructions { get; set; }
        public string AgentNamePrefix { get; set; }
        public string ThreadNamePrefix { get; set; }
    }
}