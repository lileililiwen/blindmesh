import type { Metadata } from "next";

import { PublicProfileView } from "@/components/public-profile-view";

export const metadata: Metadata = {
  title: "Contributor profile | Blindmesh",
  description: "The public contributor profile of a Blindmesh member.",
};

export default async function PeoplePage({
  params,
}: {
  params: Promise<{ username: string }>;
}) {
  const { username } = await params;

  return <PublicProfileView username={username} />;
}
