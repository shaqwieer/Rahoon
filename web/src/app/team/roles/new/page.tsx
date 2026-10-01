import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { TeamHeader } from "@/components/market/team/TeamBits";
import { RoleEditor } from "@/components/team/admin/RoleEditor";
import { apiGet, can, requireMe } from "@/lib/api/server";
import type { Catalog, Scope } from "@/lib/team/admin";

export const metadata: Metadata = { title: "دور مخصص جديد" };

export default async function NewRole() {
  const me = await requireMe();
  if (!can(me, "roles.manage")) redirect("/access-denied");
  const catalog = await apiGet<Catalog>("/team/admin/catalog");
  return (
    <div className="max-w-[900px]">
      <TeamHeader title="دور مخصص جديد" back={{ href: "/team/roles", label: "الأدوار والصلاحيات" }}
        sub="تمنح فقط ما تملكه أنت، وبنطاق لا يتجاوز نطاقك." />
      <RoleEditor catalog={catalog} grantable={(me.scopes ?? {}) as Record<string, Scope>} />
    </div>
  );
}
