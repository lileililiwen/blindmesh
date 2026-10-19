# Requirement traceability

Outcome: Resolve reports and credit disputes with accountable moderation. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| MOD-1 | Arbitration; /reports/new, /disputes/{id}, /admin/reports, /admin/disputes | design.md; src/Blindmesh.Domain/Moderation, src/Blindmesh.Application/Moderation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Moderation; apps/web | MOD-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; MOD xUnit/integration + browser/axe assertions |
| MOD-2 | Safety; /reports/new, /disputes/{id}, /admin/reports, /admin/disputes | design.md; src/Blindmesh.Domain/Moderation, src/Blindmesh.Application/Moderation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Moderation; apps/web | MOD-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; MOD xUnit/integration + browser/axe assertions |
| MOD-UX | Accessible journey; /reports/new, /disputes/{id}, /admin/reports, /admin/disputes | design.md; src/Blindmesh.Domain/Moderation, src/Blindmesh.Application/Moderation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Moderation; apps/web | MOD-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; MOD xUnit/integration + browser/axe assertions |
