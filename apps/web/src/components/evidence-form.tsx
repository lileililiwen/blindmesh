"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { CheckboxField, Field, TextArea, TextInput } from "@/components/ui/field";
import { useSession } from "@/components/session-provider";
import {
  completeAttachment,
  createAttachmentIntent,
  newEvidenceKey,
  submitEvidence,
  type AttachmentCommandState,
  type SubmissionCommandState,
} from "@/lib/findings";
import { loadParticipation, type ParticipationDetailState } from "@/lib/participations";

type Draft = {
  summary: string;
  scopeCompleted: boolean;
  results: Record<string, string>;
  testedAreas: string;
  cleanResult: string;
};

type FindingDraft = {
  category: string;
  severity: string;
  title: string;
  reproduction: string;
  expectedResult: string;
  actualResult: string;
};

const categories = ["bug", "ux", "performance", "security", "accessibility", "compatibility", "documentation"];
const severities = ["info", "low", "medium", "high", "critical"];

const emptyFinding: FindingDraft = {
  category: "bug",
  severity: "medium",
  title: "",
  reproduction: "",
  expectedResult: "",
  actualResult: "",
};

/**
 * The evidence of a seat that the caller runs. The page keeps the draft of the
 * contributor when the server refuses the evidence, so nothing is lost. The
 * files of the evidence are private and they are only readable after they left
 * the quarantine.
 */
