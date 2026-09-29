import { LenderShell } from "@/components/shell/LenderShell";
import { toShellUser } from "@/components/shell/types";
import { requireOrgPortal } from "@/lib/api/guards";
import { SmsConfirmationProvider } from "@/lib/sms-confirmation";

/** Lender workspace (portfolio, cases, tasks, approvals, complaints, reports, settings …). Always rendered per request. */
export default async function LenderLayout({ children }: LayoutProps<"/">) {
  const me = await requireOrgPortal("lender", "/portfolio");
  return (
    <SmsConfirmationProvider enabled={me.smsConfirmation !== false}>
      <LenderShell user={toShellUser(me)}>{children}</LenderShell>
    </SmsConfirmationProvider>
  );
}
