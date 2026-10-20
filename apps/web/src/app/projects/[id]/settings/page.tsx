import type { Metadata } from "next";

import { ProjectSettings } from "@/components/project-settings";

export const metadata: Metadata = {
  title: "Project settings | Blindmesh",
  description: "The content, the visibility, the lifecycle and the team of a project.",
};

export default async function ProjectSettingsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <ProjectSettings id={id} />;
}
