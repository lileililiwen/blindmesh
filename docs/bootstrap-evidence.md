# Bootstrap evidence

Date: 2026-10-19. Scope: local documentation, governance and OpenSpec bootstrap from the existing PRD. No application feature is implemented or archived.

Selected backend: C# 14, .NET SDK 10.0.400/net10.0, ASP.NET Core 10, EF Core/Npgsql 10, PostgreSQL 17. Frontend remains Next.js 16/React 19/TypeScript 5 on Node 24/npm. The user explicitly selected C#; requirements.md is preserved unchanged, SHA256 718d1f7bb8884c803fb7aa6b8b8fc88c5afba6b9688884dad187f6eb3d7ec91b.

Shared sources inspected: Forge README/HANDOFF/ROADMAP/architecture; modules/workspace-governance initializer, schema, gate binders and vocabulary; modules/dotnet-platform-libs README/package inventory and adoption guide. Product code must consume exact packages/artifacts and remain runnable without sibling source or Forge. Governance commands are development tooling only.

The canonical governance initializer created missing declarations and entry documents while preserving existing files. It reported unresolved native build/test/security/product-code checks because application manifests do not exist. The C# declaration and aspnet-web Forge profile supersede its initial Rust selection; maturity remains L0 and deployable remains false.

Local SDK inventory confirms .NET SDK 10.0.400. OpenSpec CLI 1.6.0 is installed with Node 24 but is absent from the default shell PATH; add the global Node 24 bin directory to PATH for documented verification commands. Forge is available from its source checkout's built binary, also absent from default PATH. No project-local tool installation or copied Gate runtime was created.

GitHub authentication succeeded outside the sandbox; the initial sandbox failure was caused by restricted network access. The user selected public visibility on 2026-10-19 for lileililiwen/blindmesh. Publication and metadata verification are in progress; authentication alone does not establish either outcome.

Verification results and exact remaining blockers are maintained in HANDOFF.md. Documentation checks prove artifact consistency only. Native builds/tests, browser flows, accessibility and real screenshots remain unavailable until runtime-foundation. The shared token file and token contrast measurements establish artifact provenance only, not rendered interface evidence.
