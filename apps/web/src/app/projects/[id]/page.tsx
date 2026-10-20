import type { Metadata } from "next";

import { ProjectDetail } from "@/components/project-detail";

export const metadata: Metadata = {
  title: "Project | Blindmesh",
  description: "A project of the Blindmesh marketplace with its versions and its history.",
};

export default async function ProjectPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <ProjectDetail id={id} />;
}
