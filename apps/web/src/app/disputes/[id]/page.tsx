import type { Metadata } from "next";

import { DisputeDetail } from "@/components/dispute-detail";

export const metadata: Metadata = {
  title: "A case of arbitration | Blindmesh",
  description: "The state, the decision and the appeal of one case of arbitration.",
};

export default async function DisputePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <DisputeDetail disputeId={id} />;
}
