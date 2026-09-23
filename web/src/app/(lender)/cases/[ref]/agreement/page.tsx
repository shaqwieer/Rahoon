import type { Metadata } from "next";
import Link from "next/link";
import { EmptyState, buttonClasses } from "@/components/ui";
import { apiGet } from "@/lib/api/server";
import type { AgreementData } from "@/lib/api/lender";
import { AgreementView } from "./AgreementView";

export const metadata: Metadata = { title: "الاتفاق" };

/** L19 — Agreement after the owner's in-platform acceptance: terms, consent record, activation steps. */
export default async function AgreementPage({ params }: PageProps<"/cases/[ref]/agreement">) {
  const { ref } = await params;
  const { agreement } = await apiGet<AgreementData>(`/cases/${encodeURIComponent(ref)}/agreement`);
  if (!agreement) {
    return (
      <EmptyState
        icon="handshake"
        title="لا يوجد اتفاق بعد"
        body="يُنشأ الاتفاق تلقائياً من الإصدار المعتمد عندما يقبل المالك العرض داخل البوابة برمز التحقق."
        action={<Link href={`/cases/${ref}/solutions/negotiation`} className={buttonClasses({ variant: "secondary" })}>سلسلة العروض</Link>}
      />
    );
  }
  return <AgreementView reference={ref} a={agreement} />;
}
