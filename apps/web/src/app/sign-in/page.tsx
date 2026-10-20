import type { Metadata } from "next";

import { SignInView } from "@/components/sign-in-view";

export const metadata: Metadata = {
  title: "Sign in | Blindmesh",
  description: "Sign in to Blindmesh with your GitHub account.",
};

export default async function SignInPage({
  searchParams,
}: {
  searchParams: Promise<{ return?: string; error?: string }>;
}) {
  const query = await searchParams;

  return <SignInView returnPath={query.return} error={query.error} />;
}
