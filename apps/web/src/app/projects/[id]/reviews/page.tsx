import type { Metadata } from "next";

import { ReviewPanel } from "@/components/review-panel";

export const metadata: Metadata = {
  title: "The reviews of a project | Blindmesh",
  description: "The evidence of the seats of a project and the decision of the owner.",
};

export default async function ProjectReviewsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return <ReviewPanel projectId={id} />;
}
