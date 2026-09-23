"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import {
  Alert,
  Button,
  DateText,
  Dialog,
  EmptyState,
  Icon,
  KeyValueList,
  Money,
  Select,
  Tag,
  Textarea,
  TextField,
  useToast,
} from "@/components/ui";
import { TONES } from "@/components/ui/tones";
import { apiSend, isApiError, useIdempotencyKey, type ApiError } from "@/lib/api/client";
import type { FinanceData } from "@/lib/api/caseInfo";
import { cn } from "@/lib/cn";
import { formatMoney, formatNumber } from "@/lib/format";

type MonthStatus = FinanceData["history"]["months"][number]["status"];

const MONTH_STYLE: Record<MonthStatus, { label: string; icon: string; fg: string; bg: string; bd: string }> = {
  paid: { label: "مسدد", icon: "check_circle", ...TONES.ok },
  unpaid: { label: "غير مسدد", icon: "cancel", ...TONES.err },
  partial: { label: "جزئي", icon: "contrast", ...TONES.warn },
  due: { label: "مستحق", icon: "schedule", fg: "#5E5D58", bg: "#FFFFFF", bd: "#CBCAC6" },
};

export function FinanceView({ reference, data }: { reference: string; data: FinanceData }) {
  const [correction, setCorrection] = useState(false);
  const [seq, setSeq] = useState(0);
  const openCorrection = () => {
    setSeq((n) => n + 1);
    setCorrection(true);
  };
  const s = data.snapshot;
  const c = data.contract;
  const stale = s?.staleTag ? <Tag tone="err" icon="sync_problem">{s.staleTag}</Tag> : null;

  return (
    <div className="flex flex-col gap-5">
      <h2 className="sr-only">التمويل والمديونية</h2>
      {data.banner ? (
        <Alert
          tone={data.banner.tone}
          icon={data.banner.icon}
          role={data.banner.tone === "info" ? "none" : "alert"}
          action={
            data.canRequestCorrection ? (
              <Button variant="secondary" size="sm" onClick={openCorrection}>طلب تصحيح بيانات</Button>
            ) : undefined
          }
        >
          {data.banner.text}
        </Alert>
      ) : data.canRequestCorrection ? (
        <div className="flex justify-end">
          <Button variant="secondary" size="sm" onClick={openCorrection}>طلب تصحيح بيانات</Button>
        </div>
      ) : null}

      {s ? (
        <div className="flex flex-col gap-1 rounded-lg border border-line bg-white p-4 md:hidden">
          <span className="text-13 text-muted">إجمالي القائم</span>
          <strong className="text-24 leading-9">
            <Money value={s.total} />
          </strong>
          <span className="flex flex-wrap items-center gap-1.5 text-12 text-muted">
            {s.source} · <DateText value={s.asOf} mode="datetime" /> {stale}
          </span>
        </div>
      ) : null}

      <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)]">
        <section aria-labelledby="debt-h" className="overflow-hidden rounded-lg border border-line bg-white">
          <div className="flex flex-wrap items-center gap-2 border-b border-divider px-5 py-3">
            <h3 id="debt-h" className="m-0 flex-1 text-16 font-semibold">تفصيل المديونية القائمة</h3>
            {stale}
          </div>
          {s ? (
            <>
              <table className="w-full border-collapse text-14">
                <caption className="sr-only">تفصيل المديونية</caption>
                <thead>
                  <tr className="bg-warm text-13 text-muted">
                    <th scope="col" className="px-5 py-2 text-start font-semibold">البند</th>
                    <th scope="col" className="px-3 py-2 text-start font-semibold">المبلغ (ر.س)</th>
                    <th scope="col" className="hidden px-5 py-2 text-start font-semibold sm:table-cell">المصدر</th>
                  </tr>
                </thead>
                <tbody>
                  {s.items.map((i) => (
                    <tr key={i.code} className="border-t border-divider">
                      <th scope="row" className="px-5 py-3 text-start font-normal">
                        {i.label}
                        <span className="block text-12 text-muted sm:hidden">{i.source ?? "—"}</span>
                      </th>
                      <td className="px-3 py-3">
                        <bdi dir="ltr" className="tabular-nums">{formatMoney(i.amount)}</bdi>
                      </td>
                      <td className="hidden px-5 py-3 text-13 text-muted sm:table-cell">{i.source ?? "—"}</td>
                    </tr>
                  ))}
                  <tr className="border-t border-line bg-warm font-bold">
                    <th scope="row" className="px-5 py-3 text-start">إجمالي القائم</th>
                    <td className="px-3 py-3">
                      <bdi dir="ltr" className="tabular-nums">{formatMoney(s.total)}</bdi>
                    </td>
                    <td className="hidden px-5 py-3 text-13 font-normal text-muted sm:table-cell">
                      <span className="inline-flex items-center gap-1">
                        {s.totalVerified ? <Icon name="check_circle" size={16} className="text-ok" /> : null}
                        {s.totalSource}
                      </span>
                    </td>
                  </tr>
                </tbody>
              </table>
              {s.totalWarning ? (
                <div className="border-t border-divider p-4">
                  <Alert tone="err" compact title="المجموع لا يطابق الإجمالي المسجل">{s.totalWarning}</Alert>
                </div>
              ) : null}
            </>
          ) : (
            <p className="m-0 px-5 py-4 text-14 text-muted">لا توجد لقطة مديونية من نظام التمويل بعد.</p>
          )}
        </section>

        <section aria-labelledby="contract-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <h3 id="contract-h" className="m-0 text-16 font-semibold">بيانات العقد</h3>
          {c ? (
            <KeyValueList
              dense
              rows={[
                { key: "رقم العقد", value: <bdi dir="ltr" className="font-mono">{c.contractNumberMasked}</bdi> },
                ...(c.productType ? [{ key: "نوع المنتج", value: c.productType }] : []),
                { key: "تاريخ العقد", value: c.contractDate ? <DateText value={c.contractDate} /> : "—" },
                { key: "مبلغ التمويل الأصلي", value: c.originalAmount === null ? "—" : <Money value={c.originalAmount} /> },
                { key: "المدة الأصلية", value: c.termLabel ?? "—" },
                { key: "القسط الأصلي", value: c.originalInstallment === null ? "—" : <Money value={c.originalInstallment} /> },
                { key: "هامش الربح", value: c.profitType ? c.profitLabel : "—" },
                { key: "أول قسط متأخر", value: c.firstOverdueDate ? <DateText value={c.firstOverdueDate} /> : "—" },
              ]}
            />
          ) : (
            <p className="m-0 text-14 text-muted">لا توجد بيانات عقد مسجلة.</p>
          )}
        </section>
      </div>

      <History history={data.history} arrears={data.arrears} />

      {data.canRequestCorrection ? (
        <CorrectionDialog
          key={`corr-${seq}`}
          reference={reference}
          data={data}
          open={correction}
          onClose={() => setCorrection(false)}
        />
      ) : null}
    </div>
  );
}

