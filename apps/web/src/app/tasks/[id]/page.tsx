import type { Metadata } from "next";

import { TaskDetail } from "@/components/task-detail";

export const metadata: Metadata = {
  title: "Task | Blindmesh",
  description: "A task of the Blindmesh marketplace with its budget, its slots and its history.",
};

export default async function TaskPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <TaskDetail taskId={id} />;
}
