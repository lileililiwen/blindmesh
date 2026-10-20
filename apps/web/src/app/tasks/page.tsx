import type { Metadata } from "next";

import { TaskList } from "@/components/task-list";

export const metadata: Metadata = {
  title: "Tasks | Blindmesh",
  description: "The tasks of the Blindmesh marketplace and the tasks you own.",
};

export default function TasksPage() {
  return <TaskList />;
}
