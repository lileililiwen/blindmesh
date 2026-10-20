"use client";

import { useEffect, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { loadPublicProfile, type PublicProfileState } from "@/lib/identity";

/**
 * The public page of a contributor. It shows the fields a visitor may see and
 * nothing else. A profile that does not exist and a profile that is private
 * answer with the same page.
 */
export function PublicProfileView({ username }: { username: string }) {
  const [state, setState] = useState<PublicProfileState>({ kind: "loading" });

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadPublicProfile(username);

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [username]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="profile-heading" aria-busy="true" className="reading-width">
        <h1 id="profile-heading" className="text-title font-semibold">
          Profile
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the profile.</p>
        <div className="mt-4 flex flex-col gap-2" aria-hidden="true">
          <div className="h-6 w-48 rounded bg-muted" />
          <div className="h-20 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="profile-heading" className="reading-width">
        <h1 id="profile-heading" className="text-title font-semibold">
          Profile
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This profile does not exist. It may have been removed or it may never have been public.
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="profile-heading" className="reading-width">
        <h1 id="profile-heading" className="text-title font-semibold">
          Profile
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.message}
        </p>
      </section>
    );
  }

  const profile = state.profile;

  return (
    <section aria-labelledby="profile-heading" className="reading-width">
      <div className="flex flex-wrap items-center gap-3">
        <h1 id="profile-heading" className="text-title font-semibold">
          {profile.display_name}
        </h1>
        <Badge tone="neutral">@{profile.username}</Badge>
      </div>

      {profile.bio ? <p className="mt-3 text-large">{profile.bio}</p> : null}

      <dl className="mt-6 flex flex-col gap-3">
        <div className="flex flex-col gap-1 sm:flex-row sm:items-baseline sm:gap-3">
          <dt className="text-small font-medium sm:w-40">Time zone</dt>
          <dd className="text-small text-muted-foreground">{profile.timezone}</dd>
        </div>

        <div className="flex flex-col gap-1 sm:flex-row sm:items-baseline sm:gap-3">
          <dt className="text-small font-medium sm:w-40">Skills</dt>
          <dd className="text-small text-muted-foreground">
            {profile.skills.length > 0 ? profile.skills.join(", ") : "No skills are listed yet."}
          </dd>
        </div>

        <div className="flex flex-col gap-1 sm:flex-row sm:items-baseline sm:gap-3">
          <dt className="text-small font-medium sm:w-40">Languages</dt>
          <dd className="text-small text-muted-foreground">
            {profile.languages.length > 0 ? profile.languages.join(", ") : "No languages are listed yet."}
          </dd>
        </div>

        <div className="flex flex-col gap-1 sm:flex-row sm:items-baseline sm:gap-3">
          <dt className="text-small font-medium sm:w-40">Member since</dt>
          <dd className="text-small text-muted-foreground">
            {new Date(profile.joined_at).toLocaleDateString("en-GB", {
              year: "numeric",
              month: "long",
              day: "numeric",
              timeZone: "UTC",
            })}
          </dd>
        </div>
      </dl>
    </section>
  );
}
