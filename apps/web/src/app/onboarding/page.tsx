import type { Metadata } from "next";

import { ProfileEditor } from "@/components/profile-editor";

export const metadata: Metadata = {
  title: "Set up your profile | Blindmesh",
  description: "Set up the public contributor profile of your Blindmesh account.",
};

export default function OnboardingPage() {
  return (
    <ProfileEditor
      title="Set up your profile"
      description="Your profile is the card other developers see. Choose a name, say what you work on and save it once."
      submitLabel="Save the profile"
      returnPath="/onboarding"
    />
  );
}
