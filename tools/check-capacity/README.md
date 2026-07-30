# Azure VM SKU Capacity Checker

`check-capacity` is a .NET command-line tool that checks whether an Azure VM SKU is listed for a subscription and region. It reports SKU-level restrictions, advertised availability zones, and whether the SKU is likely deployable.

> [!IMPORTANT]
> A successful result does not reserve or guarantee VM capacity. Azure performs the final capacity check when a deployment is submitted.

## Prerequisites

- .NET 10 SDK
- An Azure subscription
- An identity that can read Compute SKU metadata for the subscription

The tool uses `DefaultAzureCredential` and does not accept or store credentials. For local use, authenticate with the Azure CLI:

```bash
az login
```

In Azure-hosted environments, use a managed identity with the minimum required RBAC permissions. Service principal and other credential sources supported by `DefaultAzureCredential` can also be used.

## Run the tool

From this directory:

```bash
dotnet run -- \
  --subscription 00000000-0000-0000-0000-000000000000 \
  --region eastus \
  --sku Standard_D4s_v5
```

Add `--json` for structured output:

```bash
dotnet run -- \
  --subscription 00000000-0000-0000-0000-000000000000 \
  --region eastus \
  --sku Standard_D4s_v5 \
  --json
```

Use `--help` or `-h` to print the built-in usage information.

## Arguments

| Argument | Short form | Required | Description |
| --- | --- | --- | --- |
| `--subscription <id>` | `-s` | Yes | Azure subscription ID to query. |
| `--region <name>` | `-r` | Yes | Azure region name, such as `eastus`. |
| `--sku <name>` | `-k` | Yes | VM SKU name, such as `Standard_D4s_v5`. |
| `--json` | | No | Emits indented JSON instead of text. |
| `--help` | `-h` | No | Prints usage information. |

## Result fields

| Field | Meaning |
| --- | --- |
| `SubscriptionId` | Subscription that was queried. |
| `Region` | Requested Azure region. |
| `VmSku` | Requested or matched VM SKU name. |
| `SkuFound` | Whether the SKU is listed for `virtualMachines` in the region. |
| `HasRestrictions` | Whether Azure returned SKU-level restrictions. |
| `Zones` | Availability zones advertised for the SKU and region. |
| `LikelyDeployable` | `true` when the SKU exists and has no reported restrictions. |
| `Recommendation` | Human-readable interpretation of the result. |
| `Notes` | Capacity caveats, zone details, or restriction reasons. |

`LikelyDeployable` is intentionally conservative. A value of `false` means the SKU was not found or has reported restrictions; a value of `true` means no SKU-level restriction was reported, not that deployment capacity is guaranteed.

## Exit codes

| Code | Meaning |
| ---: | --- |
| `0` | SKU is likely deployable. |
| `2` | SKU was not found or is not confidently deployable. |
| `3` | Arguments are missing or invalid, or help was requested. |
| `4` | Authentication or Azure API request failed. |
| `10` | An unexpected error occurred. |

The exit codes and `--json` output make the tool suitable for scripts and CI workflows.

## How it works

1. Parses and validates the command-line arguments.
2. Authenticates with `DefaultAzureCredential`.
3. Queries Compute resource SKUs visible to the subscription.
4. Matches the requested VM SKU and region.
5. Evaluates SKU restrictions and advertised availability zones.
6. Prints text or JSON and returns the corresponding exit code.

## Limitations

- The tool reads SKU metadata; it does not perform a deployment or quota check.
- Regional or zonal capacity can change after the query completes.
- Subscription quota, policy, deployment configuration, and transient platform capacity can still prevent deployment.
- To guarantee capacity for supported VM sizes and regions, evaluate Azure Capacity Reservations separately.

## Build

```bash
dotnet build
```
