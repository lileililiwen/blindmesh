import type { Metadata } from "next";

import { FindingDetail } from "@/components/finding-detail";

export const metadata: Metadata = {
  title: "A finding | Blindmesh",
  description: "One finding of the evidence of a seat and the decision of the owner.",
};

export default async function FindingPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <FindingDetail findingId={id} />;
}
