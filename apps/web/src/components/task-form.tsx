"use client";

import { useEffect, useMemo, useRef, useState } from "react";

import { Button } from "@/components/ui/button";
import { CheckboxField, Field, Fieldset, Select, TextArea, TextInput } from "@/components/ui/field";
import { createTask, updateTask, type TaskCommandState } from "@/lib/tasks";
import type { ProjectSummaryResponse, TaskDetailResponse } from "@blindmesh/api-client";

/** The order of the fields. The first field with an error takes the focus. */
const fieldOrder = [
  "project_id",
  "title",
  "scope",
  "modes",
  "visibility",
  "expected_minutes",
  "base_reward",
  "slot_bonus",
  "slot_count",
  "deadline",
  "accepts_clean_findings",
  "access_kind",
  "access_resource",
  "review_criteria",
  "requirements",
  "steps",
] as const;

type RequirementDraft = { kind: string; value: string };
type StepDraft = { title: string; expectedResult: string };

type Draft = {
  projectId: string;
  title: string;
  scope: string;
  modes: string[];
  visibility: string;
  expectedMinutes: string;
  baseReward: string;
  slotBonus: string;
  slotCount: string;
  deadline: string;
  acceptsCleanFindings: boolean;
  accessKind: string;
  accessResource: string;
  reviewCriteria: string;
  requirements: RequirementDraft[];
  steps: StepDraft[];
};

const emptyDraft: Draft = {
  projectId: "",
  title: "",
  scope: "",
  modes: [],
  visibility: "private",
  expectedMinutes: "30",
  baseReward: "10",
  slotBonus: "0",
  slotCount: "1",
  deadline: "",
  acceptsCleanFindings: false,
  accessKind: "",
  accessResource: "",
  reviewCriteria: "",
  requirements: [{ kind: "region", value: "" }],
  steps: [{ title: "", expectedResult: "" }],
};

const modeOptions = [
  { value: "explore", label: "Explore" },
  { value: "verify", label: "Verify" },
  { value: "access", label: "Access" },
  { value: "review", label: "Review" },
];

/**
 * The form of a task. It writes a new task and it changes a draft. The draft
 * of the visitor is kept when the server refuses the command, and the result
 * is only announced after the server committed it.
 */
