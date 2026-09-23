import { SettingsLayout } from "@/components/shell/SettingsNav";
import { requireOrgPortal } from "@/lib/api/guards";

/** Institution admin area: LenderShell + SettingsNav (B7 §1.2). */
export default async function OrgSettingsLayout({ children }: LayoutProps<"/settings">) {
  const me = await requireOrgPortal("lender", "/portfolio");
  return <SettingsLayout orgName={me.organization?.name ?? ""}>{children}</SettingsLayout>;
}
