import type { Metadata } from "next";

import { ReputationHistory } from "@/components/reputation-history";

export const metadata: Metadata = {
  title: "Contributor reputation | Blindmesh",
  description: "The public reputation history of a Blindmesh contributor.",
};

export default async function PeopleHistoryPage({
  params,
}: {
  params: Promise<{ username: string }>;
}) {
  const { username } = await params;

  return <ReputationHistory username={username} />;
}
