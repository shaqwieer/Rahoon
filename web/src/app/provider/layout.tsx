import { ProviderShell } from "@/components/shell/PortalShell";
import { toShellUser } from "@/components/shell/types";
import { requireOrgPortal } from "@/lib/api/guards";

/** Service provider / valuer portal (V01–V04): own assignments only, for the assignment's duration. */
export default async function ProviderLayout({ children }: LayoutProps<"/provider">) {
  const me = await requireOrgPortal("serviceprovider", "/provider");
  return <ProviderShell user={toShellUser(me)}>{children}</ProviderShell>;
}
