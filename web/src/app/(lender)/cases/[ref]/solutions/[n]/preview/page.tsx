import type { Metadata } from "next";
import Link from "next/link";
import { Icon, Logo, buttonClasses } from "@/components/ui";
import { apiGet } from "@/lib/api/server";
import { formatDate, formatHijri, formatMoney } from "@/lib/format";

export const metadata: Metadata = { title: "معاينة العرض كما يراه المالك" };

interface Preview {
  lender: string;
  version: number;
  kind: string;
  installment: number;
  termMonths: number;
  firstDue: string;
  firstDueHijri: string;
  lastDue: string;
  waiver: number;
  rescheduled: number;
  offerValidityDays: number;
  validUntilIfSentToday: string;
  message: string | null;
  breach: string;
  templateCode: string | null;
}

/** L17 — What the owner will see (390 phone mock), rendered from the locked version and the published template. */
export default async function PreviewPage({ params }: PageProps<"/cases/[ref]/solutions/[n]/preview">) {
  const { ref, n } = await params;
  const p = await apiGet<Preview>(`/cases/${ref}/solutions/${encodeURIComponent(n)}/preview`);
  return (
    <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,1fr)_400px]">
      <div className="flex flex-col gap-4">
        <h2 className="m-0 text-24 font-bold">معاينة العرض v{p.version} كما يراه المالك</h2>
        <p className="m-0 max-w-[60ch] text-15 text-muted">
          هذه المعاينة تعرض ما سيراه المالك حرفياً على الجوال بعد الاعتماد. المبرر الداخلي وأسماء الفريق لا تظهر له.
        </p>
        <section className="flex flex-col gap-2 rounded-lg border border-line bg-white p-5 text-14">
          <strong>فحص الوضوح</strong>
          <span className="flex items-center gap-1.5 text-ok"><Icon name="check_circle" size={18} />لا كلمات تهديد أو إلزام</span>
          <span className="flex items-center gap-1.5 text-ok"><Icon name="check_circle" size={18} />يذكر المهلة وطريقة السؤال</span>
          <span className="flex items-center gap-1.5 text-ok"><Icon name="check_circle" size={18} />شرح «ماذا لو تأخرت؟» يبدأ بالمساعدة</span>
          {p.templateCode ? <span className="text-12 text-muted">القالب: <bdi dir="ltr">{p.templateCode}</bdi></span> : null}
        </section>
        {p.message ? (
          <section className="flex flex-col gap-2 rounded-lg border border-line bg-white p-5 text-14">
            <strong>نص الإشعار (بوابة المالك)</strong>
            <p className="m-0 leading-7">{p.message}</p>
          </section>
        ) : null}
        <Link href={`/cases/${ref}/solutions/${n}`} className={buttonClasses({ variant: "secondary" })}>رجوع للحل</Link>
      </div>

      <div className="mx-auto w-[390px] max-w-full overflow-hidden rounded-[28px] border border-line bg-warm shadow-3" aria-label="معاينة شاشة المالك">
        <div className="flex h-[60px] items-center gap-2 border-b border-divider bg-white px-3">
          <Logo variant="symbol" width={28} />
          <div className="flex flex-col">
            <strong className="text-16">العرض المقدم لك</strong>
            <span className="text-12 text-muted">صالح حتى <bdi dir="ltr">{formatDate(p.validUntilIfSentToday)}</bdi></span>
          </div>
        </div>
        <div className="flex flex-col gap-4 p-4">
          <div className="flex flex-col gap-1 rounded-lg bg-white p-4 text-center">
            <span className="text-14 text-muted">قسطك الجديد</span>
            <span className="text-32 font-bold"><bdi dir="ltr">{formatMoney(p.installment)}</bdi> <span className="text-16 font-medium">ريال</span></span>
            <span className="text-14 text-muted">يوم {new Date(p.firstDue).getUTCDate()} من كل شهر · {p.termMonths} شهراً</span>
          </div>
          <dl className="m-0 flex flex-col gap-2 rounded-lg bg-white p-4 text-15">
            <div className="flex justify-between"><dt className="text-muted">يبدأ</dt><dd className="m-0"><bdi dir="ltr">{formatDate(p.firstDue)}</bdi></dd></div>
            <div className="flex justify-between"><dt className="text-muted">ينتهي</dt><dd className="m-0"><bdi dir="ltr">{formatDate(p.lastDue)}</bdi></dd></div>
            {p.waiver > 0 ? <div className="flex justify-between"><dt className="text-muted">غرامات التأخير</dt><dd className="m-0">تُلغى (<bdi dir="ltr">{formatMoney(p.waiver)}</bdi> ريال)</dd></div> : null}
            <div className="flex justify-between"><dt className="text-muted">بيتك</dt><dd className="m-0">يبقى ملكك</dd></div>
          </dl>
          <details open className="rounded-lg bg-white p-4 text-15">
            <summary className="cursor-pointer font-semibold">ماذا لو تأخرت عن قسط؟</summary>
            <p className="m-0 mt-2 leading-7">{p.breach}</p>
          </details>
          <span className="text-12 text-muted">{formatHijri(p.firstDue)}</span>
          <div className="flex flex-col gap-2">
            <span className={buttonClasses({ variant: "primary", size: "lg", fullWidth: true })} aria-disabled="true">مراجعة وقبول</span>
            <div className="grid grid-cols-2 gap-2">
              <span className={buttonClasses({ variant: "secondary", size: "lg" })} aria-disabled="true">اقتراح بديل</span>
              <span className={buttonClasses({ variant: "secondary", size: "lg" })} aria-disabled="true">لا يناسبني</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