function History({ history: h, arrears }: { history: FinanceData["history"]; arrears: FinanceData["arrears"] }) {
  const [asTable, setAsTable] = useState(false);
  return (
    <section aria-labelledby="hist-h" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <h3 id="hist-h" className="m-0 flex-1 text-16 font-semibold">{h.title}</h3>
        {h.originalInstallment !== null ? (
          <span className="text-13 text-muted">
            القسط الأصلي <Money value={h.originalInstallment} />
          </span>
        ) : null}
        {h.months.length > 0 ? (
          <Button variant="text" size="sm" icon={asTable ? "view_module" : "table"} aria-pressed={asTable} onClick={() => setAsTable((v) => !v)}>
            {asTable ? "عرض كشريط" : "عرض كجدول"}
          </Button>
        ) : null}
      </div>

      {h.months.length === 0 ? (
        <EmptyState icon="calendar_month" title="لا يوجد سجل أقساط" body="لم يُستورد سجل الأقساط من نظام التمويل بعد." headingLevel={3} />
      ) : asTable ? (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[480px] border-collapse text-14">
            <caption className="sr-only">سجل الأقساط كجدول</caption>
            <thead>
              <tr className="bg-warm text-13 text-muted">
                <th scope="col" className="px-3 py-2 text-start font-semibold">الشهر</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">الحالة</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">المستحق (ر.س)</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">المسدد (ر.س)</th>
              </tr>
            </thead>
            <tbody>
              {h.months.map((m) => (
                <tr key={m.month} className="border-t border-divider">
                  <th scope="row" className="px-3 py-2 text-start font-normal"><bdi dir="ltr" className="font-mono">{m.month}</bdi></th>
                  <td className="px-3 py-2">{m.label}</td>
                  <td className="px-3 py-2"><bdi dir="ltr" className="tabular-nums">{formatMoney(m.amountDue)}</bdi></td>
                  <td className="px-3 py-2"><bdi dir="ltr" className="tabular-nums">{formatMoney(m.amountPaid)}</bdi></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <ol aria-label="سجل الأقساط" className="m-0 grid list-none grid-cols-4 gap-1.5 p-0 sm:grid-cols-6 xl:grid-cols-12">
          {h.months.map((m) => {
            const st = MONTH_STYLE[m.status];
            return (
              <li
                key={m.month}
                aria-label={m.aria}
                className="flex flex-col items-center gap-1 rounded-sm border px-1 py-2 text-center"
                style={{ color: st.fg, background: st.bg, borderColor: st.bd }}
              >
                <Icon name={m.icon} size={18} />
                <bdi dir="ltr" className="font-mono text-12 font-semibold">{m.shortLabel}</bdi>
                <span className="text-11 leading-4">{m.label}</span>
              </li>
            );
          })}
        </ol>
      )}

      <ul aria-label="دليل الألوان" className="m-0 flex list-none flex-wrap gap-x-4 gap-y-1 p-0 text-12 text-muted">
        {(Object.keys(MONTH_STYLE) as MonthStatus[]).map((k) => (
          <li key={k} className="inline-flex items-center gap-1">
            <Icon name={MONTH_STYLE[k].icon} size={16} style={{ color: MONTH_STYLE[k].fg }} />
            {MONTH_STYLE[k].label}
          </li>
        ))}
      </ul>

      {h.summary ? <p className="m-0 text-14 leading-6">{h.summary}</p> : null}
      {arrears.reportedAmount !== null && arrears.source ? (
        <p className="m-0 text-12 text-muted">
          المتأخرات المعلنة <Money value={arrears.reportedAmount} />
          {arrears.reportedInstallments !== null ? <> · {formatNumber(arrears.reportedInstallments)} أقساط</> : null} · المصدر: {arrears.source}
        </p>
      ) : null}
    </section>
  );
}

function CorrectionDialog({ reference, data, open, onClose }: { reference: string; data: FinanceData; open: boolean; onClose: () => void }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [field, setField] = useState("");
  const [month, setMonth] = useState("");
  const [current, setCurrent] = useState("");
  const [proposed, setProposed] = useState("");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | { title: string; fieldError: (f: string) => string | undefined } | null>(null);

  const pickField = (v: string) => {
    key.reset();
    setField(v);
    const item = data.snapshot?.items.find((i) => i.code === v);
    const c = data.contract;
    const known: Record<string, string | null | undefined> = {
      contract_number: c?.contractNumberMasked,
      contract_date: c?.contractDate,
      original_amount: c?.originalAmount === null || c?.originalAmount === undefined ? null : formatMoney(c.originalAmount),
      original_term: c?.originalTermMonths?.toString(),
      remaining_term: c?.remainingTermMonths?.toString(),
      original_installment: c?.originalInstallment === null || c?.originalInstallment === undefined ? null : formatMoney(c.originalInstallment),
      first_overdue_date: c?.firstOverdueDate,
    };
    setCurrent(item ? formatMoney(item.amount) : (known[v] ?? ""));
  };

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const res = await apiSend<{ assignee: string; dueOn: string }>(
        "POST",
        `/cases/${reference}/finance/correction-requests`,
        { field, currentValue: current.trim() || null, proposedValue: proposed.trim(), note: note.trim(), month: field === "installment_history" && month ? month : null },
        { idempotencyKey: key.get() },
      );
      key.reset();
      toast.toast({ tone: "ok", message: `أُنشئت مهمة تصحيح لـ${res.assignee} (المالية) · المهلة ${res.dueOn}. لم يُعدّل أي رقم.` });
      onClose();
      router.refresh();
    } catch (e) {
      key.reset();
      setError(isApiError(e) ? e : { title: "تعذّر إرسال طلب التصحيح.", fieldError: () => undefined });
    } finally {
      setBusy(false);
    }
  };

  const edit = (setter: (v: string) => void) => (v: string) => {
    key.reset();
    setter(v);
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="طلب تصحيح بيانات"
      description="ينشئ مهمة للمالية لمراجعة البند مع المصدر، ولا يعدّل الرقم مباشرة. يُسجَّل الطلب في سجل الحالة."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void submit()} loading={busy} disabled={!field || !proposed.trim() || note.trim().length < 10}>
            إرسال للمالية
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {error ? <Alert tone="err" title={error.title || "تعذّر إرسال طلب التصحيح."} /> : null}
        <Select
          label="البند"
          requiredMark
          placeholder="اختر البند"
          value={field}
          onChange={(e) => pickField(e.target.value)}
          options={data.correctionFields.map((f) => ({ value: f.key, label: f.label }))}
          error={error?.fieldError("field")}
        />
        {field === "installment_history" ? (
          <TextField label="الشهر" ltr mono placeholder="YYYY-MM" maxLength={7} value={month} onChange={(e) => edit(setMonth)(e.target.value)} error={error?.fieldError("month")} />
        ) : null}
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label="القيمة الحالية" optionalMark ltr value={current} onChange={(e) => edit(setCurrent)(e.target.value)} maxLength={200} error={error?.fieldError("currentValue")} />
          <TextField label="القيمة المتوقعة" requiredMark ltr value={proposed} onChange={(e) => edit(setProposed)(e.target.value)} maxLength={200} error={error?.fieldError("proposedValue")} />
        </div>
        <Textarea
          label="سبب التصحيح ومصدره"
          requiredMark
          value={note}
          onChange={(e) => edit(setNote)(e.target.value)}
          maxLength={1000}
          help="10 أحرف على الأقل. مثال: كشف الحساب المرفوع يوضح سداد قسط 2026-05 كاملاً."
          error={error?.fieldError("note")}
        />
        <p className={cn("m-0 flex items-center gap-1.5 text-13 text-muted")}>
          <Icon name="task_alt" size={16} />
          تُسند المهمة لموظف المالية خلال يومي عمل.
        </p>
      </div>
    </Dialog>
  );
}
