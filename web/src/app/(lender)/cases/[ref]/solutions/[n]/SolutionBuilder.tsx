"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import {
  Alert,
  AmountField,
  Button,
  DateField,
  Dialog,
  Icon,
  RadioCardGroup,
  TextField,
  Textarea,
  buttonClasses,
  useToast,
} from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { SOLUTION_KIND_LABEL, type SolutionCalc, type SolutionDetail } from "@/lib/api/lender";
import { formatDate, formatHijri, formatMoney, formatPercent, formatTime } from "@/lib/format";
import { cn } from "@/lib/cn";

type Params = {
  kind: string;
  termMonths: number;
  firstDueDate: string;
  waiverAmount: number;
  downPayment: number;
  graceMonths: number;
  justification: string;
  discountedPayoff: number | null;
};

export function SolutionBuilder({ reference, detail }: { reference: string; detail: SolutionDetail }) {
  const router = useRouter();
  const toast = useToast();
  const handoverKey = useIdempotencyKey();
  const { context: ctx, solution: s, previous: prev, permissions: perm } = detail;
  const editable = perm.canEdit;
  const [p, setP] = useState<Params>({
    kind: s.kind,
    termMonths: s.termMonths,
    firstDueDate: s.firstDueDate,
    waiverAmount: s.waiverAmount,
    downPayment: s.downPayment,
    graceMonths: s.graceMonths,
    justification: s.justification ?? "",
    discountedPayoff: s.discountedPayoffAmount,
  });
  const [calc, setCalc] = useState<SolutionCalc | null>(null);
  const [calcError, setCalcError] = useState<Record<string, string[]> | null>(null);
  const [rowVersion, setRowVersion] = useState(s.rowVersion);
  const [savedAt, setSavedAt] = useState<string>(s.preparedAt);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [handoverOpen, setHandoverOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Live impact: the server computes every figure (debounced), so the UI never does money math.
  useEffect(() => {
    if (!editable) return;
    const t = setTimeout(() => {
      apiSend<SolutionCalc>("POST", `/cases/${reference}/solutions/calculate`, { ...p, expectedVersion: null })
        .then((c) => {
          setCalc(c);
          setCalcError(null);
        })
        .catch((e) => setCalcError(isApiError(e) && e.errors ? e.errors : { _: [isApiError(e) ? e.title : "تعذّر الحساب."] }));
    }, 350);
    return () => clearTimeout(t);
  }, [p, reference, editable]);

  const view = calc ?? {
    rescheduledAmount: s.rescheduledAmount,
    installmentAmount: s.installmentAmount,
    finalInstallmentAmount: s.finalInstallmentAmount,
    lastDueDate: s.lastDueDate,
    waiverPercent: s.waiverPercent,
    dsr: s.dsr,
    dsrWithinLimit: s.dsr === null || s.dsr <= s.dsrLimit,
    installments: s.termMonths,
    lastDueHijri: "",
    firstDueHijri: s.firstDueHijri,
    route: detail.route,
  };
  const set = (patch: Partial<Params>) => {
    setP((x) => ({ ...x, ...patch }));
    setDirty(true);
  };

  const save = async (): Promise<boolean> => {
    setSaving(true);
    setError(null);
    try {
      const r = await apiSend<{ savedAt: string; rowVersion: number }>("PUT", `/cases/${reference}/solutions/${s.version}`, { ...p, expectedVersion: rowVersion });
      setRowVersion(r.rowVersion);
      setSavedAt(r.savedAt);
      setDirty(false);
      return true;
    } catch (e) {
      setError(isApiError(e) ? e.title : "تعذّر الحفظ.");
      return false;
    } finally {
      setSaving(false);
    }
  };

  const handover = async () => {
    if (dirty && !(await save())) return;
    try {
      const r = await apiSend<{ reviewer: string | null }>("POST", `/cases/${reference}/solutions/${s.version}/handover`, {}, { idempotencyKey: handoverKey.get() });
      handoverKey.reset();
      toast.toast({ tone: "ok", message: `سُلّم الحل v${s.version} للمراجعة${r.reviewer ? ` · ${r.reviewer}` : ""}` });
      router.push(`/cases/${reference}`);
      router.refresh();
    } catch (e) {
      handoverKey.reset();
      setHandoverOpen(false);
      setError(isApiError(e) ? [e.title, ...Object.values(e.errors ?? {}).flat()].join(" ") : "تعذّر التسليم.");
    }
  };

  const fieldErr = (k: string) => calcError?.[k]?.[0];
  const dsrPct = view.dsr === null ? null : view.dsr * 100;
  const limitPct = s.dsrLimit * 100;
  const payoff = p.kind === "ReducedPayoff";

  return (
    <div className="flex flex-col gap-5 pb-24">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-1 flex-col gap-1">
          <span className="text-13 text-muted">
            {ctx.owner} · القائم <bdi dir="ltr">{formatMoney(ctx.outstanding)}</bdi> ر.س
            {ctx.marketValue ? <> · التقييم <bdi dir="ltr">{formatMoney(ctx.marketValue)}</bdi> ر.س</> : null}
          </span>
          <h2 className="m-0 text-24 font-bold">{editable ? `منشئ الحل — الإصدار v${s.version}` : `الحل v${s.version}`}</h2>
        </div>
        {editable ? (
          <span className="flex items-center gap-1.5 text-13 text-muted" role="status">
            <Icon name={saving ? "sync" : dirty ? "edit" : "cloud_done"} size={18} />
            {saving ? "جارٍ الحفظ…" : dirty ? "تعديلات غير محفوظة" : <>مسودة محفوظة <bdi dir="ltr">{formatTime(savedAt)}</bdi></>}
          </span>
        ) : (
          <span className="text-13 text-muted">
            {s.lockedAt ? <>مقفل منذ <bdi dir="ltr">{formatDate(s.lockedAt)}</bdi> · أي تعديل ينشئ إصداراً جديداً</> : "للقراءة فقط"}
          </span>
        )}
      </div>

      {error ? <Alert tone="err" title={error} /> : null}
      {s.returnReason ? <Alert tone="warn" title="أُعيد هذا الإصدار" body={s.returnReason} /> : null}

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_420px]">
        <div className="flex min-w-0 flex-col gap-5">
          <RadioCardGroup
            legend="نوع الحل"
            name="kind"
            columns={4}
            value={p.kind}
            onChange={(v) => editable && set({ kind: v })}
            options={detail.kinds.map((k) => ({ value: k.key, label: k.label, description: k.desc, disabled: !editable || !k.enabled, disabledReason: k.reason ?? undefined }))}
          />

          <div className="grid gap-4 rounded-lg border border-line bg-white p-5 md:grid-cols-2">
            <div className="flex flex-col gap-1">
              <span className="text-14 font-semibold">{payoff ? "مبلغ السداد" : "المبلغ المعاد جدولته"}</span>
              <span className="flex h-11 items-center justify-between rounded-sm bg-subtle px-3 text-16 font-semibold tabular-nums">
                <bdi dir="ltr">{formatMoney(view.rescheduledAmount)}</bdi>
                <span className="text-14 text-muted">ر.س</span>
              </span>
              <span className="text-12 text-muted">{payoff ? "محسوب من مبلغ السداد المخفض" : "محسوب: القائم − التنازل − الدفعة المقدمة"}</span>
            </div>
            {payoff ? (
              <AmountField label="مبلغ السداد المخفض" requiredMark value={p.discountedPayoff} disabled={!editable} error={fieldErr("discountedPayoff")}
                onValueChange={(v) => set({ discountedPayoff: v })} />
            ) : (
              <TextField label="المدة" ltr inputMode="numeric" disabled={!editable} value={String(p.termMonths)} error={fieldErr("termMonths")}
                endAdornment={<span className="px-3 text-muted">شهراً</span>}
                help={`حتى ${ctx.maxTermMonths} شهراً وعمر المالك ≤ 65 عند النهاية (افتراض)`}
                onChange={(e) => set({ termMonths: Number(e.target.value.replace(/\D/g, "") || 0) })} />
            )}
            <DateField label={payoff ? "تاريخ السداد" : "أول قسط"} value={p.firstDueDate} disabled={!editable} error={fieldErr("firstDueDate")}
              onValueChange={(v) => set({ firstDueDate: v })} />
            <AmountField label="التنازل عن غرامات التأخير" value={p.waiverAmount} disabled={!editable || payoff} error={fieldErr("waiverAmount")}
              help={<>{formatPercent(view.waiverPercent * 100, { fractionDigits: 2 })} من القائم{p.waiverAmount === ctx.lateFeesDue && ctx.lateFeesDue > 0 ? " · كامل الغرامات المستحقة" : ` · الغرامات المستحقة ${formatMoney(ctx.lateFeesDue)}`}</>}
              onValueChange={(v) => set({ waiverAmount: v ?? 0 })} />
            {!payoff ? (
              <>
                <AmountField label="دفعة مقدمة" value={p.downPayment} disabled={!editable} error={fieldErr("downPayment")} onValueChange={(v) => set({ downPayment: v ?? 0 })} />
                <TextField label="فترة سماح" ltr inputMode="numeric" disabled={!editable} value={String(p.graceMonths)} error={fieldErr("graceMonths")}
                  endAdornment={<span className="px-3 text-muted">شهر</span>}
                  onChange={(e) => set({ graceMonths: Number(e.target.value.replace(/\D/g, "") || 0) })} />
              </>
            ) : null}
            <Textarea containerClassName="md:col-span-2" label={<>مبرر الحل <span className="font-normal text-muted">(يظهر للمراجِع والمعتمد، لا للمالك)</span></>}
              value={p.justification} disabled={!editable} maxLength={2000} onChange={(e) => set({ justification: e.target.value })} />
          </div>

          <section className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5 text-14">
            <strong>شروط الحل</strong>
            <p className="m-0 flex gap-2">
              <Icon name="rule" size={20} className="text-muted" />
              <span>
                عند تأخر قسطين متتاليين: تُفتح <strong>مراجعة الإخلال</strong> مع إشعار المالك ومهلة تصحيح {s.breachCureDays} يوماً. <span className="text-muted">لا إحالة تلقائية.</span>
              </span>
            </p>
            <p className="m-0 flex gap-2">
              <Icon name="event_available" size={20} className="text-muted" />
              <span>صلاحية العرض للمالك: {s.offerValidityDays} أيام من إرساله (قابلة للتهيئة).</span>
            </p>
          </section>
        </div>

        <aside aria-label="أثر الحل" aria-live="polite" className="flex flex-col gap-4 xl:sticky xl:top-4">
          <div className="flex flex-col gap-1 rounded-lg bg-ink p-5 text-white">
            <span className="text-14 text-inv-2">{payoff ? "مبلغ السداد" : "القسط الشهري المقترح"}</span>
            <span className="text-32 leading-[44px] font-bold">
              <bdi dir="ltr" className="tabular-nums">{formatMoney(payoff ? view.rescheduledAmount : view.installmentAmount)}</bdi> <span className="text-16 font-medium">ر.س</span>
            </span>
            {!payoff ? (
              <span className="text-13 text-inv-2">
                {view.installments} قسطاً · من <bdi dir="ltr">{formatDate(p.firstDueDate)}</bdi> إلى <bdi dir="ltr">{formatDate(view.lastDueDate)}</bdi>
              </span>
            ) : null}
            <span className="text-12 text-inv-2">{formatHijri(p.firstDueDate)}</span>
          </div>

          <div className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4 text-14">
            <div className="flex items-baseline justify-between">
              <strong>القدرة على السداد</strong>
              <span className="text-12 text-muted">{ctx.incomeSource ?? "لا مستند دخل"}{ctx.incomeVerifiedOn ? <> · <bdi dir="ltr">{ctx.incomeVerifiedOn}</bdi></> : null}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted">صافي الدخل الشهري المتحقق</span>
              <span><bdi dir="ltr">{ctx.netIncome ? formatMoney(ctx.netIncome) : "—"}</bdi> ر.س</span>
            </div>
            {dsrPct !== null ? (
              <>
                <div role="meter" aria-valuenow={Number(dsrPct.toFixed(1))} aria-valuemin={0} aria-valuemax={100} aria-label={`نسبة الاستقطاع ${dsrPct.toFixed(1)} بالمئة، الحد ${limitPct}`}
                  className="relative h-2.5 overflow-hidden rounded-full bg-track">
                  <span className={cn("absolute inset-y-0 start-0 rounded-full", view.dsrWithinLimit ? "bg-ok" : "bg-err")} style={{ width: `${Math.min(dsrPct, 100)}%` }} />
                  <span className="absolute inset-y-0 w-0.5 bg-ink" style={{ insetInlineStart: `${limitPct}%` }} aria-hidden="true" />
                </div>
                <div className="flex justify-between text-13">
                  <span className={cn("inline-flex items-center gap-1 font-semibold", view.dsrWithinLimit ? "text-ok" : "text-err")}>
                    <Icon name={view.dsrWithinLimit ? "check_circle" : "error"} size={16} />
                    الاستقطاع {formatPercent(dsrPct, { fractionDigits: 1 })} — {view.dsrWithinLimit ? "ضمن الحد" : "فوق الحد"}
                  </span>
                  <span className="text-muted">حد السياسة {formatPercent(limitPct, { fractionDigits: 0 })} (افتراض)</span>
                </div>
              </>
            ) : (
              <p className="m-0 text-13 text-muted">لا يمكن حساب الاستقطاع دون دخل متحقق في تحليل القدرة.</p>
            )}
          </div>

          {prev ? (
            <div className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4 text-14">
              <strong>مقارنة بالإصدار السابق</strong>
              <table className="w-full border-collapse text-13">
                <thead>
                  <tr className="text-muted">
                    <th scope="col" className="py-1 text-start font-medium">البند</th>
                    <th scope="col" className="py-1 text-start font-medium">v{prev.version}</th>
                    <th scope="col" className="py-1 text-start font-medium">v{s.version}</th>
                  </tr>
                </thead>
                <tbody className="tabular-nums">
                  <tr className="border-t border-divider"><td className="py-1">المدة</td><td>{prev.termMonths}</td><td>{p.termMonths}</td></tr>
                  <tr className="border-t border-divider"><td className="py-1">القسط</td><td><bdi dir="ltr">{formatMoney(prev.installmentAmount)}</bdi></td><td><bdi dir="ltr">{formatMoney(view.installmentAmount)}</bdi></td></tr>
                  <tr className="border-t border-divider">
                    <td className="py-1">الاستقطاع</td>
                    <td>{prev.dsr === null ? "—" : `${formatPercent(prev.dsr * 100, { fractionDigits: 1 })} ${prev.dsr <= prev.dsrLimit ? "✓" : "✕"}`}</td>
                    <td>{dsrPct === null ? "—" : `${formatPercent(dsrPct, { fractionDigits: 1 })} ${view.dsrWithinLimit ? "✓" : "✕"}`}</td>
                  </tr>
                  <tr className="border-t border-divider"><td className="py-1">التنازل</td><td><bdi dir="ltr">{formatMoney(prev.waiverAmount)}</bdi></td><td><bdi dir="ltr">{formatMoney(p.waiverAmount)}</bdi></td></tr>
                </tbody>
              </table>
            </div>
          ) : null}

          <div className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4 text-14">
            <strong>مسار الموافقة المطلوب</strong>
            <span className="flex items-center gap-1.5"><Icon name="person" size={18} className="text-muted" />مراجعة: {view.route.reviewer ?? "مدير الحالة"}</span>
            {view.route.noApprover ? (
              <span className="flex items-center gap-1.5 text-err"><Icon name="error" size={18} />لا يوجد معتمد ضمن الحدود لهذا الحل.</span>
            ) : (
              <span className="flex items-center gap-1.5">
                <Icon name="approval" size={18} className="text-muted" />
                اعتماد: {view.route.approver} — {view.route.escalated ? "مُصعّد" : "ضمن حدها"} ({view.route.approverLimit})
              </span>
            )}
            {perm.isPreparer ? (
              <span className="flex items-center gap-1.5 text-muted"><Icon name="block" size={18} />أنت المُعِدّ: لا يمكنك المراجعة أو الاعتماد.</span>
            ) : null}
          </div>
        </aside>
      </div>

      {/* Sticky action bar (design footer; on narrow screens the installment stays visible) */}
      <div className="fixed inset-x-0 bottom-0 z-20 border-t border-line bg-white px-4 py-3 md:ps-[calc(var(--shell-rail,0px)+40px)] lg:static lg:-mx-10 lg:px-10">
        <div className="flex flex-wrap items-center gap-3">
          <span className="text-14 font-semibold lg:hidden">
            القسط <bdi dir="ltr">{formatMoney(view.installmentAmount)}</bdi> ر.س
          </span>
          {editable ? (
            <>
              <Button variant="secondary" onClick={() => void save()} loading={saving} disabled={!dirty} className="max-lg:hidden">حفظ المسودة</Button>
              <Link href={`/cases/${reference}/solutions/${s.version}/preview`} className={cn(buttonClasses({ variant: "secondary" }), "max-lg:hidden")}>
                <Icon name="smartphone" size={18} />
                معاينة كما يراه المالك
              </Link>
              <span className="ms-auto text-13 text-muted max-lg:hidden">{calcError ? "صحّح الحقول المميزة" : "كل الحقول الإلزامية مكتملة"}</span>
              {perm.canHandover ? (
                <Button onClick={() => setHandoverOpen(true)} disabled={!!calcError} className="max-lg:ms-auto">
                  تسليم لمدير الحالة للمراجعة
                </Button>
              ) : null}
            </>
          ) : (
            <>
              <Link href={`/cases/${reference}/solutions/${s.version}/preview`} className={buttonClasses({ variant: "secondary" })}>
                <Icon name="smartphone" size={18} />
                معاينة كما يراه المالك
              </Link>
              {perm.canSubmit ? (
                <Link href={`/cases/${reference}/solutions/${s.version}/submit`} className={cn(buttonClasses({ variant: "primary" }), "ms-auto")}>مراجعة وإرسال…</Link>
              ) : null}
            </>
          )}
        </div>
      </div>

      <Dialog
        open={handoverOpen}
        onClose={() => setHandoverOpen(false)}
        title={`تسليم الحل v${s.version} للمراجعة`}
        description="بعد التسليم لا يمكنك تعديل هذا الإصدار؛ يراجعه مدير الحالة ويرسله للاعتماد أو يعيده إليك."
        footer={
          <>
            <Button variant="secondary" onClick={() => setHandoverOpen(false)}>رجوع</Button>
            <Button onClick={() => void handover()}>تأكيد التسليم</Button>
          </>
        }
      >
        <ul className="m-0 flex list-none flex-col gap-2 p-0 text-14">
          <li>النوع: {SOLUTION_KIND_LABEL[p.kind]}</li>
          <li>القسط: <bdi dir="ltr">{formatMoney(view.installmentAmount)}</bdi> ر.س · {view.installments} قسطاً</li>
          <li>التنازل: <bdi dir="ltr">{formatMoney(p.waiverAmount)}</bdi> ر.س ({formatPercent(view.waiverPercent * 100, { fractionDigits: 2 })})</li>
        </ul>
      </Dialog>
    </div>
  );
}
