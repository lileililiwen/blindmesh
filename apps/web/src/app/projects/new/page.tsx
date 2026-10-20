import type { Metadata } from "next";

import { ProjectCreateForm } from "@/components/project-create-form";

export const metadata: Metadata = {
  title: "Create a project | Blindmesh",
  description: "Create a project and describe the software you want tested.",
};

export default function NewProjectPage() {
  return <ProjectCreateForm />;
}
