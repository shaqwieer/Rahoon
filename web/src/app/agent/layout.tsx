import { AgentShell } from "@/components/shell/PortalShell";
import { toShellUser } from "@/components/shell/types";
import { requireOrgPortal } from "@/lib/api/guards";

/** Judicial sale agent portal (J05–J07): cases assigned by the competent authority only. */
export default async function AgentLayout({ children }: LayoutProps<"/agent">) {
  const me = await requireOrgPortal("judicialagent", "/agent");
  return <AgentShell user={toShellUser(me)}>{children}</AgentShell>;
}
