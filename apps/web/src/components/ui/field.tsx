import * as React from "react";

import { cn } from "@/lib/cn";

/**
 * A labelled form field. The label is associated with the control and the
 * error message is announced with it, so a screen reader reads the reason of
 * a refusal together with the field.
 */
export function Field({
  label,
  error,
  hint,
  children,
}: {
  label: string;
  error?: string[];
  hint?: string;
  children: (ids: {
    id: string;
    describedBy?: string;
    invalid: boolean;
  }) => React.ReactNode;
}) {
  const generated = React.useId();
  const id = `field-${generated}`;
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy = [hint ? hintId : null, error && error.length > 0 ? errorId : null]
    .filter(Boolean)
    .join(" ");

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-small font-medium">
        {label}
      </label>

      {children({
        id,
        describedBy: describedBy.length > 0 ? describedBy : undefined,
        invalid: Boolean(error && error.length > 0),
      })}

      {hint ? (
        <p id={hintId} className="text-small text-muted-foreground">
          {hint}
        </p>
      ) : null}

      {error && error.length > 0 ? (
        <p id={errorId} className="text-small text-danger">
          {error.join(" ")}
        </p>
      ) : null}
    </div>
  );
}

export function TextInput({ className, ...props }: React.ComponentProps<"input">) {
  return (
    <input
      className={cn(
        "min-h-[var(--component-control-min-height)] w-full rounded-md border border-border bg-surface px-3 py-2 text-body",
        "aria-[invalid=true]:border-danger",
        className,
      )}
      {...props}
    />
  );
}

export function TextArea({ className, ...props }: React.ComponentProps<"textarea">) {
  return (
    <textarea
      className={cn(
        "min-h-[var(--component-control-min-height)] w-full rounded-md border border-border bg-surface px-3 py-2 text-body",
        "aria-[invalid=true]:border-danger",
        className,
      )}
      {...props}
    />
  );
}

export function Checkbox({ className, ...props }: React.ComponentProps<"input">) {
  return (
    <input
      type="checkbox"
      className={cn(
        "size-5 shrink-0 rounded-sm border border-border bg-surface accent-primary",
        className,
      )}
      {...props}
    />
  );
}

/**
 * A checkbox with its visible label. The label belongs to the checkbox, so a
 * click on the text toggles the box and a screen reader reads the label with
 * the state of the box.
 */
export function CheckboxField({
  id,
  label,
  describedBy,
  className,
  ...props
}: React.ComponentProps<"input"> & { id: string; label: string; describedBy?: string }) {
  return (
    <div className={cn("flex items-start gap-3", className)}>
      <Checkbox id={id} aria-describedby={describedBy} {...props} />
      <label htmlFor={id} className="text-small">
        {label}
      </label>
    </div>
  );
}

/**
 * A group of related controls. The legend names the group and the error of the
 * group is announced with it, so the reason of a refusal is never lost.
 */
export function Fieldset({
  legend,
  error,
  hint,
  children,
}: {
  legend: string;
  error?: string[];
  hint?: string;
  children: (ids: { describedBy?: string; invalid: boolean }) => React.ReactNode;
}) {
  const generated = React.useId();
  const id = `fieldset-${generated}`;
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy = [hint ? hintId : null, error && error.length > 0 ? errorId : null]
    .filter(Boolean)
    .join(" ");

  return (
    <fieldset
      className="flex flex-col gap-1"
      aria-describedby={describedBy.length > 0 ? describedBy : undefined}
    >
      <legend className="text-small font-medium">{legend}</legend>

      {children({
        describedBy: describedBy.length > 0 ? describedBy : undefined,
        invalid: Boolean(error && error.length > 0),
      })}

      {hint ? (
        <p id={hintId} className="text-small text-muted-foreground">
          {hint}
        </p>
      ) : null}

      {error && error.length > 0 ? (
        <p id={errorId} className="text-small text-danger">
          {error.join(" ")}
        </p>
      ) : null}
    </fieldset>
  );
}

export function Select({ className, ...props }: React.ComponentProps<"select">) {
  return (
    <select
      className={cn(
        "min-h-[var(--component-control-min-height)] w-full rounded-md border border-border bg-surface px-3 py-2 text-body",
        "aria-[invalid=true]:border-danger",
        className,
      )}
      {...props}
    />
  );
}
