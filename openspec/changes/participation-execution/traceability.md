# Requirement traceability

Outcome: Apply for and execute one independently funded slot. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| PART-1 | Seat; /tasks/{id}/apply, /work, /work/{id} | design.md; src/Blindmesh.Domain/Tasks, src/Blindmesh.Application/Tasks, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Tasks; apps/web | PART-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; PART xUnit/integration + browser/axe assertions |
| PART-2 | Deadlines; /tasks/{id}/apply, /work, /work/{id} | design.md; src/Blindmesh.Domain/Tasks, src/Blindmesh.Application/Tasks, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Tasks; apps/web | PART-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; PART xUnit/integration + browser/axe assertions |
| PART-UX | Accessible journey; /tasks/{id}/apply, /work, /work/{id} | design.md; src/Blindmesh.Domain/Tasks, src/Blindmesh.Application/Tasks, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Tasks; apps/web | PART-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; PART xUnit/integration + browser/axe assertions |
