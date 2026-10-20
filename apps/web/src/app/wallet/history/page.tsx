import type { Metadata } from "next";

import { WalletHistory } from "@/components/wallet-history";

export const metadata: Metadata = {
  title: "Wallet history | Blindmesh",
  description: "Every movement of the credits of your account.",
};

export default function WalletHistoryPage() {
  return <WalletHistory />;
}
