import type { Metadata } from "next";

import { CapabilityList } from "@/components/capability-list";

export const metadata: Metadata = {
  title: "Capabilities | Blindmesh",
  description: "The capabilities of the Blindmesh marketplace that you may see.",
};

export default function CapabilitiesPage() {
  return <CapabilityList />;
}
