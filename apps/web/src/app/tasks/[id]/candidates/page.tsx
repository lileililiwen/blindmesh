import type { Metadata } from "next";

import { CandidateList } from "@/components/candidate-list";

export const metadata: Metadata = {
  title: "Candidates | Blindmesh",
  description: "The accounts whose declarations fit the hard conditions of a task of a project.",
};

export default async function TaskCandidatesPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <CandidateList taskId={id} />;
}
