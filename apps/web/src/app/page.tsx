import { AvailabilityPanel } from "@/components/availability-panel";

export default function HomePage() {
  return (
    <>
      <section className="reading-width">
        <h1 className="text-title font-semibold">Peer testing that settles in credits</h1>
        <p className="mt-4 text-large text-muted-foreground">
          Blindmesh connects independent developers who test each other&apos;s software.
          A task reserves credits before it opens, the tester is paid when the evidence
          is accepted, and every balance stays conserved.
        </p>
      </section>

      <AvailabilityPanel />

      <section className="reading-width" aria-labelledby="scope-heading">
        <h2 id="scope-heading" className="text-title font-semibold">
          What this release covers
        </h2>
        <p className="mt-3 text-muted-foreground">
          This release ships the runtime: the API, the worker, the database and this
          interface. Projects, tasks, capabilities, credits, evidence and moderation
          arrive in the later releases of the roadmap. Nothing on this page counts
          work that has not happened yet.
        </p>
      </section>
    </>
  );
}
