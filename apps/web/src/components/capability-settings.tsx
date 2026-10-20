"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import {
  CapabilityStatusBadge,
  CapabilityVisibilityBadge,
  KindBadge,
} from "@/components/capability-badges";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox, Field, Select, TextInput } from "@/components/ui/field";
import {
  createCapability,
  loadOwnCapabilities,
  updateCapability,
  withdrawCapability,
  type CapabilityCommandState,
  type CapabilityListState,
} from "@/lib/capabilities";
import type { CapabilityViewResponse } from "@blindmesh/api-client";

/**
 * The identifiers a declaration may carry. The API owns the allowlist and
 * refuses every other key, so a credential can never reach the database. The
 * list here only makes the allowed keys visible in the interface.
 */
const metadataKeys = [
  "os",
  "os_version",
  "browser",
  "browser_version",
  "device",
  "device_model",
  "region",
  "provider",
] as const;

const kinds = ["account", "device", "skill", "region", "network", "role"] as const;

/** The order of the fields. The first field with an error takes the focus. */
const fieldOrder = ["kind", "label", "tags", "visibility", "available", "metadata"] as const;

type Draft = {
  kind: string;
  label: string;
  tags: string;
  visibility: string;
  available: boolean;
  metadata: Record<string, string>;
};

function emptyDraft(): Draft {
  return {
    kind: "device",
    label: "",
    tags: "",
    visibility: "public",
    available: true,
    metadata: Object.fromEntries(metadataKeys.map((key) => [key, ""])),
  };
}

function toDraft(capability: CapabilityViewResponse): Draft {
  const metadata = Object.fromEntries(metadataKeys.map((key) => [key, capability.metadata[key] ?? ""]));

  return {
    kind: capability.kind,
    label: capability.label,
    tags: capability.tags.join(", "),
    visibility: capability.visibility,
    available: capability.available,
    metadata,
  };
}

function toTags(value: string): string[] {
  return value
    .split(",")
    .map((tag) => tag.trim())
    .filter((tag) => tag.length > 0);
}

function toMetadata(draft: Draft): Record<string, string> {
  const metadata: Record<string, string> = {};

  for (const [key, value] of Object.entries(draft.metadata)) {
    const trimmed = value.trim();

    if (trimmed.length > 0) {
      metadata[key] = trimmed;
    }
  }

  return metadata;
}

/**
 * The own capabilities of the contributor. The contributor declares, edits and
 * withdraws the capabilities here. Every command carries the version that was
 * read, so a lost change is refused instead of overwritten. A withdrawn
 * declaration keeps its row and its history, but it never matches a task
 * again.
 */
