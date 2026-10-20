import type { Metadata } from "next";

import { ProjectList } from "@/components/project-list";

export const metadata: Metadata = {
  title: "Projects | Blindmesh",
  description: "The projects of the Blindmesh marketplace and your own projects.",
};

export default function ProjectsPage() {
  return <ProjectList />;
}
