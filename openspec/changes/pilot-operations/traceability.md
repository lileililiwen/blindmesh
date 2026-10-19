# Requirement traceability

Outcome: Operate a verified pilot with recovery and measured gates. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| OPS-1 | Recovery; /admin/operations, /status | design.md; infra, src/Blindmesh.Api/Endpoints/Operations, src/Blindmesh.Worker; apps/web | OPS-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; OPS xUnit/integration + browser/axe assertions |
| OPS-2 | PilotGate; /admin/operations, /status | design.md; infra, src/Blindmesh.Api/Endpoints/Operations, src/Blindmesh.Worker; apps/web | OPS-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; OPS xUnit/integration + browser/axe assertions |
| OPS-UX | Accessible journey; /admin/operations, /status | design.md; infra, src/Blindmesh.Api/Endpoints/Operations, src/Blindmesh.Worker; apps/web | OPS-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; OPS xUnit/integration + browser/axe assertions |
