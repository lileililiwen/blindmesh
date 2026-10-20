import type { Metadata } from "next";

import { CapabilitySettings } from "@/components/capability-settings";

export const metadata: Metadata = {
  title: "Your capabilities | Blindmesh",
  description: "Declare, edit and withdraw the capabilities of your Blindmesh account.",
};

export default function CapabilitySettingsPage() {
  return <CapabilitySettings />;
}
