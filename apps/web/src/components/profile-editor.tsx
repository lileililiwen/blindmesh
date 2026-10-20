"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { Checkbox, Field, TextArea, TextInput } from "@/components/ui/field";
import { loadProfile, saveProfile, type ProfileState, type SaveProfileState } from "@/lib/identity";
import type { ProfileResponse } from "@blindmesh/api-client";

/** The order of the fields. The first field with an error takes the focus. */
const fieldOrder = ["username", "display_name", "bio", "timezone", "skills", "languages"] as const;

type Draft = {
  username: string;
  displayName: string;
  bio: string;
  timezone: string;
  skills: string;
  languages: string;
  isPublic: boolean;
};

function toDraft(profile: ProfileResponse): Draft {
  return {
    username: profile.username,
    displayName: profile.display_name,
    bio: profile.bio,
    timezone: profile.timezone,
    skills: profile.skills.join(", "),
    languages: profile.languages.join(", "),
    isPublic: profile.is_public,
  };
}

function toTags(value: string): string[] {
  return value
    .split(",")
    .map((tag) => tag.trim())
    .filter((tag) => tag.length > 0);
}

/**
 * The profile of the signed in contributor. The draft of the visitor is kept
 * when the server refuses the change, so a recoverable error never costs the
 * work of the contributor.
 */
