# Requirement traceability

Outcome: Run the real web/API/database/worker shell. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| FOUND-1 | Startup; / | design.md; src/Blindmesh.Api, src/Blindmesh.Worker, src/Blindmesh.Infrastructure; apps/web | FOUND-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; FOUND xUnit/integration + browser/axe assertions |
| FOUND-2 | Contracts; / | design.md; src/Blindmesh.Api, src/Blindmesh.Worker, src/Blindmesh.Infrastructure; apps/web | FOUND-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; FOUND xUnit/integration + browser/axe assertions |
| FOUND-3 | Portability; / | design.md; src/Blindmesh.Api, src/Blindmesh.Worker, src/Blindmesh.Infrastructure; apps/web | FOUND-3 success; rejection; boundary | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; FOUND xUnit/integration + browser/axe assertions |
| FOUND-UX | Accessible journey; / | design.md; src/Blindmesh.Api, src/Blindmesh.Worker, src/Blindmesh.Infrastructure; apps/web | FOUND-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.4, 3.1, 3.2, 4.1, 4.2 | verification.md; FOUND xUnit/integration + browser/axe assertions |
