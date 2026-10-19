# Requirement traceability

Outcome: Receive reliable in-app and email workflow notifications. All rows are unimplemented; scenario names are normative and tests must use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| NOTIF-1 | Delivery; /notifications, /settings/notifications | design.md; src/Blindmesh.Domain/Notifications, src/Blindmesh.Application/Notifications, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Notifications; apps/web | NOTIF-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; NOTIF xUnit/integration + browser/axe assertions |
| NOTIF-2 | Preferences; /notifications, /settings/notifications | design.md; src/Blindmesh.Domain/Notifications, src/Blindmesh.Application/Notifications, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Notifications; apps/web | NOTIF-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; NOTIF xUnit/integration + browser/axe assertions |
| NOTIF-UX | Accessible journey; /notifications, /settings/notifications | design.md; src/Blindmesh.Domain/Notifications, src/Blindmesh.Application/Notifications, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Notifications; apps/web | NOTIF-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; NOTIF xUnit/integration + browser/axe assertions |
