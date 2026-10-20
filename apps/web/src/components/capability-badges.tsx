import { Badge } from "@/components/ui/badge";

/** The kind of a capability, shown as a badge. */
export function KindBadge({ kind }: { kind: string }) {
  return <Badge tone="neutral">{kind}</Badge>;
}

/** The lifecycle state of a capability, shown as a badge. */
export function CapabilityStatusBadge({ status }: { status: string }) {
  const tone = status === "withdrawn" ? "warning" : "success";

  return <Badge tone={tone}>{status}</Badge>;
}

/** The visibility of a capability, shown as a badge. */
export function CapabilityVisibilityBadge({ visibility }: { visibility: string }) {
  return <Badge tone="neutral">{visibility}</Badge>;
}
