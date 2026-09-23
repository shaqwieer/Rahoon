"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { ConfirmDialog } from "@/components/case/ConfirmDialog";
import { ReasonDialog } from "@/components/case/ReasonDialog";
import { Alert, Button, Drawer, Icon, KpiTile, useToast } from "@/components/ui";
import { apiSend } from "@/lib/api/client";
import { canRecordOn, recordableInstallments, type InstallmentDto, type PaymentsActive } from "@/lib/api/lender";
import { cn } from "@/lib/cn";
import { formatDate, formatMoney, formatNumber } from "@/lib/format";
import { RecordPaymentForm } from "./RecordPaymentForm";

/** Payment status → icon + colour (text always carries the meaning). */
const STATUS_LOOK: Record<string, { icon: string; cls: string }> = {
  Matched: { icon: "check_circle", cls: "bg-ok-bg text-ok" },
  RecordedPendingMatch: { icon: "pending", cls: "bg-warn-bg text-warn" },
  Due: { icon: "schedule", cls: "bg-subtle text-charcoal" },
  Upcoming: { icon: "radio_button_unchecked", cls: "bg-white text-muted" },
  Partial: { icon: "timelapse", cls: "bg-warn-bg text-warn" },
  Overdue: { icon: "event_busy", cls: "bg-err-bg text-err" },
  Waived: { icon: "do_not_disturb_on", cls: "bg-subtle text-charcoal" },
};

export function PaymentStatus({ i }: { i: InstallmentDto }) {
  const look = STATUS_LOOK[i.status] ?? STATUS_LOOK.Upcoming;
  return (
    <span className={cn("inline-flex items-center gap-1 rounded-xs px-2 py-0.5 text-12 font-semibold whitespace-nowrap", look.cls)}>
      <Icon name={look.icon} size={14} />
      {i.statusLabel}
    </span>
  );
}

const PAGE = 12;

type Pending = { kind: "match" | "reject"; i: InstallmentDto } | null;