export function EvidenceForm({ participationId }: { participationId: string }) {
  const { state: session, csrfToken } = useSession();
  const [seat, setSeat] = useState<ParticipationDetailState>({ kind: "loading" });
  const [command, setCommand] = useState<SubmissionCommandState>({ kind: "idle" });
  const [attachment, setAttachment] = useState<AttachmentCommandState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const [draft, setDraft] = useState<Draft>({
    summary: "",
    scopeCompleted: true,
    results: {},
    testedAreas: "",
    cleanResult: "",
  });
  const [findings, setFindings] = useState<FindingDraft[]>([]);
  const [submissionId, setSubmissionId] = useState<string | null>(null);
  const [files, setFiles] = useState<{ id: string; name: string; state: string }[]>([]);
  const submitRef = useRef<HTMLButtonElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const commandKeys = useRef<Record<string, string>>({});

  function keyFor(operation: string): string {
    const name = `${operation}:${participationId}`;

    commandKeys.current[name] ??= newEvidenceKey();

    return commandKeys.current[name];
  }

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadParticipation(participationId);

      if (!cancelled) {
        setSeat(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [participationId]);

  if (seat.kind === "loading") {
    return (
      <section aria-labelledby="evidence-heading" aria-busy="true" className="reading-width">
        <h1 id="evidence-heading" className="text-title font-semibold">
          The evidence of your seat
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading your seat.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (seat.kind === "not-found") {
    return (
      <section aria-labelledby="evidence-heading" className="reading-width">
        <h1 id="evidence-heading" className="text-title font-semibold">
          The evidence of your seat
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This seat does not exist. It may belong to a task of a private project.
        </p>
        <p className="mt-4">
          <Button asChild variant="secondary">
            <Link href="/work">Back to your work</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (seat.kind === "unauthorized") {
    return (
      <section aria-labelledby="evidence-heading" className="reading-width">
        <h1 id="evidence-heading" className="text-title font-semibold">
          The evidence of your seat
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to send the evidence. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent(`/work/${participationId}/submit`)}`}>
              Sign in to continue
            </a>
          </Button>
        </p>
      </section>
    );
  }

  if (seat.kind === "error" || seat.kind === "unavailable") {
    return (
      <section aria-labelledby="evidence-heading" className="reading-width">
        <h1 id="evidence-heading" className="text-title font-semibold">
          The evidence of your seat
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {seat.message}
        </p>
      </section>
    );
  }

  const current = seat.participation;
  const signedIn = session.kind === "authenticated";
  const steps = current.steps ?? [];
  const explore = (current.modes ?? []).includes("explore");
  const verify = (current.modes ?? []).includes("verify");
  const mayWrite = signedIn && (current.state === "running" || current.state === "changes_requested");
  const fieldErrors = command.kind === "invalid" ? command.fieldErrors : {};

  return (
    <section aria-labelledby="evidence-heading" className="reading-width">
      <div className="flex flex-wrap items-center gap-2">
        <h1 id="evidence-heading" className="text-title font-semibold">
          The evidence of your seat
        </h1>
        <Badge tone={current.state === "running" ? "success" : "neutral"}>{current.state}</Badge>
      </div>

      <p className="mt-2 text-small text-muted-foreground">
        {current.task_title}. The owner of the task reads your evidence and decides about the credits of
        the seat.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>What you report</CardTitle>
          <CardDescription>
            The summary of your work, the result of every step and the findings you found.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form
            className="flex flex-col gap-4"
            onSubmit={(event) => {
              event.preventDefault();
              void send();
            }}
          >
            <Field
              label="The summary of your work"
              error={fieldErrors.summary}
              hint="Between 1 and 5000 characters. It is the first thing the owner reads."
            >
              {({ id, describedBy, invalid }) => (
                <TextArea
                  id={id}
                  aria-describedby={describedBy}
                  aria-invalid={invalid}
                  rows={5}
                  maxLength={5000}
                  value={draft.summary}
                  onChange={(event) => setDraft({ ...draft, summary: event.target.value })}
                />
              )}
            </Field>

            <CheckboxField
              id="scope-completed"
              label="I completed the whole scope of the seat."
              checked={draft.scopeCompleted}
              onChange={(event) => setDraft({ ...draft, scopeCompleted: event.target.checked })}
            />

            {verify && steps.length > 0 ? (
              <fieldset className="flex flex-col gap-3">
                <legend className="text-small font-medium">The result of every step</legend>

                {fieldErrors.steps ? (
                  <p className="text-small text-danger">{fieldErrors.steps.join(" ")}</p>
                ) : null}

                {steps.map((step) => (
                  <Field key={step.position} label={step.title} hint={`Expected: ${step.expected_result}`}>
                    {({ id, describedBy, invalid }) => (
                      <TextArea
                        id={id}
                        aria-describedby={describedBy}
                        aria-invalid={invalid}
                        rows={3}
                        value={draft.results[step.position] ?? ""}
                        onChange={(event) =>
                          setDraft({
                            ...draft,
                            results: { ...draft.results, [step.position]: event.target.value },
                          })
                        }
                      />
                    )}
                  </Field>
                ))}
              </fieldset>
            ) : null}

            {explore ? (
              <Field
                label="The areas you covered"
                error={fieldErrors.tested_areas}
                hint="One area per line. An exploration names the areas it covered."
              >
                {({ id, describedBy, invalid }) => (
                  <TextArea
                    id={id}
                    aria-describedby={describedBy}
                    aria-invalid={invalid}
                    rows={3}
                    value={draft.testedAreas}
                    onChange={(event) => setDraft({ ...draft, testedAreas: event.target.value })}
                  />
                )}
              </Field>
            ) : null}

            {findings.length === 0 ? (
              <Field
                label="The explanation of your clean result"
                error={fieldErrors.clean_result_explanation}
                hint="A result without a finding needs the explanation of what you checked."
              >
                {({ id, describedBy, invalid }) => (
                  <TextArea
                    id={id}
                    aria-describedby={describedBy}
                    aria-invalid={invalid}
                    rows={3}
                    value={draft.cleanResult}
                    onChange={(event) => setDraft({ ...draft, cleanResult: event.target.value })}
                  />
                )}
              </Field>
            ) : null}

            <div className="flex flex-col gap-3">
              <h3 className="text-small font-medium">The findings you found</h3>

              {fieldErrors.findings ? (
                <p className="text-small text-danger">{fieldErrors.findings.join(" ")}</p>
              ) : null}

              {findings.map((finding, index) => (
                <div key={index} className="flex flex-col gap-3 rounded-md border border-border p-3">
                  <div className="grid gap-3 sm:grid-cols-2">
                    <Field label="The category of the finding" error={fieldErrors[`findings[${index}].category`]}>
                      {({ id, describedBy, invalid }) => (
                        <select
                          id={id}
                          aria-describedby={describedBy}
                          aria-invalid={invalid}
                          className="min-h-[var(--component-control-min-height)] w-full rounded-md border border-border bg-surface px-3 py-2 text-body"
                          value={finding.category}
                          onChange={(event) => updateFinding(index, { category: event.target.value })}
                        >
                          {categories.map((category) => (
                            <option key={category} value={category}>
                              {category}
                            </option>
                          ))}
                        </select>
                      )}
                    </Field>

                    <Field label="The severity of the finding" error={fieldErrors[`findings[${index}].severity`]}>
                      {({ id, describedBy, invalid }) => (
                        <select
                          id={id}
                          aria-describedby={describedBy}
                          aria-invalid={invalid}
                          className="min-h-[var(--component-control-min-height)] w-full rounded-md border border-border bg-surface px-3 py-2 text-body"
                          value={finding.severity}
                          onChange={(event) => updateFinding(index, { severity: event.target.value })}
                        >
                          {severities.map((severity) => (
                            <option key={severity} value={severity}>
                              {severity}
                            </option>
                          ))}
                        </select>
                      )}
                    </Field>
                  </div>

                  <Field label="The title of the finding" error={fieldErrors[`findings[${index}].title`]}>
                    {({ id, describedBy, invalid }) => (
                      <TextInput
                        id={id}
                        aria-describedby={describedBy}
                        aria-invalid={invalid}
                        maxLength={140}
                        value={finding.title}
                        onChange={(event) => updateFinding(index, { title: event.target.value })}
                      />
                    )}
                  </Field>

                  <Field label="How to reproduce it" error={fieldErrors[`findings[${index}].reproduction`]}>
                    {({ id, describedBy, invalid }) => (
                      <TextArea
                        id={id}
                        aria-describedby={describedBy}
                        aria-invalid={invalid}
                        rows={3}
                        maxLength={10000}
                        value={finding.reproduction}
                        onChange={(event) => updateFinding(index, { reproduction: event.target.value })}
                      />
                    )}
                  </Field>

                  <div className="grid gap-3 sm:grid-cols-2">
                    <Field label="The expected result" error={fieldErrors[`findings[${index}].expected_result`]}>
                      {({ id, describedBy, invalid }) => (
                        <TextArea
                          id={id}
                          aria-describedby={describedBy}
                          aria-invalid={invalid}
                          rows={2}
                          maxLength={5000}
                          value={finding.expectedResult}
                          onChange={(event) => updateFinding(index, { expectedResult: event.target.value })}
                        />
                      )}
                    </Field>

                    <Field label="The actual result" error={fieldErrors[`findings[${index}].actual_result`]}>
                      {({ id, describedBy, invalid }) => (
                        <TextArea
                          id={id}
                          aria-describedby={describedBy}
                          aria-invalid={invalid}
                          rows={2}
                          maxLength={5000}
                          value={finding.actualResult}
                          onChange={(event) => updateFinding(index, { actualResult: event.target.value })}
                        />
                      )}
                    </Field>
                  </div>

                  <Button
                    type="button"
                    variant="secondary"
                    size="small"
                    onClick={() => setFindings(findings.filter((_, position) => position !== index))}
                  >
                    Remove the finding
                  </Button>
                </div>
              ))}

              <Button
                type="button"
                variant="secondary"
                size="small"
                onClick={() => setFindings([...findings, { ...emptyFinding }])}
              >
                Add a finding
              </Button>
            </div>

            <div className="flex flex-wrap items-center gap-3">
              <Button ref={submitRef} type="submit" variant="primary" disabled={!mayWrite || command.kind === "saving"}>
                {command.kind === "saving" ? "Sending the evidence" : "Send the evidence"}
              </Button>

              <Button asChild variant="secondary">
                <Link href={`/work/${participationId}`}>Back to your seat</Link>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      {submissionId ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>The files of your evidence</CardTitle>
            <CardDescription>
              A text, a log or a picture. The file stays private and it is readable after it left the
              quarantine.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="flex flex-wrap items-center gap-3">
              <Field label="Choose a file" error={attachment.kind === "invalid" ? attachment.fieldErrors.file_name : undefined}>
                {({ id, describedBy, invalid }) => (
                  <TextInput
                    id={id}
                    ref={fileRef}
                    type="file"
                    aria-describedby={describedBy}
                    aria-invalid={invalid}
                    accept=".txt,.log,.md,.csv,.json,.png,.jpg,.jpeg,.webp"
                    onChange={(event) => {
                      const file = event.target.files?.[0];

                      if (file) {
                        void attach(submissionId, file);
                      }
                    }}
                  />
                )}
              </Field>
            </div>

            {files.length > 0 ? (
              <ul className="flex flex-col gap-1 text-small">
                {files.map((file) => (
                  <li key={file.id}>
                    {file.name} — {file.state}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-small text-muted-foreground">No file was attached yet.</p>
            )}

            {attachment.kind === "refused" || attachment.kind === "forbidden" ? (
              <p className="text-small text-warning">{attachment.message}</p>
            ) : null}

            {attachment.kind === "error" || attachment.kind === "unavailable" ? (
              <p className="text-small text-danger">{attachment.message}</p>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {command.kind === "refused" || command.kind === "conflict" || command.kind === "forbidden" ? (
        <p className="mt-4 text-small text-warning">{command.message}</p>
      ) : null}

      {command.kind === "error" || command.kind === "unavailable" ? (
        <p className="mt-4 text-small text-danger">{command.message}</p>
      ) : null}
    </section>
  );

  function updateFinding(index: number, change: Partial<FindingDraft>) {
    setFindings(findings.map((finding, position) => (position === index ? { ...finding, ...change } : finding)));
  }

  async function send() {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await submitEvidence(
      participationId,
      {
        summary: draft.summary,
        scope_completed: draft.scopeCompleted,
        steps: steps.map((step) => ({
          position: step.position,
          title: step.title,
          result: draft.results[step.position] ?? "",
        })),
        tested_areas: draft.testedAreas
          .split("\n")
          .map((area) => area.trim())
          .filter((area) => area.length > 0),
        clean_result_explanation: draft.cleanResult,
        findings: findings.map((finding) => ({
          category: finding.category,
          severity: finding.severity,
          title: finding.title,
          reproduction: finding.reproduction,
          expected_result: finding.expectedResult,
          actual_result: finding.actualResult,
        })),
      },
      csrfToken,
      keyFor("submit"),
    );

    setCommand(result);

    if (result.kind === "saved") {
      setSubmissionId(result.submission.id);
      setAnnouncement("The evidence is sent. The owner of the task reads it now.");
      submitRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields of the evidence are not valid. The draft stays on this page.");
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to send the evidence.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The evidence was not sent.");
  }

  async function attach(submission: string, file: File) {
    setAttachment({ kind: "saving" });

    const declared = await createAttachmentIntent(
      {
        submission_id: submission,
        file_name: file.name,
        content_type: file.type || "text/plain",
        size_bytes: file.size,
      },
      csrfToken,
      newEvidenceKey(),
    );

    setAttachment(declared);

    if (declared.kind !== "saved") {
      return;
    }

    const uploaded = await completeAttachment(declared.attachment.id, file, csrfToken, newEvidenceKey());

    setAttachment(uploaded);

    if (uploaded.kind === "saved") {
      setFiles([
        ...files,
        { id: uploaded.attachment.id, name: uploaded.attachment.file_name, state: uploaded.attachment.state },
      ]);
      setAnnouncement(`The file ${uploaded.attachment.file_name} left the quarantine.`);
      return;
    }

    if (uploaded.kind === "invalid" || uploaded.kind === "refused" || uploaded.kind === "forbidden") {
      setAnnouncement("The file was refused. It stays in the quarantine and it is not readable.");
      return;
    }

    setAnnouncement("The file was not uploaded.");
  }
}
