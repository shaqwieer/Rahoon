import type { Metadata } from "next";
import { DirectoryForm } from "@/components/market/team/DirectoryForm";
import { TeamHeader } from "@/components/market/team/TeamBits";
import { can, requireMe } from "@/lib/api/server";
import { redirect } from "next/navigation";

export const metadata: Metadata = { title: "إضافة جهة" };

export default async function NewOrganization() {
  const me = await requireMe();
  if (!can(me, "directory.manage")) redirect("/access-denied");
  return (
    <div className="max-w-[720px]">
      <TeamHeader title="إضافة جهة إلى الدليل" back={{ href: "/team/organizations", label: "دليل الجهات" }}
        sub="أضف جهة حقيقية فقط، واذكر المصدر الذي وجدتها فيه." />
      <DirectoryForm />
    </div>
  );
}
