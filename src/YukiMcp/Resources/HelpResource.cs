using System.ComponentModel;
using ModelContextProtocol.Server;

namespace YukiMcp.Resources;

/// <summary>
/// The one resource this server exposes today: a human/model-readable "how do I use this MCP"
/// guide, independent of the underlying Yuki API docs. Update <see cref="GetHelp"/> as tools are
/// added in Fase 3 so it stays a genuine usage guide instead of drifting from reality.
/// </summary>
[McpServerResourceType]
public static class HelpResource
{
    [McpServerResource(UriTemplate = "help://yuki-mcp", Name = "yuki-mcp-help", MimeType = "text/markdown")]
    [Description("Explains what the Yuki MCP server is, how it authenticates against the Yuki API, and what tools/resources it currently exposes.")]
    public static string GetHelp() => """
        # Yuki MCP - help

        This MCP server wraps the Yuki accounting API webservices
        (https://documenter.getpostman.com/view/12207912/UVCBB51L) as MCP tools, so an assistant
        can call Yuki directly over the Model Context Protocol.

        ## Authentication

        The server takes a single Yuki webservice access key (Domain or Administration API key) as
        a startup argument - it is never a tool parameter. Internally it exchanges that key for a
        session id (Yuki's `Authenticate` call), caches it, and re-authenticates automatically when
        it expires. Tools never take a session id argument.

        ## Tools

        Tools are named `yuki_<service>_<operation>`, one per Yuki .asmx webservice operation
        (Accounting, AccountingInfo, Archive, Backoffice, ChangeDigest, Contact, Domains,
        FiscalTable, Integration, Pettycash, Sales, Vat), plus a small `yuki_general_*` group for
        the handful of operations (Administrations, Domains, Companies, ...) that exist identically
        on every service. `yuki_server_status` reports the server's own version/config, not Yuki
        data. See `plan.md` in the repository for the full endpoint-by-tool mapping and known
        limitations (e.g. undocumented WSDL operations, one unsupported filter parameter).

        Tools marked as writes in `plan.md` create or change data in Yuki (uploading documents,
        posting journals, updating contacts, ...) - treat those with the same care you would a
        direct Yuki API call, since Yuki has no separate sandbox for this server to target.

        ## Adding this server to Claude Desktop

        Add an entry to `claude_desktop_config.json` under `mcpServers` pointing at the published
        exe, with the API key as an argument - see the repository README.md for the exact JSON
        snippet and where that config file lives per OS.
        """;
}
