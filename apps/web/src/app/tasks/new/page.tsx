import type { Metadata } from "next";

import { TaskCreateForm } from "@/components/task-create-form";

export const metadata: Metadata = {
  title: "Write a task | Blindmesh",
  description: "Write a task of the Blindmesh marketplace and reserve its budget.",
};

export default function NewTaskPage() {
  return <TaskCreateForm />;
}
