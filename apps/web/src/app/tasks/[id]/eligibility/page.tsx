import type { Metadata } from "next";

import { TaskEligibility } from "@/components/capability-eligibility";

export const metadata: Metadata = {
  title: "Task eligibility | Blindmesh",
  description: "Check whether your capabilities meet the conditions of a task of the Blindmesh marketplace.",
};

export default async function TaskEligibilityPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <TaskEligibility taskId={id} />;
}
