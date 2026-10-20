import type { Metadata } from "next";

import { TaskList } from "@/components/task-list";

export const metadata: Metadata = {
  title: "The tasks of a project | Blindmesh",
  description: "The tasks of a project of the Blindmesh marketplace.",
};

export default async function ProjectTasksPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <TaskList projectId={id} />;
}
