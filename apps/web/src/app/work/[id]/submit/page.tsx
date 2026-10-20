import type { Metadata } from "next";

import { EvidenceForm } from "@/components/evidence-form";

export const metadata: Metadata = {
  title: "The evidence of your seat | Blindmesh",
  description: "The protected evidence of a seat that you run, with its private files.",
};

export default async function WorkSubmitPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <EvidenceForm participationId={id} />;
}
