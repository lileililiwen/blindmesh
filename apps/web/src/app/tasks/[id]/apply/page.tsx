import type { Metadata } from "next";

import { ParticipationApply } from "@/components/participation-apply";

export const metadata: Metadata = {
  title: "Apply for a slot | Blindmesh",
  description: "Apply for one independently funded slot of a published task of the Blindmesh marketplace.",
};

export default async function ApplyForSlotPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <ParticipationApply taskId={id} />;
}
