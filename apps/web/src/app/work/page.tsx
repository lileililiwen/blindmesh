import type { Metadata } from "next";

import { ParticipationList } from "@/components/participation-list";

export const metadata: Metadata = {
  title: "Your work | Blindmesh",
  description: "The seats of the tasks of the Blindmesh marketplace that you applied for.",
};

export default function WorkPage() {
  return <ParticipationList />;
}
