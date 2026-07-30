using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;

try
{
    var parsed = ParseArgs(args);
    if (!parsed.Ok)
    {
        if (parsed.Error is not null)
            Console.Error.WriteLine($"Error: {parsed.Error}");
        PrintUsage();
        return ExitCodes.InvalidArgs;
    }

    var (subscriptionId, region, vmSku, json) = parsed.Value;

    var credential = new DefaultAzureCredential();
    var armClient = new ArmClient(credential, subscriptionId);

    var notes = new List<string>();
    ComputeResourceSku? matched = null;

    var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

    try
    {
        await foreach (var sku in subscription.GetComputeResourceSkusAsync())
        {
            if (!string.Equals(sku.ResourceType?.ToString(), "virtualMachines", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(sku.Name, vmSku, StringComparison.OrdinalIgnoreCase))
                continue;

            var locations = sku.Locations?.Select(l => l.ToString().Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToHashSet(StringComparer.OrdinalIgnoreCase)
                           ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!locations.Contains(region))
                continue;

            matched = sku;
            break;
        }
    }
    catch (AuthenticationFailedException ex)
    {
        Console.Error.WriteLine("Authentication failed using DefaultAzureCredential.");
        Console.Error.WriteLine("Make sure you're logged in (az login) or have service principal/managed identity configured.");
        Console.Error.WriteLine($"Details: {ex.Message}");
        return ExitCodes.AuthOrApiError;
    }
    catch (RequestFailedException ex)
    {
        Console.Error.WriteLine("Azure API request failed while querying compute SKUs.");
        Console.Error.WriteLine($"Status: {ex.Status}, Code: {ex.ErrorCode}");
        Console.Error.WriteLine($"Details: {ex.Message}");
        return ExitCodes.AuthOrApiError;
    }

    if (matched is null)
    {
        var notFound = new Result(
            subscriptionId,
            region,
            vmSku,
            SkuFound: false,
            HasRestrictions: false,
            Zones: Array.Empty<string>(),
            LikelyDeployable: false,
            Recommendation: "VM SKU not found for virtualMachines in the requested region. Treat as not deployable.",
            Notes: new[]
            {
                "Verify SKU spelling (e.g., Standard_D4s_v5).",
                "Verify region format (e.g., eastus).",
                "SKU availability can vary by subscription and region."
            }
        );

        PrintResult(notFound, json);
        return ExitCodes.NotDeployableOrNotFound;
    }

    var hasRestrictions = matched.Restrictions is not null && matched.Restrictions.Any();

    var zones = (matched.LocationInfo ?? Enumerable.Empty<ComputeResourceSkuLocationInfo>())
        .Where(li => string.Equals(li.Location.ToString(), region, StringComparison.OrdinalIgnoreCase))
        .SelectMany(li => li.Zones ?? Enumerable.Empty<string>())
        .Where(z => !string.IsNullOrWhiteSpace(z))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(z => z, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    bool likelyDeployable;
    string recommendation;

    if (!hasRestrictions)
    {
        likelyDeployable = true;
        recommendation = "SKU exists in region and has no SKU-level restrictions. Likely deployable.";
        notes.Add("Final capacity is still validated by Azure at deployment time.");
    }
    else
    {
        likelyDeployable = false;
        recommendation = "SKU exists in region but has restrictions. Treat as not confidently deployable without additional validation.";

        foreach (var r in matched.Restrictions!)
        {
            var reason = r.ReasonCode?.ToString() ?? "UnknownReason";
            var vals = r.Values is null || !r.Values.Any() ? "(none)" : string.Join(", ", r.Values);
            notes.Add($"Restriction: reason={reason}, values={vals}");
        }

        notes.Add("Try deploying to a specific availability zone if compatible, or choose an alternate SKU/region.");
    }

    if (zones.Length == 0)
    {
        notes.Add("No zone data reported for this SKU+region combination.");
    }
    else
    {
        notes.Add($"Available zones in region: {string.Join(", ", zones)}");
    }

    var result = new Result(
        subscriptionId,
        region,
        matched.Name ?? vmSku,
        SkuFound: true,
        HasRestrictions: hasRestrictions,
        Zones: zones,
        LikelyDeployable: likelyDeployable,
        Recommendation: recommendation,
        Notes: notes.ToArray()
    );

    PrintResult(result, json);
    return likelyDeployable ? ExitCodes.Success : ExitCodes.NotDeployableOrNotFound;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Unexpected error.");
    Console.Error.WriteLine(ex.ToString());
    return ExitCodes.UnknownError;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  check-capacity --subscription <subscriptionId> --region <region> --sku <vmSku> [--json]");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  check-capacity --subscription 00000000-0000-0000-0000-000000000000 --region eastus --sku Standard_D4s_v5");
}

static void PrintResult(Result result, bool asJson)
{
    if (asJson)
    {
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
        return;
    }

    Console.WriteLine("Azure VM SKU Capacity/Deployability Check");
    Console.WriteLine("-----------------------------------------");
    Console.WriteLine($"Subscription : {result.SubscriptionId}");
    Console.WriteLine($"Region       : {result.Region}");
    Console.WriteLine($"VM SKU       : {result.VmSku}");
    Console.WriteLine($"SKU Found    : {result.SkuFound}");
    Console.WriteLine($"Restrictions : {result.HasRestrictions}");
    Console.WriteLine($"Zones        : {(result.Zones.Length == 0 ? "(none reported)" : string.Join(", ", result.Zones))}");
    Console.WriteLine($"Deployable   : {result.LikelyDeployable}");
    Console.WriteLine($"Recommendation: {result.Recommendation}");

    if (result.Notes.Length > 0)
    {
        Console.WriteLine("Notes:");
        foreach (var n in result.Notes)
            Console.WriteLine($"- {n}");
    }
}

static (bool Ok, (string SubscriptionId, string Region, string Sku, bool Json) Value, string? Error) ParseArgs(string[] args)
{
    if (args.Length == 0)
        return (false, default, "Missing arguments.");

    string? subscriptionId = null;
    string? region = null;
    string? sku = null;
    bool json = false;

    for (int i = 0; i < args.Length; i++)
    {
        var a = args[i];

        switch (a)
        {
            case "--subscription":
            case "-s":
                if (!TryReadValue(args, ref i, out subscriptionId))
                    return (false, default, "Missing value for --subscription.");
                break;

            case "--region":
            case "-r":
                if (!TryReadValue(args, ref i, out region))
                    return (false, default, "Missing value for --region.");
                break;

            case "--sku":
            case "-k":
                if (!TryReadValue(args, ref i, out sku))
                    return (false, default, "Missing value for --sku.");
                break;

            case "--json":
                json = true;
                break;

            case "--help":
            case "-h":
                return (false, default, null);

            default:
                return (false, default, $"Unknown argument: {a}");
        }
    }

    if (string.IsNullOrWhiteSpace(subscriptionId))
        return (false, default, "--subscription is required.");
    if (string.IsNullOrWhiteSpace(region))
        return (false, default, "--region is required.");
    if (string.IsNullOrWhiteSpace(sku))
        return (false, default, "--sku is required.");

    return (true, (subscriptionId.Trim(), region.Trim(), sku.Trim(), json), null);
}

static bool TryReadValue(string[] args, ref int i, out string? value)
{
    value = null;
    if (i + 1 >= args.Length)
        return false;

    value = args[++i];
    return !string.IsNullOrWhiteSpace(value);
}

record Result(
    string SubscriptionId,
    string Region,
    string VmSku,
    bool SkuFound,
    bool HasRestrictions,
    string[] Zones,
    bool LikelyDeployable,
    string Recommendation,
    string[] Notes
);

static class ExitCodes
{
    public const int Success = 0;
    public const int NotDeployableOrNotFound = 2;
    public const int InvalidArgs = 3;
    public const int AuthOrApiError = 4;
    public const int UnknownError = 10;
}