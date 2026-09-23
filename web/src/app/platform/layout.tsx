import { PlatformShell } from "@/components/shell/PlatformShell";
import { toShellUser } from "@/components/shell/types";
import { requireOrgPortal } from "@/lib/api/guards";

/** Platform administration (PA01–PA18): aggregated data only; case data needs audited temporary access. */
export default async function PlatformLayout({ children }: LayoutProps<"/platform">) {
  const me = await requireOrgPortal("platform", "/platform");
  return <PlatformShell user={toShellUser(me)}>{children}</PlatformShell>;
}
