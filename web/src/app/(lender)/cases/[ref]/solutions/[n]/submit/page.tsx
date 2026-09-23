import type { Metadata } from "next";
import { apiGet } from "@/lib/api/server";
import type { SubmissionData } from "@/lib/api/lender";
import { SubmitReview } from "./SubmitReview";

export const metadata: Metadata = { title: "مراجعة قبل الإرسال للموافقة" };

/** L15 — High-impact review before sending a locked version to internal approval. */
export default async function SubmitPage({ params }: PageProps<"/cases/[ref]/solutions/[n]/submit">) {
  const { ref, n } = await params;
  const data = await apiGet<SubmissionData>(`/cases/${ref}/solutions/${encodeURIComponent(n)}/submission`);
  return <SubmitReview data={data} />;
}
