import type { Metadata } from "next";

import { ReportQueue } from "@/components/report-queue";

export const metadata: Metadata = {
  title: "The reports of the moderation | Blindmesh",
  description: "The queue of the reports for the moderation staff.",
};

export default function AdminReportsPage() {
  return <ReportQueue />;
}
