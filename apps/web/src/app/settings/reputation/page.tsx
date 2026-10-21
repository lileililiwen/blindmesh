import type { Metadata } from "next";

import { ReputationHistory } from "@/components/reputation-history";

export const metadata: Metadata = {
  title: "Your reputation | Blindmesh",
  description: "The reputation history of your Blindmesh account.",
};

export default function ReputationSettingsPage() {
  return <ReputationHistory />;
}
