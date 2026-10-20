import { Badge } from "@/components/ui/badge";

/** The lifecycle state of a project, shown as a badge. */
export function StatusBadge({ status }: { status: string }) {
  const tone = status === "active" ? "success" : status === "archived" ? "neutral" : status === "paused" ? "warning" : "neutral";

  return <Badge tone={tone}>{status}</Badge>;
}

/** The visibility of a project, shown as a badge. */
export function VisibilityBadge({ visibility }: { visibility: string }) {
  return <Badge tone="neutral">{visibility}</Badge>;
}
