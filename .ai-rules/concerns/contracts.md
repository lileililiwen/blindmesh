# Contract and dependency concerns

The ASP.NET Core API owns OpenAPI and the generated TypeScript client. No handwritten duplicate response models or direct frontend database access. Breaking changes require an explicit version/migration decision.

Resolve compatible dependency patch versions in the selected implementation change and commit packages.lock.json/package-lock.json. Use one exact PlatformPackageVersion and Package Source Mapping; verify isolated NuGet restore before adoption claims. Shared artifacts require a version/digest and source receipt; sibling ProjectReference and local feed paths are prohibited.

Use the API error, cursor, visibility, integer-credit, and idempotency contracts in docs/architecture.md.
