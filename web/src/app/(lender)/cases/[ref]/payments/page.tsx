import type { Metadata } from "next";
import Link from "next/link";
import { todayRiyadh } from "@/components/case/BidiText";
import { EmptyState, buttonClasses } from "@/components/ui";
import { apiGet } from "@/lib/api/server";
import { hasSchedule, type PaymentsData } from "@/lib/api/lender";
import { PaymentsView } from "./PaymentsView";

export const metadata: Metadata = { title: "المدفوعات" };

/** L20 — Payment schedule and manual recording (maker-checker). Payments happen outside the platform (A-04). */
export default async function PaymentsPage({ params }: PageProps<"/cases/[ref]/payments">) {
  const { ref } = await params;
  const d = await apiGet<PaymentsData>(`/cases/${encodeURIComponent(ref)}/payments`);
  if (!hasSchedule(d)) {
    return (
      <EmptyState
        icon="payments"
        title="لا يوجد جدول سداد بعد"
        body="يظهر جدول السداد بعد تفعيل الاتفاق. السداد يتم خارج المنصة بتحويل بنكي، ويُسجَّل هنا يدوياً ثم يطابقه موظف مالية آخر."
        action={<Link href={`/cases/${ref}/agreement`} className={buttonClasses({ variant: "secondary" })}>الاتفاق وخطوات التفعيل</Link>}
      />
    );
  }
  return <PaymentsView reference={ref} d={d} today={todayRiyadh()} />;
}
