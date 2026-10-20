import { Badge } from "@/components/ui/badge";

/** The lifecycle state of a task, shown as a badge. */
export function TaskStateBadge({ status }: { status: string }) {
  const tone = status === "open" ? "success" : status === "cancelled" ? "danger" : "neutral";

  return <Badge tone={tone}>{status}</Badge>;
}

/** The visibility of a task, shown as a badge. */
export function TaskVisibilityBadge({ visibility }: { visibility: string }) {
  return <Badge tone="neutral">{visibility}</Badge>;
}

/** The modes of a task, shown as badges. */
export function TaskModeBadges({ modes }: { modes: string[] }) {
  return (
    <>
      {modes.map((mode) => (
        <Badge key={mode} tone="neutral">
          {mode}
        </Badge>
      ))}
    </>
  );
}
