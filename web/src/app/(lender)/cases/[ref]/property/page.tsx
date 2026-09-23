import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import type { PropertyData } from "@/lib/api/caseInfo";
import { apiLoad } from "@/lib/api/load";
import { getMe } from "@/lib/api/server";
import { PropertyView } from "./PropertyView";

export const metadata: Metadata = { title: "العقار والرهن" };

/** L08 + L09 — Property (case team) and mortgage & security (legal review). */
export default async function PropertyPage({ params }: PageProps<"/cases/[ref]/property">) {
  const { ref } = await params;
  const [res, me] = await Promise.all([apiLoad<PropertyData>(`/cases/${encodeURIComponent(ref)}/property`), getMe()]);
  if (!res.ok) return <LoadFailure state={res} forbiddenTitle="لا تملك صلاحية عرض بيانات العقار والرهن" />;
  const perms = me.authenticated ? me.permissions : [];
  return (
    <PropertyView
      reference={ref}
      data={res.data}
      // UI hint only — the legal-review endpoint requires agreement.activate (legal role) and remains the authority.
      isLegal={perms.includes("agreement.activate")}
      canViewPhoto={perms.includes("document.download")}
    />
  );
}
