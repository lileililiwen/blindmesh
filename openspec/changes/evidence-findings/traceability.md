# Requirement traceability

Outcome: Submit protected evidence and settle reviewed work. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| EVID-1 | Evidence; /work/{id}/submit, /projects/{id}/reviews, /findings/{id} | design.md; src/Blindmesh.Domain/Findings, src/Blindmesh.Application/Findings, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Findings; apps/web | EVID-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; EVID xUnit/integration + browser/axe assertions |
| EVID-2 | Review; /work/{id}/submit, /projects/{id}/reviews, /findings/{id} | design.md; src/Blindmesh.Domain/Findings, src/Blindmesh.Application/Findings, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Findings; apps/web | EVID-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; EVID xUnit/integration + browser/axe assertions |
| EVID-UX | Accessible journey; /work/{id}/submit, /projects/{id}/reviews, /findings/{id} | design.md; src/Blindmesh.Domain/Findings, src/Blindmesh.Application/Findings, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Findings; apps/web | EVID-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; EVID xUnit/integration + browser/axe assertions |
