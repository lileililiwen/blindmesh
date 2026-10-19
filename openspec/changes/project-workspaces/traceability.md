# Requirement traceability

Outcome: Manage a project's visibility and membership. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| PROJ-1 | Lifecycle; /projects, /projects/new, /projects/{id}, /projects/{id}/settings | design.md; src/Blindmesh.Domain/Projects, src/Blindmesh.Application/Projects, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Projects; apps/web | PROJ-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; PROJ xUnit/integration + browser/axe assertions |
| PROJ-2 | Visibility; /projects, /projects/new, /projects/{id}, /projects/{id}/settings | design.md; src/Blindmesh.Domain/Projects, src/Blindmesh.Application/Projects, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Projects; apps/web | PROJ-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; PROJ xUnit/integration + browser/axe assertions |
| PROJ-UX | Accessible journey; /projects, /projects/new, /projects/{id}, /projects/{id}/settings | design.md; src/Blindmesh.Domain/Projects, src/Blindmesh.Application/Projects, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Projects; apps/web | PROJ-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; PROJ xUnit/integration + browser/axe assertions |
