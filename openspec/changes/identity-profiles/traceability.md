# Requirement traceability

Outcome: Sign in safely and manage a public contributor profile. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| IDENT-1 | Session; /sign-in, /onboarding, /settings/profile, /people/{username} | design.md; src/Blindmesh.Domain/Identity, src/Blindmesh.Application/Identity, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Identity; apps/web | IDENT-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; IDENT xUnit/integration + browser/axe assertions |
| IDENT-2 | Profile; /sign-in, /onboarding, /settings/profile, /people/{username} | design.md; src/Blindmesh.Domain/Identity, src/Blindmesh.Application/Identity, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Identity; apps/web | IDENT-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; IDENT xUnit/integration + browser/axe assertions |
| IDENT-UX | Accessible journey; /sign-in, /onboarding, /settings/profile, /people/{username} | design.md; src/Blindmesh.Domain/Identity, src/Blindmesh.Application/Identity, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Identity; apps/web | IDENT-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; IDENT xUnit/integration + browser/axe assertions |
