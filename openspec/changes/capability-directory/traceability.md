# Requirement traceability

Outcome: Declare capabilities and evaluate hard matching conditions. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| CAP-1 | Declaration; /capabilities, /settings/capabilities, /tasks/{id}/eligibility | design.md; src/Blindmesh.Domain/Capabilities, src/Blindmesh.Application/Capabilities, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Capabilities; apps/web | CAP-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; CAP xUnit/integration + browser/axe assertions |
| CAP-2 | Eligibility; /capabilities, /settings/capabilities, /tasks/{id}/eligibility | design.md; src/Blindmesh.Domain/Capabilities, src/Blindmesh.Application/Capabilities, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Capabilities; apps/web | CAP-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; CAP xUnit/integration + browser/axe assertions |
| CAP-UX | Accessible journey; /capabilities, /settings/capabilities, /tasks/{id}/eligibility | design.md; src/Blindmesh.Domain/Capabilities, src/Blindmesh.Application/Capabilities, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Capabilities; apps/web | CAP-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; CAP xUnit/integration + browser/axe assertions |