export function PaymentsView({ reference, d, today }: { reference: string; d: PaymentsActive; today: string }) {
  const router = useRouter();
  const toast = useToast();
  const [drawer, setDrawer] = useState(false);
  const [all, setAll] = useState(false);
  const [pending, setPending] = useState<Pending>(null);
  const base = `/cases/${reference}`;
  const k = d.kpis;
  const eligible = recordableInstallments(d.installments);
  const recordBlocked = !canRecordOn(d.agreement.status)
    ? "التسجيل متاح أثناء سريان الاتفاق فقط."
    : eligible.length === 0
      ? "لا توجد أقساط متاحة للتسجيل."
      : null;
  const rows = all ? d.installments : d.installments.slice(0, PAGE);
  const progress = d.agreement.installmentCount > 0 ? Math.round((k.paidCount / d.agreement.installmentCount) * 100) : 0;

  const actions = (i: InstallmentDto) =>
    i.status === "RecordedPendingMatch" && i.paymentId ? (
      i.canMatch ? (
        <span className="flex flex-wrap gap-1.5">
          <Button size="sm" onClick={() => setPending({ kind: "match", i })}>مطابقة</Button>
          <Button size="sm" variant="secondary" review onClick={() => setPending({ kind: "reject", i })}>رفض</Button>
        </span>
      ) : i.matchBlockedReason ? (
        <span className="text-12 text-muted">{i.matchBlockedReason}</span>
      ) : null
    ) : null;

  return (
    <div className="flex flex-col gap-5">
      {d.breach ? (
        <Alert
          tone="warn"
          icon="warning"
          title="مراجعة إخلال مفتوحة"
          action={<Link href={`${base}/payments/breach`}>مراجعة الإخلال</Link>}
        >
          الأقساط غير المسددة: <bdi dir="ltr">{d.breach.missed.join("، ")}</bdi> · مهلة التصحيح حتى <bdi dir="ltr">{formatDate(d.breach.cureDeadline)}</bdi>
        </Alert>
      ) : null}

      {/* Mobile progress summary (390) */}
      <section aria-label="ملخص السداد" className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4 md:hidden">
        <span className="text-15 font-bold">
          سُدد <bdi dir="ltr">{formatNumber(k.paidCount)}</bdi> من <bdi dir="ltr">{formatNumber(d.agreement.installmentCount)}</bdi>
        </span>
        <div role="progressbar" aria-label="نسبة الأقساط المطابقة" aria-valuenow={progress} aria-valuemin={0} aria-valuemax={100} className="h-2 overflow-hidden rounded-pill bg-track">
          <span className="block h-full bg-ok" style={{ width: `${progress}%` }} />
        </div>
        <span className="text-14 text-muted">
          المتبقي <bdi dir="ltr">{formatMoney(k.remaining)}</bdi> ر.س
        </span>
      </section>

      <div className="grid grid-cols-2 gap-3 max-md:hidden xl:grid-cols-4">
        <KpiTile label="المُعاد جدولته" value={formatMoney(k.rescheduled)} unit="ر.س" sub={<bdi dir="ltr">{d.agreement.number}</bdi>} />
        <KpiTile label="المسدد" value={formatMoney(k.paid)} unit="ر.س" sub={`${formatNumber(k.paidCount)} من ${formatNumber(d.agreement.installmentCount)} قسطاً · المطابقة فقط`} />
        <KpiTile label="المتبقي" value={formatMoney(k.remaining)} unit="ر.س" sub="لا يشمل الدفعات بانتظار المطابقة" />
        <KpiTile label="بانتظار المطابقة" value={formatNumber(k.pendingMatch)} iconTone="warn" icon={k.pendingMatch > 0 ? "pending" : undefined} sub="دفعة مسجلة" />
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <h2 className="m-0 flex-1 text-18 font-bold">جدول السداد</h2>
        {d.canRecord ? (
          <>
            {recordBlocked ? (
              <span id="rec-blocked" className="text-13 text-muted">{recordBlocked}</span>
            ) : null}
            <Button icon="add" className="max-md:hidden" softDisabled={!!recordBlocked} aria-describedby={recordBlocked ? "rec-blocked" : undefined} onClick={() => setDrawer(true)}>
              تسجيل دفعة
            </Button>
            <Button icon="add" className="md:hidden" href={recordBlocked ? undefined : `${base}/payments/record`} softDisabled={!!recordBlocked} aria-describedby={recordBlocked ? "rec-blocked" : undefined}>
              تسجيل دفعة
            </Button>
          </>
        ) : null}
      </div>

      {/* Desktop / tablet table */}
      <div className="overflow-x-auto rounded-lg border border-line bg-white max-md:hidden">
        <table className="w-full min-w-[760px] border-collapse text-14">
          <caption className="sr-only">جدول السداد: الاستحقاق والمبلغ والمدفوع والمرجع والحالة لكل قسط</caption>
          <thead>
            <tr className="bg-warm text-13 text-muted">
              <th scope="col" className="w-[60px] px-4 py-2.5 text-start font-semibold">#</th>
              <th scope="col" className="px-3 py-2.5 text-start font-semibold">الاستحقاق</th>
              <th scope="col" className="px-3 py-2.5 text-start font-semibold">المبلغ</th>
              <th scope="col" className="px-3 py-2.5 text-start font-semibold">المدفوع</th>
              <th scope="col" className="px-3 py-2.5 text-start font-semibold">المرجع</th>
              <th scope="col" className="px-3 py-2.5 text-start font-semibold">الحالة</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((i) => (
              <tr key={i.id} className={cn("border-t border-divider align-top", i.status === "Due" && "bg-warm")}>
                <td className="px-4 py-3 tabular-nums"><bdi dir="ltr">{i.no}</bdi></td>
                <td className="px-3 py-3 tabular-nums"><bdi dir="ltr">{formatDate(i.dueDate)}</bdi></td>
                <td className="px-3 py-3 tabular-nums"><bdi dir="ltr">{formatMoney(i.amount)}</bdi></td>
                <td className="px-3 py-3 tabular-nums">{i.paid === null ? "—" : <bdi dir="ltr">{formatMoney(i.paid)}</bdi>}</td>
                <td className="px-3 py-3">
                  {i.reference ? <bdi dir="ltr" className="font-mono text-13">{i.reference}</bdi> : "—"}
                  {i.recordedBy ? <span className="block text-12 text-muted">سجّلها {i.recordedBy}</span> : null}
                </td>
                <td className="px-3 py-3">
                  <span className="flex flex-col items-start gap-1.5">
                    <PaymentStatus i={i} />
                    {actions(i)}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Mobile cards */}
      <ul className="m-0 flex list-none flex-col gap-2 p-0 md:hidden" aria-label="الأقساط">
        {rows.map((i) => (
          <li key={i.id} className="flex flex-col gap-1.5 rounded-md border border-line bg-white p-3.5">
            <span className="flex items-center justify-between gap-2">
              <strong className="text-15">
                القسط <bdi dir="ltr">{i.no}</bdi> · <bdi dir="ltr">{formatDate(i.dueDate)}</bdi>
              </strong>
              <bdi dir="ltr" className="tabular-nums">{formatMoney(i.amount)}</bdi>
            </span>
            <span className="flex flex-wrap items-center gap-2">
              <PaymentStatus i={i} />
              {i.reference ? <bdi dir="ltr" className="font-mono text-12 text-muted">{i.reference}</bdi> : null}
            </span>
            {actions(i)}
          </li>
        ))}
      </ul>

      {d.installments.length > PAGE ? (
        <Button variant="text" className="self-start" onClick={() => setAll((v) => !v)} aria-expanded={all}>
          {all ? "عرض أول 12 قسطاً" : `عرض كل الأقساط (${formatNumber(d.installments.length)})`}
        </Button>
      ) : null}

      <Drawer open={drawer} onClose={() => setDrawer(false)} title="تسجيل دفعة يدوياً">
        <RecordPaymentForm
          reference={reference}
          installments={eligible}
          today={today}
          onCancel={() => setDrawer(false)}
          onDone={() => {
            setDrawer(false);
            toast.toast({ tone: "ok", message: "سُجّلت الدفعة — بانتظار المطابقة من موظف مالية آخر." });
            router.refresh();
          }}
        />
      </Drawer>

      <ConfirmDialog
        open={pending?.kind === "match"}
        onClose={() => setPending(null)}
        title="مطابقة الدفعة"
        description="خطوة المدقق: تأكد أن المرجع والمبلغ يطابقان كشف الحساب البنكي."
        confirmLabel="تأكيد المطابقة"
        onConfirm={async (key) => {
          if (!pending?.i.paymentId) return;
          const r = await apiSend<{ caseStatus: string }>("POST", `${base}/payments/${pending.i.paymentId}/match`, {}, { idempotencyKey: key });
          toast.toast({ tone: "ok", message: r.caseStatus === "awaiting_reconciliation" ? "طوبقت الدفعة واكتمل السداد · انتقلت الحالة للتسوية المالية." : "طوبقت الدفعة وتظهر للمالك «مستلمة»." });
          router.refresh();
        }}
      >
        {pending ? (
          <dl className="m-0 grid grid-cols-[120px_minmax(0,1fr)] gap-x-3 gap-y-2 rounded-md bg-subtle p-3 text-14">
            <dt className="text-muted">القسط</dt>
            <dd className="m-0"><bdi dir="ltr">{pending.i.no}</bdi> · <bdi dir="ltr">{formatDate(pending.i.dueDate)}</bdi></dd>
            <dt className="text-muted">المبلغ</dt>
            <dd className="m-0"><bdi dir="ltr">{formatMoney(pending.i.paid)}</bdi> ر.س</dd>
            <dt className="text-muted">المرجع</dt>
            <dd className="m-0"><bdi dir="ltr" className="font-mono">{pending.i.reference}</bdi></dd>
            <dt className="text-muted">سجّلها</dt>
            <dd className="m-0">{pending.i.recordedBy ?? "—"}</dd>
          </dl>
        ) : null}
      </ConfirmDialog>
      <ReasonDialog
        open={pending?.kind === "reject"}
        onClose={() => setPending(null)}
        title="رفض الدفعة المسجلة"
        description="يعود القسط إلى «مستحقة» أو «متأخرة»، ويُسجَّل السبب في سجل الحالة."
        label="سبب الرفض"
        confirmLabel="تأكيد الرفض"
        confirmVariant="sensitive"
        onSubmit={async (reason, key) => {
          if (!pending?.i.paymentId) return;
          await apiSend("POST", `${base}/payments/${pending.i.paymentId}/reject`, { reason }, { idempotencyKey: key });
          toast.toast({ tone: "ok", message: "رُفضت الدفعة المسجلة." });
          router.refresh();
        }}
      />
    </div>
  );
}
