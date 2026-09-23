"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { AGREEMENT_STATUS, SigningNote } from "@/components/case/AgreementBits";
import { BidiText } from "@/components/case/BidiText";
import { ConfirmDialog } from "@/components/case/ConfirmDialog";
import { ReasonDialog } from "@/components/case/ReasonDialog";
import { Button, Icon, IntegrationStateTag, Tag, buttonClasses, useToast } from "@/components/ui";
import type { Tone } from "@/components/ui";
import { apiSend } from "@/lib/api/client";
import { toIntegrationState, type AgreementDto } from "@/lib/api/lender";

type Pending = "legal" | "schedule" | "activate" | null;

export function AgreementView({ reference, a }: { reference: string; a: AgreementDto }) {
  const router = useRouter();
  const toast = useToast();
  const [dialog, setDialog] = useState<Pending>(null);
  const base = `/cases/${reference}`;
  const status = AGREEMENT_STATUS[a.status] ?? { label: a.status, tone: "neutral" as Tone };
  const p = a.permissions;
  const pendingSteps = a.steps.filter((s) => s.key !== "core_system" && !s.done);

  return (
    <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_380px]">
      <section aria-labelledby="agr-h" className="flex min-w-0 flex-col gap-4 rounded-lg border border-line bg-white p-5 md:p-6">
        <div className="flex flex-wrap items-center gap-3">
          <h2 id="agr-h" className="m-0 text-20 font-bold">اتفاق إعادة الجدولة</h2>
          <bdi dir="ltr" className="rounded-xs bg-subtle px-2 py-0.5 font-mono text-13 font-semibold">
            {a.number} · {a.versionLabel}
          </bdi>
          <Tag tone={status.tone}>{status.label}</Tag>
        </div>
        <dl className="m-0 flex flex-col">
          {a.terms.map((t) => (
            <div key={t.k} className="grid gap-1 border-t border-divider py-3 first:border-t-0 md:grid-cols-[200px_minmax(0,1fr)] md:gap-4">
              <dt className="text-14 text-muted">{t.k}</dt>
              <dd className="m-0 text-15 leading-6">
                <BidiText text={t.v} />
              </dd>
            </div>
          ))}
        </dl>
        <div className="flex flex-wrap items-center gap-3 border-t border-divider pt-4">
          <a href={`/print/agreements/${encodeURIComponent(reference)}`} target="_blank" rel="noopener" className={buttonClasses({ variant: "secondary" })}>
            <Icon name="description" size={18} />
            النص الكامل PDF
            <span className="sr-only">(يفتح في نافذة جديدة للطباعة أو الحفظ PDF)</span>
          </a>
          <Link href={`${base}/solutions`} className={buttonClasses({ variant: "secondary" })}>
            مقارنة مع العرض المعتمد
          </Link>
          {a.versionMatchesOffer ? (
            <Tag tone="ok" icon="check_circle">مطابق لإصدار العرض المقبول</Tag>
          ) : (
            <Tag tone="warn" icon="error">لا يطابق إصدار العرض المقبول — راجع الفروق قبل التفعيل</Tag>
          )}
        </div>
      </section>

      <aside className="flex flex-col gap-4" aria-label="الموافقة والتفعيل">
        {a.consent ? (
          <section aria-labelledby="consent-h" className="flex flex-col gap-1.5 rounded-lg border border-line bg-white p-4 text-14">
            <h3 id="consent-h" className="m-0 flex items-center gap-2 text-15 font-bold text-ok">
              <Icon name="task_alt" size={20} />
              سجل موافقة المالك
            </h3>
            <BidiText text={a.consent.title} />
            <BidiText text={a.consent.meta} className="text-13 text-muted" />
            <span className="text-13">{a.consent.acks}</span>
          </section>
        ) : (
          <section aria-labelledby="consent-h" className="flex flex-col gap-1.5 rounded-lg border border-line bg-white p-4 text-14">
            <h3 id="consent-h" className="m-0 flex items-center gap-2 text-15 font-bold text-warn">
              <Icon name="hourglass_top" size={20} />
              سجل موافقة المالك
            </h3>
            <span className="text-muted">لا يوجد سجل موافقة موثّق بعد؛ لا يمكن التفعيل دونه.</span>
          </section>
        )}

        <section aria-labelledby="steps-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
          <h3 id="steps-h" className="m-0 text-15 font-bold">خطوات التفعيل</h3>
          <ol className="m-0 flex list-none flex-col gap-2.5 p-0 text-14">
            {a.steps.map((s) => (
              <li key={s.key} className="flex flex-col gap-1.5">
                <span className="flex items-start gap-2">
                  <Icon name={s.done ? "check_circle" : "schedule"} size={20} className={s.done ? "text-ok" : "text-muted"} />
                  <span className="flex-1">
                    {s.label}
                    <span className="sr-only">{s.done ? " — مكتملة" : " — بانتظار"}</span>
                  </span>
                </span>
                {!s.done && s.key === "legal_review" && p.canLegalReview ? (
                  <Button size="sm" variant="secondary" review className="ms-7 self-start" onClick={() => setDialog("legal")}>
                    تسجيل مراجعة القانونية
                  </Button>
                ) : null}
                {!s.done && s.key === "schedule" && p.canCreateSchedule ? (
                  <Button size="sm" variant="secondary" className="ms-7 self-start" onClick={() => setDialog("schedule")}>
                    إنشاء جدول السداد
                  </Button>
                ) : null}
              </li>
            ))}
          </ol>
          {p.canActivate ? (
            <div className="flex flex-col gap-2 border-t border-divider pt-3">
              {pendingSteps.length > 0 ? (
                <p id="act-hint" className="m-0 text-13 text-muted">
                  يتطلب التفعيل اكتمال: {pendingSteps.map((s) => s.label.split(" · ")[0]).join("، ")}.
                </p>
              ) : null}
              <Button review onClick={() => setDialog("activate")} aria-describedby={pendingSteps.length > 0 ? "act-hint" : undefined}>
                تفعيل الاتفاق
              </Button>
            </div>
          ) : null}
        </section>

        <div role="note" className="flex items-start gap-2.5 rounded-md border border-dashed border-line-strong bg-warm p-3.5 text-13 leading-5">
          <Icon name="draw" size={20} className="text-muted" />
          <div className="flex flex-col gap-2">
            <SigningNote note={a.signing.note} />
            <IntegrationStateTag state={toIntegrationState(a.signing.state)} />
          </div>
        </div>
      </aside>

      <ReasonDialog
        open={dialog === "legal"}
        onClose={() => setDialog(null)}
        title="تسجيل مراجعة القانونية"
        description={`يُسجَّل اسمك ووقت المراجعة على الاتفاق ${a.number} في سجل الحالة.`}
        label="ملاحظة المراجعة"
        confirmLabel="تسجيل المراجعة"
        onSubmit={async (note, key) => {
          await apiSend("POST", `${base}/agreement/legal-review`, { note }, { idempotencyKey: key });
          toast.toast({ tone: "ok", message: "سُجّلت مراجعة القانونية." });
          router.refresh();
        }}
      />
      <ConfirmDialog
        open={dialog === "schedule"}
        onClose={() => setDialog(null)}
        title="إنشاء جدول السداد"
        confirmLabel="إنشاء الجدول"
        onConfirm={async (key) => {
          const r = await apiSend<{ installments: number }>("POST", `${base}/agreement/schedule`, {}, { idempotencyKey: key });
          toast.toast({ tone: "ok", message: `أُنشئ جدول السداد (${r.installments} قسطاً).` });
          router.refresh();
        }}
      >
        <p className="m-0 text-14 leading-6">تُولَّد الأقساط من شروط الاتفاق على الخادم، ويمتص القسط الأخير فرق التقريب ليساوي مجموع الأقساط المبلغ المعاد جدولته.</p>
      </ConfirmDialog>
      <ConfirmDialog
        open={dialog === "activate"}
        onClose={() => setDialog(null)}
        title="تفعيل الاتفاق…"
        description="إجراء يُسجَّل في سجل الحالة مع الفاعل والوقت."
        confirmLabel="تأكيد التفعيل"
        onConfirm={async (key) => {
          await apiSend("POST", `${base}/agreement/activate`, {}, { idempotencyKey: key });
          toast.toast({ tone: "ok", message: "فُعّل الاتفاق · الحالة الآن «تسوية معتمدة / نشطة»." });
          router.refresh();
        }}
      >
        <ul className="m-0 flex list-none flex-col gap-2 rounded-md bg-subtle p-3 text-14 leading-[22px]">
          <li className="flex gap-2"><Icon name="swap_horiz" size={18} className="text-muted" />تنتقل الحالة إلى <strong>تسوية معتمدة / نشطة</strong>.</li>
          <li className="flex gap-2"><Icon name="notifications" size={18} className="text-muted" />يُبلَّغ المالك بأول قسط وبمكان جدول السداد.</li>
          <li className="flex gap-2"><Icon name="task_alt" size={18} className="text-muted" />تُنشأ مهمة للمالية لتحديث نظام التمويل الأساسي يدوياً.</li>
          <li className="flex gap-2"><Icon name="rule" size={18} className="text-muted" />يرفض الخادم التفعيل دون سجل موافقة المالك ومراجعة القانونية وجدول السداد، ويذكر السبب.</li>
        </ul>
      </ConfirmDialog>
    </div>
  );
}
