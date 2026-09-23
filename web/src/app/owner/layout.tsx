import { OwnerShell } from "@/components/shell/OwnerShell";
import { requireOwnerPortal } from "@/lib/api/guards";
import { getServerDictionary } from "@/lib/i18n/server";

/** Owner (debtor) portal — scoped to one case; session comes from the invitation + ID + SMS flow. */
export default async function OwnerLayout({ children }: LayoutProps<"/owner">) {
  const me = await requireOwnerPortal();
  const { t } = await getServerDictionary();
  const firstName = me.owner?.firstName ?? "";
  const lender = me.owner?.lenderName ?? me.organization?.name ?? "";
  return (
    <OwnerShell
      title={t.owner.greeting(firstName)}
      sub={lender ? t.owner.sub(lender) : undefined}
      userLabel={[firstName, lender].filter(Boolean).join(" · ")}
      unread={me.unreadNotifications}
      numerals={me.user.numerals === "arab" ? "arab" : "latn"}
    >
      {children}
    </OwnerShell>
  );
}
