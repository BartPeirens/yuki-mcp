namespace YukiMcp.Configuration;

/// <summary>
/// Startup configuration for the MCP server. Values come from command-line arguments (as passed
/// by Claude Desktop's "args" config) with environment variables as a fallback for local/manual
/// runs. See README.md for the Claude Desktop configuration example.
/// </summary>
public sealed class YukiServerOptions
{
    public const string DefaultBaseUrl = "https://api.yukiworks.be/ws/";

    /// <summary>The Yuki webservice access key (Domain or Administration API key).</summary>
    public required string ApiKey { get; init; }

    /// <summary>Base URL for the Yuki .asmx webservices. Overridable for test/sandbox use.</summary>
    public string BaseUrl { get; init; } = DefaultBaseUrl;

    public static YukiServerOptions Parse(string[] args)
    {
        var apiKey = GetValue(args, "--api-key", "-k") ?? Environment.GetEnvironmentVariable("YUKI_API_KEY");
        var baseUrl = GetValue(args, "--base-url") ?? Environment.GetEnvironmentVariable("YUKI_BASE_URL") ?? DefaultBaseUrl;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Missing Yuki API key. Pass it as a startup argument (--api-key <key>) in the " +
                "Claude Desktop MCP server config, or set the YUKI_API_KEY environment variable " +
                "when running the exe manually. See README.md for the full setup.");
        }

        return new YukiServerOptions
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        };
    }

    /// <summary>
    /// Supports both "--flag value" and "--flag=value" styles so the config works regardless of
    /// how a user (or Claude Desktop's JSON args array) splits the arguments.
    /// </summary>
    private static string? GetValue(string[] args, params string[] names)
    {
        for (var i = 0; i < args.Length; i++)
        {
            foreach (var name in names)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }

                var prefix = name + "=";
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i][prefix.Length..];
                }
            }
        }

        return null;
    }
}
