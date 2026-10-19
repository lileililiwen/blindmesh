# Blindmesh UI baseline

Brand: Blindmesh. Tagline: Find what you couldn't see.
Audience: independent developers seeking actionable peer testing.
Primary actions: find a task, ask for testing, submit useful evidence.

Use the shared semantic token palette, neutral surfaces, system font fallback, clear labels and structured information. Brand recognition comes from content and hierarchy; do not invent a new palette before evidence supports it. shadcn/ui/Radix components consume semantic CSS variables through Tailwind mappings. No independently hand-built authentication form; sign-in is the GitHub authorization action in the shared application shell.

Theme uses data-theme with system preference and optional persisted toggle. Focus, errors, validation, status and ledger amounts are communicated in words. Follow docs/architecture.md for responsive and accessibility requirements.

Token source: Forge modules/dotnet-platform-libs/ui/packages/design-tokens/dist/tokens.css.
Local immutable copy: apps/web/src/styles/vendor/tokens.css; SHA256 7ca6b7956b3f6b475254b4a5d0cf599724e4e1af5f0f88aba4fbccb9f38fc19b. docs/evidence/token-contrast.json contains measured token pairs for both themes, verified by scripts/verify_bootstrap.py. These measurements do not establish rendered browser accessibility; foundation must wire real components and verify them in the browser.
