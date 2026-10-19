# Requirement traceability

Outcome: Explain non-purchasable reputation from accepted outcomes. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| REP-1 | Provenance; /people/{username}/history, /settings/reputation | design.md; src/Blindmesh.Domain/Reputation, src/Blindmesh.Application/Reputation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Reputation; apps/web | REP-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; REP xUnit/integration + browser/axe assertions |
| REP-2 | Privacy; /people/{username}/history, /settings/reputation | design.md; src/Blindmesh.Domain/Reputation, src/Blindmesh.Application/Reputation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Reputation; apps/web | REP-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; REP xUnit/integration + browser/axe assertions |
| REP-UX | Accessible journey; /people/{username}/history, /settings/reputation | design.md; src/Blindmesh.Domain/Reputation, src/Blindmesh.Application/Reputation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Reputation; apps/web | REP-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; REP xUnit/integration + browser/axe assertions |
