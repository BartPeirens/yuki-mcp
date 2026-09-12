namespace YukiMcp.Configuration;

/// <summary>
/// Startup configuration for the MCP server. Values come from command-line arguments (as passed
/// by Claude Desktop's "args" config), then environment variables, then a ".env" file sitting
/// next to the exe (see <see cref="LoadDotEnvFile"/>) — whichever is found first. See README.md
/// for the Claude Desktop configuration example.
/// </summary>
public sealed class YukiServerOptions
{
    public const string DefaultBaseUrl = "https://api.yukiworks.be/ws/";

    /// <summary>
    /// Placeholder value shipped in the distributed .env template (see scripts/package-zip.ps1).
    /// Treated as "not set" so a user who forgot to fill it in gets the normal missing-key error
    /// instead of the server trying to authenticate with the literal placeholder text.
    /// </summary>
    private const string PlaceholderApiKey = "Replace with apikey";

    /// <summary>The Yuki webservice access key (Domain or Administration API key).</summary>
    public required string ApiKey { get; init; }

    /// <summary>Base URL for the Yuki .asmx webservices. Overridable for test/sandbox use.</summary>
    public string BaseUrl { get; init; } = DefaultBaseUrl;

    public static YukiServerOptions Parse(string[] args)
    {
        var dotEnv = new Lazy<IReadOnlyDictionary<string, string>>(LoadDotEnvFile);

        var apiKey = GetValue(args, "--api-key", "-k")
            ?? Environment.GetEnvironmentVariable("YUKI_API_KEY")
            ?? GetDotEnvValue(dotEnv, "APIKEY", "API_KEY", "YUKI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Equals(PlaceholderApiKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Missing Yuki API key. Fill in APIKEY=... in the .env file next to YukiMcp.exe, " +
                "pass it as a startup argument (--api-key <key>) in the Claude Desktop MCP server " +
                "config, or set the YUKI_API_KEY environment variable when running the exe manually. " +
                "See README.md for the full setup.");
        }

        var baseUrl = GetValue(args, "--base-url")
            ?? Environment.GetEnvironmentVariable("YUKI_BASE_URL")
            ?? GetDotEnvValue(dotEnv, "BASEURL", "BASE_URL", "YUKI_BASE_URL")
            ?? DefaultBaseUrl;

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

    private static string? GetDotEnvValue(Lazy<IReadOnlyDictionary<string, string>> dotEnv, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dotEnv.Value.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads simple KEY=VALUE lines from a ".env" file next to the running exe (so the recipient
    /// of the distributed zip never has to touch Claude Desktop's config to set an API key — see
    /// README.md). <c>AppContext.BaseDirectory</c> is the documented, single-file-publish-aware
    /// way to get "the directory the exe lives in" (unlike <c>Environment.ProcessPath</c>, which
    /// during `dotnet run` points at dotnet.exe, not the app). Falls back to the current working
    /// directory so a ".env" at the repo root is also picked up when running via `dotnet run`.
    /// Blank lines and lines starting with '#' are ignored; values may be wrapped in single or
    /// double quotes.
    /// </summary>
    private static IReadOnlyDictionary<string, string> LoadDotEnvFile()
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        };

        var path = candidatePaths.FirstOrDefault(File.Exists);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (path is null)
        {
            return result;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }
}
