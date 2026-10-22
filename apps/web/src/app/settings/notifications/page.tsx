import type { Metadata } from "next";

import { NotificationPreferences } from "@/components/notification-preferences";

export const metadata: Metadata = {
  title: "Notification preferences | Blindmesh",
  description: "How the notifications of your Blindmesh account reach you.",
};

export default function NotificationSettingsPage() {
  return <NotificationPreferences />;
}
