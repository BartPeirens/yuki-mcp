using System.ComponentModel;
using ModelContextProtocol.Server;
using YukiMcp.Configuration;

namespace YukiMcp.Tools;

/// <summary>
/// Server-status tool for verifying the exe, the stdio transport and the Claude Desktop config
/// work end to end, independent of whether the configured API key/Yuki itself is reachable. The
/// real Yuki endpoint tools live in the other files in this folder, one class per .asmx service
/// (AccountingTools, ArchiveTools, ContactTools, ...) plus GeneralTools.
/// </summary>
[McpServerToolType]
public static class DiagnosticsTools
{
    [McpServerTool(Name = "yuki_server_status")]
    [Description("Returns the YukiMcp server status: version, configured Yuki base URL, and whether an API key was supplied. This does not call Yuki itself - use it to verify the MCP server and Claude Desktop config are wired up correctly, independent of the Yuki API being reachable.")]
    public static string GetServerStatus(YukiServerOptions options)
    {
        var version = typeof(DiagnosticsTools).Assembly.GetName().Version?.ToString() ?? "unknown";
        var maskedKey = options.ApiKey.Length <= 4
            ? "****"
            : $"{new string('*', options.ApiKey.Length - 4)}{options.ApiKey[^4..]}";

        return $"""
            YukiMcp server status
            ----------------------
            Version:        {version}
            Yuki base URL:  {options.BaseUrl}
            API key:        configured ({maskedKey})
            Endpoint tools: see the help://yuki-mcp resource, or plan.md, for the full list.
            """;
    }
}
