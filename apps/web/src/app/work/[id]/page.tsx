import type { Metadata } from "next";

import { ParticipationDetail } from "@/components/participation-detail";

export const metadata: Metadata = {
  title: "Your seat | Blindmesh",
  description: "The instructions, the deadline and the next action of your seat of a task.",
};

export default async function WorkDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <ParticipationDetail participationId={id} />;
}
