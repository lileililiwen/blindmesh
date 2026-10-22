import type { Metadata } from "next";

import { NotificationList } from "@/components/notification-list";

export const metadata: Metadata = {
  title: "Notifications | Blindmesh",
  description: "The notifications of your Blindmesh account.",
};

export default function NotificationsPage() {
  return <NotificationList />;
}
