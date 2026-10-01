import { TeamShell } from "@/components/team/TeamShell";
import { toShellUser } from "@/components/shell/types";
import { requireOrgPortal } from "@/lib/api/guards";
import { SmsConfirmationProvider } from "@/lib/sms-confirmation";

/** «فريق رهون» workspace: sale and buyer requests, opportunities, interests. Operator members only; the API authorizes every call. */
export default async function TeamLayout({ children }: LayoutProps<"/team">) {
  const me = await requireOrgPortal("operator", "/team");
  return (
    <SmsConfirmationProvider enabled={me.smsConfirmation !== false}>
      <TeamShell user={toShellUser(me)}>{children}</TeamShell>
    </SmsConfirmationProvider>
  );
}
