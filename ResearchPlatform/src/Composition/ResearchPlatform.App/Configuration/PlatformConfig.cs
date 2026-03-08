using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ResearchPlatform.App.Configuration;

public sealed class PlatformConfig
{
    public RuntimeConfig Runtime { get; set; } = new();
    public DataIngestionConfig DataIngestion { get; set; } = new();
    public DataWarehouseConfig DataWarehouse { get; set; } = new();
    public BacktestConfig Backtest { get; set; } = new();

    public static PlatformConfig Load(string? environmentOverride = null)
    {
        var environment = ResolveEnvironment(environmentOverride);
        var basePath = AppContext.BaseDirectory;

        var mergedNode = new JsonObject();
        MergeFileInto(mergedNode, Path.Combine(basePath, "appsettings.json"), required: true);
        MergeFileInto(mergedNode, Path.Combine(basePath, $"appsettings.{environment}.json"), required: false);

        ApplyEnvironmentOverrides(mergedNode, "RP__");

        var config = JsonSerializer.Deserialize<PlatformConfig>(mergedNode, Serializer.Options)
                     ?? throw new InvalidOperationException("Failed to deserialize platform configuration.");

        config.Runtime.Environment = environment;
        Validate(config);
        return config;
    }

    private static string ResolveEnvironment(string? overrideValue)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return overrideValue;
        }

        return Environment.GetEnvironmentVariable("RP_ENVIRONMENT")
               ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
               ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
               ?? "Development";
    }

    private static void MergeFileInto(JsonObject target, string path, bool required)
    {
        if (!File.Exists(path))
        {
            if (required)
            {
                throw new FileNotFoundException($"Required configuration file not found: {path}");
            }

            return;
        }

        var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        if (node is null)
        {
            throw new InvalidOperationException($"Configuration file {path} does not contain a JSON object.");
        }

        MergeObjects(target, node);
    }

    private static void MergeObjects(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            if (value is JsonObject sourceObject)
            {
                if (target[key] is JsonObject targetObject)
                {
                    MergeObjects(targetObject, sourceObject);
                }
                else
                {
                    target[key] = sourceObject.DeepClone();
                }
            }
            else
            {
                target[key] = value?.DeepClone();
            }
        }
    }

    private static void ApplyEnvironmentOverrides(JsonObject root, string prefix)
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string rawKey || !rawKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.Value is not string rawValue)
            {
                continue;
            }

            var path = rawKey[prefix.Length..].Split("__", StringSplitOptions.RemoveEmptyEntries);
            if (path.Length == 0)
            {
                continue;
            }

            SetByPath(root, path, ParsePrimitive(rawValue));
        }
    }

    private static void SetByPath(JsonObject root, IReadOnlyList<string> path, JsonNode? value)
    {
        JsonObject current = root;

        for (var i = 0; i < path.Count - 1; i++)
        {
            var segment = path[i];
            if (current[segment] is not JsonObject child)
            {
                child = new JsonObject();
                current[segment] = child;
            }

            current = child;
        }

        current[path[^1]] = value;
    }

    private static JsonNode ParsePrimitive(string value)
    {
        if (bool.TryParse(value, out var boolValue))
        {
            return JsonValue.Create(boolValue)!;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return JsonValue.Create(intValue)!;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return JsonValue.Create(doubleValue)!;
        }

        if (value.Contains(',', StringComparison.Ordinal))
        {
            var items = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var array = new JsonArray();
            foreach (var item in items)
            {
                array.Add(item);
            }

            return array;
        }

        return JsonValue.Create(value)!;
    }

    private static void Validate(PlatformConfig config)
    {
        var issues = new List<string>();

        if (!RuntimeConfig.SupportedEnvironments.Contains(config.Runtime.Environment, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add($"Runtime.Environment '{config.Runtime.Environment}' is not supported. Supported values: {string.Join(", ", RuntimeConfig.SupportedEnvironments)}.");
        }

        if (string.IsNullOrWhiteSpace(config.DataIngestion.Provider))
        {
            issues.Add("DataIngestion.Provider must not be empty.");
        }

        if (config.DataIngestion.Universes.Count == 0)
        {
            issues.Add("DataIngestion.Universes must contain at least one universe.");
        }

        if (config.DataIngestion.RequestTimeoutSeconds <= 0)
        {
            issues.Add("DataIngestion.RequestTimeoutSeconds must be greater than 0.");
        }

        if (config.DataIngestion.Provider.Equals("Massive", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(config.DataIngestion.MassiveApiBaseUrl))
        {
            issues.Add("DataIngestion.MassiveApiBaseUrl must not be empty when Provider is Massive.");
        }

        if (string.IsNullOrWhiteSpace(config.DataWarehouse.ConnectionString))
        {
            issues.Add("DataWarehouse.ConnectionString must not be empty.");
        }

        if (config.DataWarehouse.MaxBatchSize <= 0)
        {
            issues.Add("DataWarehouse.MaxBatchSize must be greater than 0.");
        }

        if (config.Backtest.InitialCapital <= 0)
        {
            issues.Add("Backtest.InitialCapital must be greater than 0.");
        }

        if (config.Backtest.CommissionBps < 0)
        {
            issues.Add("Backtest.CommissionBps cannot be negative.");
        }

        if (config.Backtest.SlippageBps < 0)
        {
            issues.Add("Backtest.SlippageBps cannot be negative.");
        }

        if (issues.Count > 0)
        {
            throw new InvalidOperationException("Configuration validation failed:\n- " + string.Join("\n- ", issues));
        }
    }

    private static class Serializer
    {
        public static readonly System.Text.Json.JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }
}

public sealed class RuntimeConfig
{
    public static readonly string[] SupportedEnvironments = ["Development", "Test", "Production"];

    public string Environment { get; set; } = "Development";
    public string LogLevel { get; set; } = "Information";
}

public sealed class DataIngestionConfig
{
    public string Provider { get; set; } = "Mock";
    public List<string> Universes { get; set; } = ["SP500", "SP100"];
    public bool EnableDailyRefresh { get; set; } = true;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public string MassiveApiBaseUrl { get; set; } = "https://api.massive.com";
    public string? MassiveApiKey { get; set; }
    public bool MassiveUseFixtureFallbackWhenApiKeyMissing { get; set; } = true;
}

public sealed class DataWarehouseConfig
{
    public string ConnectionString { get; set; } = "Server=localhost;Database=ResearchPlatform;Trusted_Connection=True;TrustServerCertificate=True;";
    public int MaxBatchSize { get; set; } = 1000;
}

public sealed class BacktestConfig
{
    public double InitialCapital { get; set; } = 50000;
    public double CommissionBps { get; set; } = 1.0;
    public double SlippageBps { get; set; } = 2.0;
}
