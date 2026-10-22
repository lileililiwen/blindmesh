import type { Metadata } from "next";

import { StatusPanel } from "@/components/status-panel";

export const metadata: Metadata = {
  title: "The status of Blindmesh",
  description: "The real state of the product, its dependencies and what is not implemented yet.",
};

export default function StatusPage() {
  return <StatusPanel />;
}