export function CapabilitySettings() {
  const { state: session, csrfToken } = useSession();
  const [state, setState] = useState<CapabilityListState>({ kind: "loading" });
  const [capabilities, setCapabilities] = useState<CapabilityViewResponse[]>([]);
  const [draft, setDraft] = useState<Draft>(emptyDraft);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [command, setCommand] = useState<CapabilityCommandState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const [withdrawing, setWithdrawing] = useState<string | null>(null);

  const fieldRefs = useRef<Record<string, HTMLElement | null>>({});
  const triggerRef = useRef<HTMLButtonElement>(null);
  const idempotencyKey = useRef<string>(newIdempotencyKey());

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadOwnCapabilities();

    if (next.kind === "ready") {
      setCapabilities(next.capabilities);
    }

    setState(next);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      if (session.kind !== "authenticated") {
        setState({ kind: "error", message: "Sign in to manage your capabilities." });
        return;
      }

      const next = await loadOwnCapabilities();

      if (cancelled) {
        return;
      }

      if (next.kind === "ready") {
        setCapabilities(next.capabilities);
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="capability-settings-heading" aria-busy="true" className="reading-width">
        <h1 id="capability-settings-heading" className="text-title font-semibold">
          Your capabilities
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading your capabilities.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-20 w-full rounded bg-muted" />
          <div className="h-20 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="capability-settings-heading" className="reading-width">
        <h1 id="capability-settings-heading" className="text-title font-semibold">
          Your capabilities
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.message}
        </p>
        <p className="mt-4">
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  const fieldErrors = command.kind === "invalid" ? command.fieldErrors : {};
  const pending = command.kind === "saving";
  const editing = editingId ? capabilities.find((item) => item.id === editingId) : undefined;

  function focusFirstError(errors: Record<string, string[]>) {
    const field = fieldOrder.find((name) => (errors[name] ?? []).length > 0) ?? Object.keys(errors)[0];

    if (field) {
      fieldRefs.current[field]?.focus();
    }
  }

  function startEdit(capability: CapabilityViewResponse) {
    setEditingId(capability.id);
    setDraft(toDraft(capability));
    setCommand({ kind: "idle" });
    setAnnouncement(`The form edits ${capability.label}.`);
  }

  function startCreate() {
    setEditingId(null);
    setDraft(emptyDraft());
    setCommand({ kind: "idle" });
    idempotencyKey.current = newIdempotencyKey();
    setAnnouncement("The form declares a new capability.");
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (pending) {
      return;
    }

    setCommand({ kind: "saving" });
    setAnnouncement("");

    const body = {
      kind: draft.kind,
      label: draft.label.trim(),
      tags: toTags(draft.tags),
      visibility: draft.visibility,
      available: draft.available,
      metadata: toMetadata(draft),
    };

    const result = editing
      ? await updateCapability(
          editing.id,
          { expected_version: Number(editing.version), ...body },
          csrfToken,
        )
      : await createCapability(body, csrfToken, idempotencyKey.current);

    setCommand(result);

    if (result.kind === "saved") {
      await load();

      setAnnouncement(
        editing
          ? `${result.capability.label} is saved.`
          : `${result.capability.label} is declared.`,
      );

      if (!editing) {
        setDraft(emptyDraft());
        idempotencyKey.current = newIdempotencyKey();
      }

      triggerRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields need another value before the capability can be saved.");
      focusFirstError(result.fieldErrors);
      return;
    }

    if (result.kind === "conflict") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "forbidden" || result.kind === "refused") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to change your capabilities.");
      return;
    }

    if (result.kind === "not-found") {
      setAnnouncement("This capability does not exist or is not visible to you any more.");
      return;
    }

    if (result.kind === "unavailable" || result.kind === "error") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The capability was not changed.");
  }

  async function withdraw(capability: CapabilityViewResponse) {
    if (pending) {
      return;
    }

    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await withdrawCapability(
      capability.id,
      { expected_version: Number(capability.version) },
      csrfToken,
    );

    setCommand(result);
    setWithdrawing(null);

    if (result.kind === "saved") {
      await load();
      setAnnouncement(`${capability.label} is withdrawn. It keeps its history and matches no task any more.`);
      triggerRef.current?.focus();
      return;
    }

    if (result.kind === "conflict") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "forbidden" || result.kind === "refused") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to change your capabilities.");
      return;
    }

    if (result.kind === "unavailable" || result.kind === "error") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The capability was not withdrawn.");
  }

  return (
    <section aria-labelledby="capability-settings-heading" className="reading-width">
      <h1 id="capability-settings-heading" className="text-title font-semibold">
        Your capabilities
      </h1>
      <p className="mt-2 text-muted-foreground">
        Declare what you can work with. Only a public and available declaration is used for a match, and a
        declaration never holds a password, a cookie or a token.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>{editing ? `Edit ${editing.label}` : "Declare a capability"}</CardTitle>
          <CardDescription>
            {editing
              ? "Change the content of the declaration. The version protects your change against a lost update."
              : "A public and available declaration can be matched against a task."}
          </CardDescription>
        </CardHeader>

        <CardContent>
          <form className="flex flex-col gap-5" onSubmit={submit} noValidate>
            <Field label="Kind" error={fieldErrors.kind}>
              {({ id, describedBy, invalid }) => (
                <Select
                  id={id}
                  name="kind"
                  value={draft.kind}
                  aria-describedby={describedBy}
                  aria-invalid={invalid}
                  ref={(element) => {
                    fieldRefs.current.kind = element;
                  }}
                  onChange={(event) => setDraft({ ...draft, kind: event.target.value })}
                >
                  {kinds.map((kind) => (
                    <option key={kind} value={kind}>
                      {kind}
                    </option>
                  ))}
                </Select>
              )}
            </Field>

            <Field label="Label" error={fieldErrors.label} hint="1 to 100 characters. The name a visitor reads.">
              {({ id, describedBy, invalid }) => (
                <TextInput
                  id={id}
                  name="label"
                  value={draft.label}
                  aria-describedby={describedBy}
                  aria-invalid={invalid}
                  ref={(element) => {
                    fieldRefs.current.label = element;
                  }}
                  onChange={(event) => setDraft({ ...draft, label: event.target.value })}
                />
              )}
            </Field>

            <Field label="Tags" error={fieldErrors.tags} hint="At most 20 tags, separated by commas.">
              {({ id, describedBy, invalid }) => (
                <TextInput
                  id={id}
                  name="tags"
                  value={draft.tags}
                  aria-describedby={describedBy}
                  aria-invalid={invalid}
                  ref={(element) => {
                    fieldRefs.current.tags = element;
                  }}
                  onChange={(event) => setDraft({ ...draft, tags: event.target.value })}
                />
              )}
            </Field>

            <Field label="Visibility" error={fieldErrors.visibility}>
              {({ id, describedBy, invalid }) => (
                <Select
                  id={id}
                  name="visibility"
                  value={draft.visibility}
                  aria-describedby={describedBy}
                  aria-invalid={invalid}
                  ref={(element) => {
                    fieldRefs.current.visibility = element;
                  }}
                  onChange={(event) => setDraft({ ...draft, visibility: event.target.value })}
                >
                  <option value="public">public</option>
                  <option value="private">private</option>
                </Select>
              )}
            </Field>

            <div className="flex items-start gap-3">
              <Checkbox
                id="capability-available"
                name="available"
                checked={draft.available}
                ref={(element) => {
                  fieldRefs.current.available = element;
                }}
                onChange={(event) => setDraft({ ...draft, available: event.target.checked })}
              />
              <label htmlFor="capability-available" className="text-small">
                I can offer this capability right now. An unavailable declaration is never used for a match.
              </label>
            </div>

            <details className="rounded-md border border-border p-3">
              <summary className="text-small font-medium">Identifiers (optional)</summary>
              <p className="mt-2 text-small text-muted-foreground">
                Only these keys are accepted. A value holds letters, digits, dots, dashes and underscores.
              </p>
              <div className="mt-3 flex flex-col gap-3">
                {metadataKeys.map((key) => (
                  <Field key={key} label={key} error={fieldErrors.metadata}>
                    {({ id, describedBy, invalid }) => (
                      <TextInput
                        id={id}
                        name={`metadata-${key}`}
                        value={draft.metadata[key] ?? ""}
                        aria-describedby={describedBy}
                        aria-invalid={invalid}
                        onChange={(event) =>
                          setDraft({ ...draft, metadata: { ...draft.metadata, [key]: event.target.value } })
                        }
                      />
                    )}
                  </Field>
                ))}
              </div>
            </details>

            <div className="flex flex-wrap items-center gap-3">
              <Button ref={triggerRef} type="submit" disabled={pending} aria-busy={pending}>
                {pending
                  ? "Saving the capability"
                  : editing
                    ? "Save the capability"
                    : "Declare the capability"}
              </Button>

              {editing ? (
                <Button type="button" variant="secondary" onClick={startCreate}>
                  Cancel the change
                </Button>
              ) : null}
            </div>

            <p role="status" aria-live="polite" className="sr-only">
              {announcement}
            </p>
          </form>
        </CardContent>
      </Card>

      <h2 className="mt-10 text-large font-semibold">Your declarations</h2>

      {capabilities.length === 0 ? (
        <div className="mt-4 rounded-lg border border-border bg-surface p-6">
          <p className="text-muted-foreground">
            You have not declared a capability yet. Declare your device, your region or your role, so a task can
            be matched against it.
          </p>
        </div>
      ) : (
        <ul className="mt-4 flex flex-col gap-4">
          {capabilities.map((capability) => (
            <li key={capability.id}>
              <article className="rounded-lg border border-border bg-surface p-4 sm:p-5">
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="text-large font-semibold">{capability.label}</h3>
                  <KindBadge kind={capability.kind} />
                  <CapabilityVisibilityBadge visibility={capability.visibility} />
                  <CapabilityStatusBadge status={capability.status} />
                </div>

                {capability.tags.length > 0 ? (
                  <p className="mt-2 text-small text-muted-foreground">Tags: {capability.tags.join(", ")}</p>
                ) : null}

                {Object.keys(capability.metadata).length > 0 ? (
                  <p className="mt-1 text-small text-muted-foreground">
                    {Object.entries(capability.metadata)
                      .map(([key, value]) => `${key}: ${value}`)
                      .join(", ")}
                  </p>
                ) : null}

                <p className="mt-2 text-small text-muted-foreground">
                  Verification: {capability.verification === "verified" ? "verified" : "self declared"}.{" "}
                  {capability.usable_for_matching
                    ? "This declaration is used for a match."
                    : "This declaration is not used for a match, because a match only uses public and available declarations."}
                </p>

                {capability.status_history.length > 0 ? (
                  <ul className="mt-3 flex flex-col gap-1 text-small text-muted-foreground">
                    {capability.status_history.map((change, index) => (
                      <li key={`${change.changed_at}-${index}`}>
                        {change.from_status} to {change.to_status}, {formatDate(change.changed_at)}
                      </li>
                    ))}
                  </ul>
                ) : null}

                {capability.status === "withdrawn" ? (
                  <p className="mt-3 text-small text-muted-foreground">
                    This declaration is withdrawn. It keeps its history and matches no task any more.
                  </p>
                ) : (
                  <div className="mt-4 flex flex-wrap items-center gap-3">
                    <Button type="button" variant="secondary" onClick={() => startEdit(capability)}>
                      Edit the declaration
                    </Button>

                    <Button
                      type="button"
                      variant="ghost"
                      onClick={() => {
                        setWithdrawing(capability.id);
                        setAnnouncement(`The withdrawal of ${capability.label} has to be confirmed.`);
                      }}
                    >
                      Withdraw the declaration
                    </Button>
                  </div>
                )}

                {withdrawing === capability.id ? (
                  <div className="mt-4 rounded-md border border-warning bg-surface p-3">
                    <p className="text-small">
                      The declaration is withdrawn. It keeps its row and its history, but it matches no task any
                      more.
                    </p>
                    <p className="mt-3 flex flex-wrap gap-3">
                      <Button
                        type="button"
                        disabled={pending}
                        onClick={() => void withdraw(capability)}
                      >
                        Yes, withdraw it
                      </Button>
                      <Button
                        type="button"
                        variant="secondary"
                        onClick={() => {
                          setWithdrawing(null);
                          setAnnouncement("The withdrawal was cancelled.");
                        }}
                      >
                        Keep the declaration
                      </Button>
                    </p>
                  </div>
                ) : null}
              </article>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function newIdempotencyKey(): string {
  return `capability-create-${crypto.randomUUID()}`;
}

function formatDate(value: string): string {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toISOString().slice(0, 10);
}
