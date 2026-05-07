using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration;

public static class WorkflowActivityParseEvents
{
    [Function(nameof(ParseEvents))]
    public static string ParseEvents([ActivityTrigger] string name, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("ParseEvents");
        logger.LogInformation("Saying hello to {name}.", name);
        return $"Hello {name}!";
    }
}
