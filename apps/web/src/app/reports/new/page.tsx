import type { Metadata } from "next";

import { ReportForm } from "@/components/report-form";

export const metadata: Metadata = {
  title: "Report abusive content | Blindmesh",
  description: "A report of abusive content and the receipt of it.",
};

export default function NewReportPage() {
  return <ReportForm />;
}
