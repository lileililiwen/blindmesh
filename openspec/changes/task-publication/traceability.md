# Requirement traceability

Outcome: Publish budget-backed unified testing tasks. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| TASK-1 | Publication; /tasks, /tasks/new, /tasks/{id}, /projects/{id}/tasks | design.md; src/Blindmesh.Domain/Tasks, src/Blindmesh.Application/Tasks, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Tasks; apps/web | TASK-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; TASK xUnit/integration + browser/axe assertions |
| TASK-2 | Discovery; /tasks, /tasks/new, /tasks/{id}, /projects/{id}/tasks | design.md; src/Blindmesh.Domain/Tasks, src/Blindmesh.Application/Tasks, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Tasks; apps/web | TASK-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; TASK xUnit/integration + browser/axe assertions |
| TASK-UX | Accessible journey; /tasks, /tasks/new, /tasks/{id}, /projects/{id}/tasks | design.md; src/Blindmesh.Domain/Tasks, src/Blindmesh.Application/Tasks, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Tasks; apps/web | TASK-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; TASK xUnit/integration + browser/axe assertions |