export function ProfileEditor({
  title,
  description,
  submitLabel,
  returnPath,
}: {
  title: string;
  description: string;
  submitLabel: string;
  returnPath: string;
}) {
  const { state: session, csrfToken } = useSession();
  const [state, setState] = useState<ProfileState>({ kind: "loading" });
  const [profile, setProfile] = useState<ProfileResponse | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [save, setSave] = useState<SaveProfileState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const fieldRefs = useRef<Record<string, HTMLElement | null>>({});
  const triggerRef = useRef<HTMLButtonElement>(null);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadProfile();
    setState(next);

    if (next.kind === "ready") {
      setProfile(next.profile);
      setDraft((current) => current ?? toDraft(next.profile));
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      if (session.kind !== "authenticated") {
        setState({ kind: "unauthorized" });
        return;
      }

      const next = await loadProfile();

      if (!cancelled) {
        setState(next);

        if (next.kind === "ready") {
          setProfile(next.profile);
          setDraft((current) => current ?? toDraft(next.profile));
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind]);

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="profile-heading" className="reading-width">
        <h1 id="profile-heading" className="text-title font-semibold">
          {title}
        </h1>
        <p className="mt-3 text-muted-foreground">
          Sign in to open your profile. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent(returnPath)}`}>Sign in to continue</a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="profile-heading" aria-busy="true" className="reading-width">
        <h1 id="profile-heading" className="text-title font-semibold">
          {title}
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading your profile.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-10 w-full rounded bg-muted" />
          <div className="h-10 w-full rounded bg-muted" />
          <div className="h-24 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable" || state.kind === "forbidden") {
    const message =
      state.kind === "error" || state.kind === "unavailable" || state.kind === "forbidden"
        ? state.message
        : "";

    return (
      <section aria-labelledby="profile-heading" className="reading-width">
        <h1 id="profile-heading" className="text-title font-semibold">
          {title}
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {message}
        </p>
        <p className="mt-4">
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "not-found" || !draft || !profile) {
    return (
      <section aria-labelledby="profile-heading" className="reading-width">
        <h1 id="profile-heading" className="text-title font-semibold">
          {title}
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          There is no profile for this account yet.
        </p>
      </section>
    );
  }

  const fieldErrors = save.kind === "invalid" ? save.fieldErrors : {};
  const pending = save.kind === "saving";

  function focusFirstError(errors: Record<string, string[]>) {
    const field = fieldOrder.find((name) => (errors[name] ?? []).length > 0) ?? Object.keys(errors)[0];

    if (field) {
      fieldRefs.current[field]?.focus();
    }
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!draft || !profile || pending) {
      return;
    }

    setSave({ kind: "saving" });
    setAnnouncement("");

    const result = await saveProfile(
      {
        expected_version: Number(profile.version),
        username: draft.username,
        display_name: draft.displayName,
        bio: draft.bio,
        timezone: draft.timezone,
        skills: toTags(draft.skills),
        languages: toTags(draft.languages),
        is_public: draft.isPublic,
      },
      csrfToken,
    );

    setSave(result);

    if (result.kind === "saved") {
      setProfile({ ...profile, version: result.version });
      setAnnouncement("Your profile is saved.");
      triggerRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields need another value before the profile can be saved.");
      focusFirstError(result.fieldErrors);
      return;
    }

    if (result.kind === "conflict") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to save the profile.");
      return;
    }

    if (result.kind === "error") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The profile was not saved.");
  }

  return (
    <section aria-labelledby="profile-heading" className="reading-width">
      <h1 id="profile-heading" className="text-title font-semibold">
        {title}
      </h1>
      <p className="mt-2 text-muted-foreground">{description}</p>

      <form className="mt-6 flex flex-col gap-5" onSubmit={submit} noValidate>
        <Field label="Username" error={fieldErrors.username} hint="3 to 30 letters, digits and hyphens.">
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="username"
              value={draft.username}
              autoComplete="username"
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.username = element;
              }}
              onChange={(event) => setDraft({ ...draft, username: event.target.value })}
            />
          )}
        </Field>

        <Field label="Display name" error={fieldErrors.display_name}>
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="display_name"
              value={draft.displayName}
              autoComplete="name"
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.display_name = element;
              }}
              onChange={(event) => setDraft({ ...draft, displayName: event.target.value })}
            />
          )}
        </Field>

        <Field label="Bio" error={fieldErrors.bio} hint="At most 1000 characters.">
          {({ id, describedBy, invalid }) => (
            <TextArea
              id={id}
              name="bio"
              rows={4}
              value={draft.bio}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.bio = element;
              }}
              onChange={(event) => setDraft({ ...draft, bio: event.target.value })}
            />
          )}
        </Field>

        <Field
          label="Time zone"
          error={fieldErrors.timezone}
          hint="An IANA name, for example Europe/Berlin."
        >
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="timezone"
              value={draft.timezone}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.timezone = element;
              }}
              onChange={(event) => setDraft({ ...draft, timezone: event.target.value })}
            />
          )}
        </Field>

        <Field label="Skills" error={fieldErrors.skills} hint="At most 20 tags, separated by commas.">
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="skills"
              value={draft.skills}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.skills = element;
              }}
              onChange={(event) => setDraft({ ...draft, skills: event.target.value })}
            />
          )}
        </Field>

        <Field label="Languages" error={fieldErrors.languages} hint="At most 20 tags, separated by commas.">
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="languages"
              value={draft.languages}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.languages = element;
              }}
              onChange={(event) => setDraft({ ...draft, languages: event.target.value })}
            />
          )}
        </Field>

        <div className="flex items-start gap-3">
          <Checkbox
            id="profile-is-public"
            name="is_public"
            checked={draft.isPublic}
            onChange={(event) => setDraft({ ...draft, isPublic: event.target.checked })}
          />
          <label htmlFor="profile-is-public" className="text-small">
            Show my profile on the public page. A private profile is not readable by a visitor.
          </label>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <Button ref={triggerRef} type="submit" disabled={pending} aria-busy={pending}>
            {pending ? "Saving the profile" : submitLabel}
          </Button>

          {save.kind === "conflict" ? (
            <Button type="button" variant="secondary" onClick={() => void load()}>
              Load the current profile
            </Button>
          ) : null}
        </div>

        <p role="status" aria-live="polite" className="sr-only">
          {announcement}
        </p>

        {save.kind === "conflict" ? (
          <p className="text-small text-warning">{save.message}</p>
        ) : null}
      </form>
    </section>
  );
}
