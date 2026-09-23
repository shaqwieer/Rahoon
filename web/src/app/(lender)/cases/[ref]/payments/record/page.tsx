import type { Metadata } from "next";
import Link from "next/link";
import { todayRiyadh } from "@/components/case/BidiText";
import { EmptyState, Icon, buttonClasses } from "@/components/ui";
import { apiGet } from "@/lib/api/server";
import { canRecordOn, hasSchedule, recordableInstallments, type PaymentsData } from "@/lib/api/lender";
import { RecordPaymentPage } from "./RecordPaymentPage";

export const metadata: Metadata = { title: "تسجيل دفعة" };

/** L20 mobile: «تسجيل دفعة» as a full page (the desktop uses the drawer on /payments). */
export default async function RecordPage({ params }: PageProps<"/cases/[ref]/payments/record">) {
  const { ref } = await params;
  const d = await apiGet<PaymentsData>(`/cases/${encodeURIComponent(ref)}/payments`);
  const back = `/cases/${ref}/payments`;
  const header = (
    <div className="mb-4 flex items-center gap-2">
      <Link href={back} aria-label="رجوع إلى المدفوعات" className={buttonClasses({ variant: "text", className: "px-2" })}>
        <Icon name="arrow_forward" size={22} mirror />
      </Link>
      <h2 className="m-0 text-18 font-bold">تسجيل دفعة يدوياً</h2>
    </div>
  );
  if (!hasSchedule(d) || !d.canRecord || !canRecordOn(d.agreement.status) || recordableInstallments(d.installments).length === 0) {
    return (
      <>
        {header}
        <EmptyState
          icon="payments"
          headingLevel={3}
          title="لا يمكن تسجيل دفعة الآن"
          body={!hasSchedule(d) ? "لا يوجد جدول سداد بعد؛ يُنشأ عند تفعيل الاتفاق." : !d.canRecord ? "تسجيل الدفعات من صلاحيات المالية." : "التسجيل متاح أثناء سريان الاتفاق للأقساط غير المسجلة فقط."}
          action={<Link href={back} className={buttonClasses({ variant: "secondary" })}>العودة إلى المدفوعات</Link>}
        />
      </>
    );
  }
  return (
    <>
      {header}
      <div className="mx-auto w-full max-w-[560px] overflow-hidden rounded-lg border border-line bg-white">
        <RecordPaymentPage reference={ref} installments={recordableInstallments(d.installments)} today={todayRiyadh()} />
      </div>
    </>
  );
}
