# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A C#/.NET 10 MCP server (stdio transport) that wraps Yuki's accounting SOAP webservices as MCP
tools for Claude Desktop. See `plan.md` for the full architecture write-up, the endpoint-to-tool
mapping, and the current roadmap/open questions — read it before making structural changes.

## Commands

```powershell
dotnet build                                            # compile
dotnet run --project src/YukiMcp -- --api-key <key>     # run locally against real Yuki
dotnet run --project src/YukiMcp --no-build -- --api-key test123   # run without a real key (only
                                                          # yuki_server_status and the help
                                                          # resource work without hitting Yuki)

./scripts/publish.ps1                                   # publish single-file exe -> dist/win-x64/
./scripts/publish.ps1 -Runtime osx-arm64                 # other RIDs (untested)
./scripts/package-zip.ps1 -Version 0.1.0                 # publish + zip with README/LICENSE
```

There is no test project yet. To sanity-check the server manually, pipe newline-delimited
JSON-RPC into it (the stdio transport, not HTTP-framed):

```bash
(echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"0.0.1"}}}'; \
 sleep 1; echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'; \
 sleep 1; echo '{"jsonrpc":"2.0","id":2,"method":"tools/list"}') \
 | dotnet run --project src/YukiMcp --no-build -- --api-key test123
```

Logging goes to stderr (never stdout — stdout is JSON-RPC only), so redirect stderr separately when
inspecting output.

## Architecture

**Yuki is a SOAP 1.1 API, not REST.** Every "endpoint" in Yuki's public Postman documentation
(https://documenter.getpostman.com/view/12207912/UVCBB51L) is really an operation on one of 13
`.asmx` webservices under `https://api.yukiworks.be/ws/`. The client code for each service is
generated straight from the live WSDL with `dotnet-svcutil` (not hand-written XML envelopes):

```
dotnet-svcutil "https://api.yukiworks.be/ws/<Service>.asmx?WSDL" \
  --outputDir Yuki/<Service> --outputFile <Service>Client.cs \
  --namespace "*,YukiMcp.Yuki.<Service>" --projectFile YukiMcp.csproj
```

Generated output lives in `src/YukiMcp/Yuki/<Service>/<Service>Client.cs` plus a
`dotnet-svcutil.params.json` per service (used by `dotnet-svcutil -u` to refresh a service later).
**The live WSDL is the source of truth, not the Postman docs** — it exposes several undocumented
operations and is missing at least one documented one (see "Onder de motorkap" / "Open vragen" in
`plan.md`). When adding or touching a service's tools, regenerate from the WSDL rather than trusting
the Postman collection's parameter list.

Each generated `<Service>SoapClient` has two API surfaces: an interface-level one (sometimes a
single `*Request` message-contract parameter) and a friendlier `public` overload with flattened
scalar parameters that builds the request internally — **tools always call the scalar overload**.

Key pieces, all under `src/YukiMcp/`:

- `Program.cs` — generic host wiring: stdio transport, DI registration, logging-to-stderr.
- `Configuration/YukiServerOptions.cs` — parses `--api-key`/`--base-url` (or `YUKI_API_KEY`/
  `YUKI_BASE_URL` env vars).
- `Yuki/ServiceCollectionExtensions.cs` — registers one transient `<Service>SoapClient` per
  service (transient, not singleton: a WCF channel can fault and must be recreated).
- `Yuki/YukiSessionManager.cs` — owns the Yuki session lifecycle. Calls `Authenticate` (via the
  Sales client — arbitrary choice, it exists identically on every service), caches the session id
  (~23h), retries a call once on `FaultException` after re-authenticating. **Tools never take a
  session id parameter** — they call `sessionManager.ExecuteAsync(sessionId => client.OpAsync(sessionId, ...))`.
- `Yuki/YukiResult.cs` — formats whatever a Yuki call returns into tool output text: `XmlNode` →
  pretty XML, everything else → indented JSON (`IncludeFields = true`, since the generated DTOs use
  public fields, not properties). A handful of operations return a `*Response` wrapper with one
  field (`<Op>Result`) — those are unwrapped at the call site before formatting, not in
  `YukiResult` itself.
- `Yuki/YukiEnumHelper.cs` / `Yuki/YukiXml.cs` — SOAP enum params are exposed as `string` tool
  arguments (valid values listed in the `[Description]`) and parsed here; XML input params
  (`ProcessJournal`, `UpdateContact`, ...) are exposed as raw XML `string` and parsed here.
- `Tools/<Service>Tools.cs` — one static `[McpServerToolType]` class per service, one
  `[McpServerTool]` method per operation, named `yuki_<service>_<operation>` (snake_case).
  `Tools/GeneralTools.cs` holds the handful of operations (`Administrations`, `Domains`,
  `Companies`, `GetCurrentDomain`, `Language`, `SupportedLanguages`,
  `AdministrationsWithInternalCustomerCode`, `AdministrationID`) that exist identically on *every*
  service — exposed once as `yuki_general_*` instead of 13 times. The three `Authenticate*`
  operations are session-manager-internal only and are never exposed as tools.
- `Resources/HelpResource.cs` — the `help://yuki-mcp` resource.
- `Prompts/`, `Apps/` — empty on purpose; scaffolding notes for future work (see `plan.md` Fase
  4/5). Don't add prompt/app code without also wiring the corresponding `.With*FromAssembly()` call
  in `Program.cs`.

### Regenerating or extending the tool files

`Tools/*Tools.cs` (except `DiagnosticsTools.cs` and `GeneralTools.cs`) were produced by a one-off
codegen script (not checked into the repo) that parsed the generated client classes and the Postman
collection for descriptions. They are meant to be hand-edited afterwards — there is no `//
<auto-generated>` regeneration step wired into the build. When adding a new operation by hand,
follow the existing pattern in a neighboring method in the same file: inject `YukiSessionManager`
and the relevant `<Service>SoapClient`, call `sessionManager.ExecuteAsync(sessionId => client.OpAsync(sessionId, ...))`,
format with `YukiResult.Format(...)`, and set the MCP annotations (`ReadOnly`/`Destructive`/
`Idempotent`/`OpenWorld`) consistently with the read/write convention described above.

### Distribution

`YukiMcp.csproj` sets `SelfContained` + `PublishSingleFile` so `dotnet publish` produces one
self-contained exe (no separate .NET runtime install needed by the recipient). `RuntimeIdentifiers`
lists win-x64/osx-x64/osx-arm64/linux-x64, but only win-x64 has actually been run. This is
intentionally *not* packaged as an installer (MSI/Inno Setup) — see `plan.md` Fase 6 for why, and
when that might change.
