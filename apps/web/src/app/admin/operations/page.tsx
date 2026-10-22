import type { Metadata } from "next";

import { OperationsPanel } from "@/components/operations-panel";

export const metadata: Metadata = {
  title: "The operations of the pilot | Blindmesh",
  description: "The measured gate of the pilot, the backups, the restore rehearsals and the deletion requests.",
};

export default function AdminOperationsPage() {
  return <OperationsPanel />;
}
