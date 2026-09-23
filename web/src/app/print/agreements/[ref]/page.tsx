import type { Metadata } from "next";
import Link from "next/link";
import { AGREEMENT_STATUS, SigningNote } from "@/components/case/AgreementBits";
import { BidiText } from "@/components/case/BidiText";
import { Logo } from "@/components/ui/Logo";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { apiGet } from "@/lib/api/server";
import type { AgreementData } from "@/lib/api/lender";
import { PrintButton } from "./PrintButton";

export async function generateMetadata({ params }: PageProps<"/print/agreements/[ref]">): Promise<Metadata> {
  const { ref } = await params;
  return { title: `النص الكامل للاتفاق · ${ref}`, robots: { index: false, follow: false } };
}

/**
 * L19 «النص الكامل»: printable agreement document outside the app shell (print CSS hides the toolbar).
 * Built only from the API's agreement payload — no clause text is invented here. Auth: apiGet sends 401 to
 * login and 403 to access-denied, the API authorizes the read.
 */
export default async function AgreementPrintPage({ params }: PageProps<"/print/agreements/[ref]">) {
  const { ref } = await params;
  const { agreement: a } = await apiGet<AgreementData>(`/cases/${encodeURIComponent(ref)}/agreement`);
  return (
    <div className="min-h-dvh bg-warm print:bg-white">
      <div className="flex flex-wrap items-center gap-3 border-b border-line bg-white px-4 py-3 md:px-10 print:hidden">
        <Link href={`/cases/${encodeURIComponent(ref)}/agreement`} className={buttonClasses({ variant: "text" })}>
          العودة للاتفاق
        </Link>
        <span className="ms-auto text-13 text-muted">اختر «حفظ بصيغة PDF» من نافذة الطباعة للحصول على ملف.</span>
        {a ? <PrintButton /> : null}
      </div>
      <main id="main" className="mx-auto my-6 w-full max-w-[820px] rounded-lg border border-line bg-white p-6 md:my-10 md:p-12 print:m-0 print:max-w-none print:rounded-none print:border-0 print:p-0">
        {!a ? (
          <>
            <h1 className="m-0 text-24 font-bold">لا يوجد اتفاق بعد</h1>
            <p className="text-15 text-muted">يُنشأ الاتفاق عندما يقبل المالك العرض داخل البوابة.</p>
          </>
        ) : (
          <article className="flex flex-col gap-6 text-15 leading-7">
            <header className="flex flex-col gap-3 border-b border-line pb-5">
              <Logo variant="horizontal" width={150} alt="رهون" />
              <h1 className="m-0 text-26 leading-10 font-bold">اتفاق إعادة الجدولة</h1>
              <p className="m-0 flex flex-wrap gap-x-4 gap-y-1 text-14 text-muted">
                <span>
                  رقم الاتفاق <bdi dir="ltr" className="font-mono font-semibold text-ink">{a.number}</bdi>
                </span>
                <span>
                  الإصدار <bdi dir="ltr" className="font-mono font-semibold text-ink">{a.versionLabel}</bdi>
                </span>
                <span>
                  الحالة المرجعية <bdi dir="ltr" className="font-mono font-semibold text-ink">{ref}</bdi>
                </span>
                <span>الوضع: {AGREEMENT_STATUS[a.status]?.label ?? a.status}</span>
              </p>
            </header>

            <section aria-labelledby="terms-h" className="flex flex-col gap-2">
              <h2 id="terms-h" className="m-0 text-18 font-bold">الشروط</h2>
              <dl className="m-0 flex flex-col">
                {a.terms.map((t, i) => (
                  <div key={t.k} className="grid grid-cols-[32px_180px_minmax(0,1fr)] gap-3 border-t border-divider py-2.5 first:border-t-0 print:break-inside-avoid">
                    <span aria-hidden="true" className="font-mono text-13 text-muted">
                      {i + 1}.
                    </span>
                    <dt className="font-semibold">{t.k}</dt>
                    <dd className="m-0">
                      <BidiText text={t.v} />
                    </dd>
                  </div>
                ))}
              </dl>
            </section>

            <section aria-labelledby="consent-h" className="flex flex-col gap-1.5 print:break-inside-avoid">
              <h2 id="consent-h" className="m-0 text-18 font-bold">سجل موافقة المالك</h2>
              {a.consent ? (
                <>
                  <BidiText text={a.consent.title} />
                  <BidiText text={a.consent.meta} className="text-14 text-muted" />
                  <span className="text-14">{a.consent.acks}</span>
                  {a.consent.textHash ? (
                    <span className="text-12 text-muted">
                      بصمة نص الشروط المقبول: <bdi dir="ltr" className="font-mono break-all">{a.consent.textHash}</bdi>
                    </span>
                  ) : null}
                </>
              ) : (
                <span className="text-muted">لا يوجد سجل موافقة موثّق بعد.</span>
              )}
            </section>

            <section aria-labelledby="sign-h" className="flex flex-col gap-1.5 rounded-md border border-dashed border-line-strong p-4 text-14 print:break-inside-avoid">
              <h2 id="sign-h" className="sr-only">التوقيع</h2>
              <SigningNote note={a.signing.note} />
            </section>

            <footer className="border-t border-line pt-4 text-12 text-muted">
              وثيقة مولّدة من سجل الاتفاق في رهون. السداد خارج المنصة بتحويل بنكي إلى حساب المصرف.
            </footer>
          </article>
        )}
      </main>
    </div>
  );
}
