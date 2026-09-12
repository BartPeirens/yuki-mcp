using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YukiMcp.Configuration;
using YukiMcp.Yuki;

var builder = Host.CreateApplicationBuilder(args);

// The MCP stdio transport uses stdout exclusively for JSON-RPC frames, so every log line must
// go to stderr instead - otherwise it corrupts the protocol stream and Claude Desktop can't
// parse responses.
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// The Yuki API access key is passed as a startup argument (see README.md / plan.md) so the
// exe can be dropped straight into a Claude Desktop "command"/"args" config with no extra
// setup step (no .env file, no OS-level secret store).
var yukiOptions = YukiServerOptions.Parse(args);
builder.Services.AddSingleton(yukiOptions);

builder.Services.AddYukiClients();
builder.Services.AddSingleton<YukiSessionManager>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

// Prompts and MCP "apps" (richer, UI-carrying responses) are on the roadmap - see plan.md,
// Fase 4 and Fase 5 - but have no real content yet. Once the first one ships, register it here:
//   .WithPromptsFromAssembly()

await builder.Build().RunAsync();
