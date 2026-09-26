import { TeamShell } from "@/components/team/TeamShell";
import { toShellUser } from "@/components/shell/types";
import { requireOrgPortal } from "@/lib/api/guards";

/** «فريق رهون» workspace (ADR 0001, design request D-4): operator members only; the API authorizes every call. */
export default async function TeamLayout({ children }: LayoutProps<"/team">) {
  const me = await requireOrgPortal("operator", "/team");
  return <TeamShell user={toShellUser(me)}>{children}</TeamShell>;
}
