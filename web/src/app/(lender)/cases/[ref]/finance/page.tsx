import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import type { FinanceData } from "@/lib/api/caseInfo";
import { apiLoad } from "@/lib/api/load";
import { FinanceView } from "./FinanceView";

export const metadata: Metadata = { title: "التمويل والمديونية" };

/** L07 — Financing & debt: read-only figures with source and sync time, 12-month history, correction request (task for Finance). */
export default async function FinancePage({ params }: PageProps<"/cases/[ref]/finance">) {
  const { ref } = await params;
  const res = await apiLoad<FinanceData>(`/cases/${encodeURIComponent(ref)}/finance`);
  if (!res.ok) return <LoadFailure state={res} forbiddenTitle="لا تملك صلاحية عرض بيانات التمويل" />;
  return <FinanceView reference={ref} data={res.data} />;
}
