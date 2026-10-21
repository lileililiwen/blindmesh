import type { Metadata } from "next";

import { DisputeQueue } from "@/components/dispute-queue";

export const metadata: Metadata = {
  title: "The cases of the moderation | Blindmesh",
  description: "The queue of the cases for the moderation staff.",
};

export default function AdminDisputesPage() {
  return <DisputeQueue />;
}
