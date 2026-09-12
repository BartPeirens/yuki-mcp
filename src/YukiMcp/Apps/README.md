# Apps (reserved, not implemented)

This folder is reserved for MCP "apps" - tool/resource responses that carry an interactive UI
(HTML/JS payload) alongside their data, so a client that supports it can render something richer
than plain text or JSON for a Yuki result (e.g. a document preview, a transactions table).

This is an emerging part of the MCP ecosystem and not yet a stable, versioned feature of the
`ModelContextProtocol` C# SDK used by this project (see `YukiMcp.csproj`). Nothing is wired up in
`Program.cs` for it.

When the SDK exposes a stable API for this:

1. Add the relevant `PackageReference` / SDK feature and wire it into `Program.cs`, next to the
   existing `.WithToolsFromAssembly()` / `.WithResourcesFromAssembly()` calls.
2. Add app-backed responses here, one file per feature area, following the same per-service
   grouping used in `Tools/`.

See `plan.md`, Fase 5, for context on why this is deferred.
