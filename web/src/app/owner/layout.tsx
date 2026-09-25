import { requireOwnerPortal } from "@/lib/api/guards";

/**
 * Owner (debtor) portal — scoped to one case; session comes from the invitation + ID + SMS flow.
 * Each screen renders its own OwnerShell (via OwnerPage) because DebtorTop title/sub/back and the bottom nav
 * differ per screen (B6); the layout only guards the portal.
 */
export default async function OwnerLayout({ children }: LayoutProps<"/owner">) {
  await requireOwnerPortal();
  return children;
}
