# Requirement traceability

Outcome: View conserved balances and execute credit escrow primitives. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| CRED-1 | Conservation; /wallet, /wallet/history | design.md; src/Blindmesh.Domain/Credits, src/Blindmesh.Application/Credits, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Credits; apps/web | CRED-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; CRED xUnit/integration + browser/axe assertions |
| CRED-2 | Grant; /wallet, /wallet/history | design.md; src/Blindmesh.Domain/Credits, src/Blindmesh.Application/Credits, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Credits; apps/web | CRED-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; CRED xUnit/integration + browser/axe assertions |
| CRED-UX | Accessible journey; /wallet, /wallet/history | design.md; src/Blindmesh.Domain/Credits, src/Blindmesh.Application/Credits, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Credits; apps/web | CRED-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; CRED xUnit/integration + browser/axe assertions |
