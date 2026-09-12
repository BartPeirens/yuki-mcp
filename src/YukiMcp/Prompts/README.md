# Prompts (reserved, not implemented)

This folder is reserved for MCP **prompts** - reusable, parameterized prompt templates the server
exposes so a client can pull in a ready-made Yuki-related prompt instead of the model composing
one from scratch (e.g. "reconcile outstanding debtor items for administration X").

No prompts exist yet. When the first one is added:

1. Add a class here, e.g. `ReconciliationPrompts.cs`, marked `[McpServerPromptType]` with methods
   marked `[McpServerPrompt]` (same pattern as `Tools/` and `Resources/`).
2. Register the assembly scan in `Program.cs` by adding `.WithPromptsFromAssembly()` to the
   `AddMcpServer()` builder chain.

See `plan.md`, Fase 4, for context on why this is deferred.
