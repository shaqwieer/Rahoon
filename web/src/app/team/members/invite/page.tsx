import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { TeamHeader } from "@/components/market/team/TeamBits";
import { InviteForm } from "@/components/team/admin/InviteForm";
import { apiGet, can, requireMe } from "@/lib/api/server";
import type { RoleRow } from "@/lib/team/admin";

export const metadata: Metadata = { title: "دعوة عضو" };

export default async function InviteMember() {
  const me = await requireMe();
  if (!can(me, "team.manage")) redirect("/access-denied");
  const roles = await apiGet<{ items: RoleRow[] }>("/team/admin/roles");
  // Only roles within the inviter's own grants (same or narrower scope) can be offered; the API checks again.
  const mine = me.scopes ?? {};
  const grantable = roles.items.filter((r) => !r.archived && r.grants.every((g) => mine[g.key] === "all" || (mine[g.key] === "assigned" && g.scope === "assigned")));
  return (
    <div className="max-w-[720px]">
      <TeamHeader title="دعوة عضو إلى الفريق" back={{ href: "/team/members", label: "الفريق" }}
        sub="تحدد أنت البريد والجوال والأدوار؛ المدعو يضع كلمة مروره فقط ولا يستطيع اختيار دور آخر." />
      <InviteForm roles={grantable.map((r) => ({ id: r.id, nameAr: r.nameAr, isSystem: r.isSystem, descriptionAr: r.descriptionAr }))} />
    </div>
  );
}
