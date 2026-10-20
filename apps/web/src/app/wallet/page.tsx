import type { Metadata } from "next";

import { WalletView } from "@/components/wallet-view";

export const metadata: Metadata = {
  title: "Wallet | Blindmesh",
  description: "The credits of your account, their expiry and the grant of new contributors.",
};

export default function WalletPage() {
  return <WalletView />;
}
