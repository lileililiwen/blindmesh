import type { Metadata } from "next";

import { ProfileEditor } from "@/components/profile-editor";

export const metadata: Metadata = {
  title: "Profile settings | Blindmesh",
  description: "Change the public contributor profile of your Blindmesh account.",
};

export default function ProfileSettingsPage() {
  return (
    <ProfileEditor
      title="Profile settings"
      description="Change the fields of your public profile. The version of the profile protects your change against a lost update."
      submitLabel="Save the changes"
      returnPath="/settings/profile"
    />
  );
}
