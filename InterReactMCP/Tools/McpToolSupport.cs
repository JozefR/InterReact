using System.Net;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using InterReact;
using InterReact.Core;
using InterReact.Interfaces;
using InterReact.Messages;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterReactMCP.Tools;

internal static class McpToolSupport
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    internal static async Task<IInterReactClient> ConnectAsync(CancellationToken ct = default)
    {
        return await InterReactClient.ConnectAsync(options =>
        {
            options.LogFactory = NullLoggerFactory.Instance;

            string? host = Environment.GetEnvironmentVariable("IB_HOST");
            if (!string.IsNullOrWhiteSpace(host) && IPAddress.TryParse(host, out IPAddress? ipAddress))
                options.TwsIpAddress = ipAddress;

            string? ports = Environment.GetEnvironmentVariable("IB_PORTS");
            if (!string.IsNullOrWhiteSpace(ports))
            {
                List<int> parsedPorts = ports
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(p => int.TryParse(p, out int value) ? value : -1)
                    .Where(p => p > 0)
                    .ToList();
                if (parsedPorts.Count > 0)
                    options.IBPortAddresses = parsedPorts;
            }

            string? clientId = Environment.GetEnvironmentVariable("IB_CLIENT_ID");
            if (!string.IsNullOrWhiteSpace(clientId) && int.TryParse(clientId, out int parsedClientId))
                options.TwsClientId = parsedClientId;
        }, ct).ConfigureAwait(false);
    }

    internal static T ParseJson<T>(string json, string parameterName) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json))
            return new T();

        T? parsed = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (parsed is null)
            throw new ArgumentException($"Invalid JSON payload for '{parameterName}'.", parameterName);
        return parsed;
    }

    internal static TimeSpan Timeout(int timeoutSeconds, int defaultSeconds = 20)
    {
        int normalized = timeoutSeconds > 0 ? timeoutSeconds : defaultSeconds;
        return TimeSpan.FromSeconds(normalized);
    }

    internal static string ToJson(object value) => JsonSerializer.Serialize(value, JsonOptions);

    internal static List<TEnum> ParseEnumCsv<TEnum>(string csv) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseEnumToken<TEnum>)
            .ToList();
    }

    internal static TEnum ParseEnumToken<TEnum>(string value) where TEnum : struct, Enum
    {
        if (int.TryParse(value, out int numeric))
        {
            object boxed = Enum.ToObject(typeof(TEnum), numeric);
            if (boxed is TEnum enumValue)
                return enumValue;
        }

        if (Enum.TryParse(value, ignoreCase: true, out TEnum parsed))
            return parsed;

        throw new ArgumentException($"Could not parse '{value}' as {typeof(TEnum).Name}.");
    }

    internal static async Task<T[]> RequestArrayUntilEndAsync<T, TEnd>(
        IInterReactClient client,
        Action<Request, int> requestAction,
        Action<Request, int>? cancelAction = null,
        int timeoutSeconds = 20,
        CancellationToken ct = default)
        where T : IHasRequestId
        where TEnd : IHasRequestId
    {
        int requestId = client.Request.GetNextId();
        IObservable<TEnd> completed = client.Response.OfType<TEnd>().Where(x => x.RequestId == requestId).Take(1);
        IObservable<T> stream = client.Response.OfType<T>().Where(x => x.RequestId == requestId).TakeUntil(completed);
        IObservable<T> fatalAlerts = client.Response
            .OfType<AlertMessage>()
            .Where(x => x.RequestId == requestId && x.IsFatal)
            .SelectMany(alert => Observable.Throw<T>(alert.ToAlertException()));

        try
        {
            requestAction(client.Request, requestId);
            return await stream
                .Merge(fatalAlerts)
                .ToArray()
                .Timeout(Timeout(timeoutSeconds))
                .ToTask(ct)
                .ConfigureAwait(false);
        }
        finally
        {
            cancelAction?.Invoke(client.Request, requestId);
        }
    }

    internal static async Task<T> RequestSingleByIdAsync<T>(
        IInterReactClient client,
        Action<Request, int> requestAction,
        int timeoutSeconds = 20,
        CancellationToken ct = default)
        where T : IHasRequestId
    {
        int requestId = client.Request.GetNextId();
        IObservable<T> success = client.Response.OfType<T>().Where(x => x.RequestId == requestId).Take(1);
        IObservable<T> fatalAlerts = client.Response
            .OfType<AlertMessage>()
            .Where(x => x.RequestId == requestId && x.IsFatal)
            .SelectMany(alert => Observable.Throw<T>(alert.ToAlertException()));
        requestAction(client.Request, requestId);
        return await success
            .Merge(fatalAlerts)
            .Take(1)
            .Timeout(Timeout(timeoutSeconds))
            .ToTask(ct)
            .ConfigureAwait(false);
    }

    internal static async Task<T> RequestFirstAsync<T>(
        IInterReactClient client,
        Action<Request> requestAction,
        int timeoutSeconds = 20,
        CancellationToken ct = default)
    {
        IObservable<T> stream = client.Response.OfType<T>().Take(1);
        requestAction(client.Request);
        return await stream.Timeout(Timeout(timeoutSeconds)).ToTask(ct).ConfigureAwait(false);
    }
}
