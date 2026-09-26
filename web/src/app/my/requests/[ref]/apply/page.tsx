import { redirect } from "next/navigation";
import { getIndividualContext, individualMetadata, myGet } from "@/components/individual/server";
import { apiGet } from "@/lib/api/server";
import type { Institution, MyRequestDetail } from "@/lib/api/requests";
import { RequestWizard } from "./RequestWizard";

export const generateMetadata = () => individualMetadata((c) => c.wizard.title);

/** OA01–OA05 (B13 + design request D-2): one draft, autosaved per step; locked once submitted (add-only). */
export default async function ApplyPage({ params, searchParams }: PageProps<"/my/requests/[ref]/apply">) {
  const { ref } = await params;
  const sp = await searchParams;
  await getIndividualContext();
  const [detail, institutions] = await Promise.all([myGet<MyRequestDetail>(`/requests/${encodeURIComponent(ref)}`), apiGet<Institution[]>("/institutions")]);
  if (!detail.canEdit) redirect(`/my/requests/${encodeURIComponent(detail.reference)}`);
  const raw = Number(Array.isArray(sp.step) ? sp.step[0] : sp.step);
  const step = Number.isInteger(raw) && raw >= 1 && raw <= 5 ? raw : 1;
  return <RequestWizard key={detail.reference} initial={detail} institutions={institutions} step={step} />;
}
