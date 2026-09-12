using System.ServiceModel;
using YukiMcp.Configuration;
using YukiMcp.Yuki.Sales;

namespace YukiMcp.Yuki;

/// <summary>
/// Owns the Yuki SOAP session lifecycle so individual tools never have to think about it: it
/// calls GENERAL - Authenticate with the configured access key, caches the returned sessionID,
/// and re-authenticates when it expires. Every tool asks this class to run its call instead of
/// taking a session id as a tool parameter.
///
/// Authenticate lives identically on every generated service client (see plan.md, "Sessie- en
/// authenticatiebeheer"); the Sales one is used here only because that's the client the public
/// Postman collection's GENERAL examples happen to use - it has no other significance.
/// </summary>
public sealed class YukiSessionManager
{
    // Yuki documents sessions as valid for 24h. Refresh a bit early so a long-running tool call
    // never races the exact expiry.
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(23);

    private readonly YukiServerOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _sessionId;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public YukiSessionManager(YukiServerOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Runs <paramref name="operation"/> with a valid session id, retrying it exactly once with a
    /// freshly-authenticated session if the call faults (the cached session id may have expired
    /// early or been closed on Yuki's side).
    /// </summary>
    public async Task<TResult> ExecuteAsync<TResult>(Func<string, Task<TResult>> operation)
    {
        var sessionId = await GetSessionIdAsync();
        try
        {
            return await operation(sessionId);
        }
        catch (FaultException)
        {
            InvalidateSession();
            var freshSessionId = await GetSessionIdAsync();
            return await operation(freshSessionId);
        }
    }

    private async Task<string> GetSessionIdAsync()
    {
        if (_sessionId is { } cached && DateTimeOffset.UtcNow < _expiresAt)
        {
            return cached;
        }

        await _gate.WaitAsync();
        try
        {
            if (_sessionId is { } cachedAfterWait && DateTimeOffset.UtcNow < _expiresAt)
            {
                return cachedAfterWait;
            }

            var authClient = new SalesSoapClient(SalesSoapClient.EndpointConfiguration.SalesSoap, $"{_options.BaseUrl}Sales.asmx");
            string sessionId;
            try
            {
                sessionId = await authClient.AuthenticateAsync(_options.ApiKey);
            }
            finally
            {
                ((IDisposable)authClient).Dispose();
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new InvalidOperationException(
                    "Yuki's Authenticate call returned an empty session id - check the configured API key (--api-key).");
            }

            _sessionId = sessionId;
            _expiresAt = DateTimeOffset.UtcNow + SessionLifetime;
            return sessionId;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void InvalidateSession()
    {
        _sessionId = null;
        _expiresAt = DateTimeOffset.MinValue;
    }
}