export function TaskForm({
  variant,
  projects,
  task,
  csrfToken,
  onCommitted,
}: {
  variant: "create" | "edit";
  projects: ProjectSummaryResponse[];
  task?: TaskDetailResponse;
  csrfToken: string;
  onCommitted: (task: TaskDetailResponse) => void;
}) {
  const [draft, setDraft] = useState<Draft>(() => (task ? toDraft(task) : emptyDraft));
  const [command, setCommand] = useState<TaskCommandState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const fieldRefs = useRef<Record<string, HTMLElement | null>>({});
  const triggerRef = useRef<HTMLButtonElement>(null);
  const idempotencyKey = useRef<string>("");

  useEffect(() => {
    idempotencyKey.current = createIdempotencyKey();
  }, []);

  const fieldErrors = command.kind === "invalid" ? command.fieldErrors : {};
  const pending = command.kind === "saving";

  const maximumBudget = useMemo(() => {
    const base = Number(draft.baseReward);
    const bonus = Number(draft.slotBonus);
    const slots = Number(draft.slotCount);

    if (![base, bonus, slots].every((value) => Number.isFinite(value))) {
      return 0;
    }

    return (base + bonus) * slots;
  }, [draft.baseReward, draft.slotBonus, draft.slotCount]);

  function focusFirstError(errors: Record<string, string[]>) {
    const field = fieldOrder.find((name) => (errors[name] ?? []).length > 0) ?? Object.keys(errors)[0];

    if (field) {
      fieldRefs.current[field]?.focus();
    }
  }

  function toggleMode(mode: string) {
    setDraft((current) => ({
      ...current,
      modes: current.modes.includes(mode)
        ? current.modes.filter((item) => item !== mode)
        : [...current.modes, mode],
    }));
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (pending) {
      return;
    }

    setCommand({ kind: "saving" });
    setAnnouncement("");

    const body = {
      project_id: draft.projectId,
      title: draft.title,
      scope: draft.scope,
      modes: draft.modes,
      visibility: draft.visibility,
      expected_minutes: Number(draft.expectedMinutes),
      base_reward: Number(draft.baseReward),
      slot_bonus: Number(draft.slotBonus),
      slot_count: Number(draft.slotCount),
      deadline: draft.deadline ? new Date(draft.deadline).toISOString() : null,
      accepts_clean_findings: draft.acceptsCleanFindings,
      access_kind: draft.accessKind === "" ? null : draft.accessKind,
      access_resource: draft.accessResource === "" ? null : draft.accessResource,
      review_criteria: draft.reviewCriteria === "" ? null : draft.reviewCriteria,
      requirements: draft.requirements.map((item) => ({ kind: item.kind, value: item.value })),
      steps: draft.steps.map((item) => ({ title: item.title, expected_result: item.expectedResult })),
    };

    const result =
      variant === "create"
        ? await createTask(body, csrfToken, idempotencyKey.current)
        : await updateTask(
            task!.id,
            { ...body, expected_version: task!.version },
            csrfToken,
          );

    setCommand(result);

    if (result.kind === "saved") {
      setAnnouncement(
        variant === "create" ? "The task is written as a draft." : "The task is saved.",
      );
      onCommitted(result.task);
      triggerRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields need another value before the task can be saved.");
      focusFirstError(result.fieldErrors);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to change the task.");
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The task was not saved.");
  }

  return (
    <form className="flex flex-col gap-5" onSubmit={submit} noValidate>
      {variant === "create" ? (
        <Field
          label="Project"
          error={fieldErrors.project_id}
          hint="A task belongs to a project. You publish a task in a project you own or in a project you are an editor of."
        >
          {({ id, describedBy, invalid }) => (
            <Select
              id={id}
              name="project_id"
              value={draft.projectId}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.project_id = element;
              }}
              onChange={(event) => setDraft({ ...draft, projectId: event.target.value })}
            >
              <option value="">Choose a project</option>
              {projects.map((project) => (
                <option key={project.id} value={project.id}>
                  {project.name}
                </option>
              ))}
            </Select>
          )}
        </Field>
      ) : null}

      <Field label="Title" error={fieldErrors.title} hint="At most 140 characters.">
        {({ id, describedBy, invalid }) => (
          <TextInput
            id={id}
            name="title"
            value={draft.title}
            maxLength={140}
            aria-describedby={describedBy}
            aria-invalid={invalid}
            ref={(element) => {
              fieldRefs.current.title = element;
            }}
            onChange={(event) => setDraft({ ...draft, title: event.target.value })}
          />
        )}
      </Field>

      <Field
        label="Scope"
        error={fieldErrors.scope}
        hint="What the contributor has to do. At most 10000 characters. Never write a password or a token here."
      >
        {({ id, describedBy, invalid }) => (
          <TextArea
            id={id}
            name="scope"
            rows={6}
            value={draft.scope}
            aria-describedby={describedBy}
            aria-invalid={invalid}
            ref={(element) => {
              fieldRefs.current.scope = element;
            }}
            onChange={(event) => setDraft({ ...draft, scope: event.target.value })}
          />
        )}
      </Field>

      <Fieldset
        legend="Modes"
        error={fieldErrors.modes}
        hint="A task carries at least one mode: explore, verify, access or review."
      >
        {({ describedBy }) => (
          <div className="flex flex-col gap-2">
            {modeOptions.map((option) => (
              <CheckboxField
                key={option.value}
                id={`task-mode-${option.value}`}
                label={option.label}
                checked={draft.modes.includes(option.value)}
                describedBy={describedBy}
                onChange={() => toggleMode(option.value)}
              />
            ))}
          </div>
        )}
      </Fieldset>

      <Field
        label="Visibility"
        error={fieldErrors.visibility}
        hint="Private is visible for you and your team only. Matched shows the summary to the marketplace. Public shows the whole task."
      >
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
            <option value="private">Private</option>
            <option value="matched">Matched</option>
            <option value="public">Public</option>
          </Select>
        )}
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field
          label="Expected minutes"
          error={fieldErrors.expected_minutes}
          hint="Between 5 and 480 minutes for one slot."
        >
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="expected_minutes"
              type="number"
              inputMode="numeric"
              min={5}
              max={480}
              value={draft.expectedMinutes}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.expected_minutes = element;
              }}
              onChange={(event) => setDraft({ ...draft, expectedMinutes: event.target.value })}
            />
          )}
        </Field>

        <Field
          label="Deadline"
          error={fieldErrors.deadline}
          hint="At least one day and at most thirty days from the publication."
        >
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="deadline"
              type="datetime-local"
              value={draft.deadline}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.deadline = element;
              }}
              onChange={(event) => setDraft({ ...draft, deadline: event.target.value })}
            />
          )}
        </Field>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <Field
          label="Base reward"
          error={fieldErrors.base_reward}
          hint="The credits of one slot. Between 1 and 10000."
        >
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="base_reward"
              type="number"
              inputMode="numeric"
              min={1}
              max={10000}
              value={draft.baseReward}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.base_reward = element;
              }}
              onChange={(event) => setDraft({ ...draft, baseReward: event.target.value })}
            />
          )}
        </Field>

        <Field
          label="Bonus"
          error={fieldErrors.slot_bonus}
          hint="The extra credits of one slot. Between 0 and 10000."
        >
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="slot_bonus"
              type="number"
              inputMode="numeric"
              min={0}
              max={10000}
              value={draft.slotBonus}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.slot_bonus = element;
              }}
              onChange={(event) => setDraft({ ...draft, slotBonus: event.target.value })}
            />
          )}
        </Field>

        <Field
          label="Slots"
          error={fieldErrors.slot_count}
          hint="The number of contributors. Between 1 and 20."
        >
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="slot_count"
              type="number"
              inputMode="numeric"
              min={1}
              max={20}
              value={draft.slotCount}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.slot_count = element;
              }}
              onChange={(event) => setDraft({ ...draft, slotCount: event.target.value })}
            />
          )}
        </Field>
      </div>

      <p className="text-small text-muted-foreground">
        The whole budget of {maximumBudget} credits is reserved when the task is published.
      </p>

      {draft.modes.includes("explore") ? (
        <Field
          label="A clean result"
          error={fieldErrors.accepts_clean_findings}
          hint="An explore task accepts a report that holds no finding."
        >
          {({ id, describedBy, invalid }) => (
            <CheckboxField
              id={id}
              label="A report without a finding is a valid result of this task."
              checked={draft.acceptsCleanFindings}
              describedBy={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.accepts_clean_findings = element;
              }}
              onChange={(event) => setDraft({ ...draft, acceptsCleanFindings: event.target.checked })}
            />
          )}
        </Field>
      ) : null}

      {draft.modes.includes("access") ? (
        <div className="flex flex-col gap-4">
          <Field
            label="Access"
            error={fieldErrors.access_kind}
            hint="Name the resource you operate or the consent of its owner. Never a credential."
          >
            {({ id, describedBy, invalid }) => (
              <Select
                id={id}
                name="access_kind"
                value={draft.accessKind}
                aria-describedby={describedBy}
                aria-invalid={invalid}
                ref={(element) => {
                  fieldRefs.current.access_kind = element;
                }}
                onChange={(event) => setDraft({ ...draft, accessKind: event.target.value })}
              >
                <option value="">Choose the kind of the access</option>
                <option value="owner_operated">A resource I operate</option>
                <option value="delegated_consent">A delegated consent of the owner</option>
              </Select>
            )}
          </Field>

          <Field
            label="Resource or consent"
            error={fieldErrors.access_resource}
            hint="The address of the resource or the reference of the consent."
          >
            {({ id, describedBy, invalid }) => (
              <TextInput
                id={id}
                name="access_resource"
                value={draft.accessResource}
                aria-describedby={describedBy}
                aria-invalid={invalid}
                ref={(element) => {
                  fieldRefs.current.access_resource = element;
                }}
                onChange={(event) => setDraft({ ...draft, accessResource: event.target.value })}
              />
            )}
          </Field>
        </div>
      ) : null}

      {draft.modes.includes("review") ? (
        <Field
          label="The structure of the assessment"
          error={fieldErrors.review_criteria}
          hint="A review task accepts a structured peer assessment. Name the parts of the assessment."
        >
          {({ id, describedBy, invalid }) => (
            <TextArea
              id={id}
              name="review_criteria"
              rows={4}
              value={draft.reviewCriteria}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.review_criteria = element;
              }}
              onChange={(event) => setDraft({ ...draft, reviewCriteria: event.target.value })}
            />
          )}
        </Field>
      ) : null}

      <Fieldset
        legend="Hard requirements"
        error={fieldErrors.requirements}
        hint="The capability the contributor has to offer. A published task names at least one requirement."
      >
        {({ describedBy }) => (
          <div className="flex flex-col gap-3">
            {draft.requirements.map((requirement, index) => (
              <div key={index} className="flex flex-col gap-2 sm:flex-row sm:items-end">
                <div className="sm:w-48">
                  <label htmlFor={`task-requirement-kind-${index}`} className="text-small font-medium">
                    Kind of requirement {index + 1}
                  </label>
                  <Select
                    id={`task-requirement-kind-${index}`}
                    name={`requirement_kind_${index}`}
                    value={requirement.kind}
                    aria-describedby={describedBy}
                    onChange={(event) =>
                      setDraft((current) => ({
                        ...current,
                        requirements: current.requirements.map((item, position) =>
                          position === index ? { ...item, kind: event.target.value } : item,
                        ),
                      }))
                    }
                  >
                    <option value="region">Region</option>
                    <option value="device">Device</option>
                    <option value="role">Role</option>
                  </Select>
                </div>

                <div className="flex-1">
                  <label htmlFor={`task-requirement-value-${index}`} className="text-small font-medium">
                    Value of requirement {index + 1}
                  </label>
                  <TextInput
                    id={`task-requirement-value-${index}`}
                    name={`requirement_value_${index}`}
                    value={requirement.value}
                    aria-describedby={describedBy}
                    onChange={(event) =>
                      setDraft((current) => ({
                        ...current,
                        requirements: current.requirements.map((item, position) =>
                          position === index ? { ...item, value: event.target.value } : item,
                        ),
                      }))
                    }
                  />
                </div>

                {draft.requirements.length > 1 ? (
                  <Button
                    type="button"
                    variant="secondary"
                    onClick={() =>
                      setDraft((current) => ({
                        ...current,
                        requirements: current.requirements.filter((_, position) => position !== index),
                      }))
                    }
                  >
                    Remove
                  </Button>
                ) : null}
              </div>
            ))}

            <div>
              <Button
                type="button"
                variant="secondary"
                onClick={() =>
                  setDraft((current) => ({
                    ...current,
                    requirements: [...current.requirements, { kind: "region", value: "" }],
                  }))
                }
              >
                Add a requirement
              </Button>
            </div>
          </div>
        )}
      </Fieldset>

      <Fieldset
        legend="Steps of the verification"
        error={fieldErrors.steps}
        hint="A verify task needs at least one step with an expected result."
      >
        {({ describedBy }) => (
          <div className="flex flex-col gap-3">
            {draft.steps.map((step, index) => (
              <div key={index} className="flex flex-col gap-2">
                <label htmlFor={`task-step-title-${index}`} className="text-small font-medium">
                  Step {index + 1}
                </label>
                <TextInput
                  id={`task-step-title-${index}`}
                  name={`step_title_${index}`}
                  value={step.title}
                  aria-describedby={describedBy}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      steps: current.steps.map((item, position) =>
                        position === index ? { ...item, title: event.target.value } : item,
                      ),
                    }))
                  }
                />
                <label htmlFor={`task-step-result-${index}`} className="text-small font-medium">
                  Expected result of step {index + 1}
                </label>
                <TextArea
                  id={`task-step-result-${index}`}
                  name={`step_result_${index}`}
                  rows={2}
                  value={step.expectedResult}
                  aria-describedby={describedBy}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      steps: current.steps.map((item, position) =>
                        position === index ? { ...item, expectedResult: event.target.value } : item,
                      ),
                    }))
                  }
                />

                {draft.steps.length > 1 ? (
                  <div>
                    <Button
                      type="button"
                      variant="secondary"
                      onClick={() =>
                        setDraft((current) => ({
                          ...current,
                          steps: current.steps.filter((_, position) => position !== index),
                        }))
                      }
                    >
                      Remove the step
                    </Button>
                  </div>
                ) : null}
              </div>
            ))}

            <div>
              <Button
                type="button"
                variant="secondary"
                onClick={() =>
                  setDraft((current) => ({
                    ...current,
                    steps: [...current.steps, { title: "", expectedResult: "" }],
                  }))
                }
              >
                Add a step
              </Button>
            </div>
          </div>
        )}
      </Fieldset>

      <div className="flex flex-wrap items-center gap-3">
        <Button ref={triggerRef} type="submit" disabled={pending} aria-busy={pending}>
          {pending
            ? "Saving the task"
            : variant === "create"
              ? "Write the task"
              : "Save the task"}
        </Button>
      </div>

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {command.kind === "refused" || command.kind === "conflict" || command.kind === "forbidden" ? (
        <p className="text-small text-warning">{command.message}</p>
      ) : null}

      {command.kind === "error" || command.kind === "unavailable" ? (
        <p className="text-small text-danger">{command.message}</p>
      ) : null}
    </form>
  );
}

/** The draft of an existing task. */
function toDraft(task: TaskDetailResponse): Draft {
  return {
    projectId: task.project_id,
    title: task.title,
    scope: task.scope,
    modes: task.modes,
    visibility: task.visibility,
    expectedMinutes: String(task.expected_minutes),
    baseReward: String(task.base_reward),
    slotBonus: String(task.slot_bonus),
    slotCount: String(task.slot_count),
    deadline: task.deadline ? toLocalInput(task.deadline) : "",
    acceptsCleanFindings: task.accepts_clean_findings,
    accessKind: task.access_kind ?? "",
    accessResource: task.access_resource ?? "",
    reviewCriteria: task.review_criteria ?? "",
    requirements: task.requirements.map((item) => ({ kind: item.kind, value: item.value })),
    steps: task.steps.map((item) => ({ title: item.title, expectedResult: item.expected_result })),
  };
}

/** The value of a datetime input for a moment of the server. */
function toLocalInput(value: string): string {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "";
  }

  const offset = date.getTimezoneOffset() * 60000;

  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

/**
 * The key of the write command. The same key is used for a retry of the same
 * form, so a request that reached the server twice never writes two tasks.
 */
function createIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `task-${Date.now()}-${Math.floor(Math.random() * 1000000)}`;
}
