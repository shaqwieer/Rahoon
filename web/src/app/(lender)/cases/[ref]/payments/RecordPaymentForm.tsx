"use client";

import { useEffect, useState } from "react";
import { Alert, AmountField, Button, DateField, Icon, Select, TextField, Textarea } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { InstallmentDto } from "@/lib/api/lender";
import { cn } from "@/lib/cn";
import { formatDate, formatMoney } from "@/lib/format";

type RefCheck = { ref: string; available: boolean | null; message: string };

/**
 * L20 «تسجيل دفعة يدوياً» (maker step). Used in the desktop drawer and the mobile full page.
 * Installment defaults to the earliest unpaid; amount defaults to the installment (a different amount reveals the
 * mandatory variance reason); the bank reference is checked for reuse as you type.
 */
export function RecordPaymentForm({ reference, installments, today, onDone, onCancel }: {
  reference: string;
  installments: InstallmentDto[];
  /** Riyadh business date from the server: receipt dates may not be in the future. */
  today: string;
  onDone: () => void;
  onCancel: () => void;
}) {
  const key = useIdempotencyKey();
  const first = installments[0];
  const [no, setNo] = useState(first ? String(first.no) : "");
  const [amount, setAmount] = useState<number | null>(first?.amount ?? null);
  const [amountTouched, setAmountTouched] = useState(false);
  const [variance, setVariance] = useState("");
  const [receivedOn, setReceivedOn] = useState(today);
  const [bankRef, setBankRef] = useState("");
  const [check, setCheck] = useState<RefCheck | null>(null);
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);

  const inst = installments.find((i) => String(i.no) === no) ?? null;
  const differs = inst !== null && amount !== null && Math.round(amount * 100) !== Math.round(inst.amount * 100);
  const norm = bankRef.trim().toUpperCase();
  const refLengthOk = norm.length >= 6 && norm.length <= 40;

  useEffect(() => {
    if (norm.length < 6 || norm.length > 40) return;
    const ctrl = new AbortController();
    const timer = window.setTimeout(() => {
      apiSend<{ available: boolean; message: string }>("GET", `/payments/check-reference?reference=${encodeURIComponent(norm)}`, undefined, { signal: ctrl.signal })
        .then((r) => setCheck({ ref: norm, available: r.available, message: r.message }))
        .catch((e: unknown) => {
          if (e instanceof DOMException && e.name === "AbortError") return;
          setCheck({ ref: norm, available: null, message: "تعذّر التحقق من المرجع الآن؛ سيُفحص عند التسجيل." });
        });
    }, 450);
    return () => {
      window.clearTimeout(timer);
      ctrl.abort();
    };
  }, [norm]);

  const refState = refLengthOk && check?.ref === norm ? check : null;
  const checking = refLengthOk && !refState;
  const changed = (field: string) => {
    key.reset();
    if (errors[field]) setErrors((e) => ({ ...e, [field]: "" }));
  };

  const submit = async () => {
    const local: Record<string, string> = {};
    if (!inst) local.installmentNo = "اختر القسط.";
    if (amount === null || amount <= 0) local.amount = "المبلغ مطلوب ويجب أن يكون أكبر من صفر.";
    if (differs && variance.trim().length < 3) local.varianceReason = "المبلغ يختلف عن القسط؛ اذكر السبب.";
    if (!receivedOn) local.receivedOn = "تاريخ الاستلام مطلوب.";
    else if (receivedOn > today) local.receivedOn = "تاريخ الاستلام لا يكون في المستقبل.";
    if (!refLengthOk) local.bankReference = "مرجع التحويل البنكي مطلوب.";
    else if (refState?.available === false) local.bankReference = refState.message;
    setErrors(local);
    setFormError(null);
    if (Object.values(local).some(Boolean) || !inst || amount === null) return;

    setBusy(true);
    try {
      await apiSend(
        "POST",
        `/cases/${reference}/payments`,
        { installmentNo: inst.no, amount, receivedOn, bankReference: norm, varianceReason: differs ? variance.trim() : null, proofDocumentVersionId: null },
        { idempotencyKey: key.get() },
      );
      key.reset();
      onDone();
    } catch (e) {
      if (!isApiError(e) || e.status !== 0) key.reset();
      if (isApiError(e) && e.errors) {
        setErrors(Object.fromEntries(Object.entries(e.errors).map(([k, v]) => [k, v[0] ?? ""])));
        setFormError(e.title || "راجع الحقول المظللة.");
      } else if (isApiError(e)) {
        setFormError(e.status === 0 ? "تعذّر الاتصال بالخادم. أعد المحاولة؛ لن تُسجَّل الدفعة مرتين." : e.title || "تعذّر تسجيل الدفعة.");
      } else setFormError("تعذّر تسجيل الدفعة.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <form
      noValidate
      className="flex min-h-full flex-col"
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
    >
      <div className="flex flex-1 flex-col gap-4 p-5">
        {formError ? <Alert tone="err" title={formError} /> : null}
        <Select
          label="القسط"
          value={no}
          onChange={(e) => {
            const next = installments.find((i) => String(i.no) === e.target.value);
            setNo(e.target.value);
            if (next && !amountTouched) setAmount(next.amount);
            changed("installmentNo");
          }}
          options={installments.map((i) => ({ value: String(i.no), label: `القسط ${i.no} · ${formatDate(i.dueDate)}${i.status === "Overdue" ? " · متأخر" : ""}` }))}
          error={errors.installmentNo || undefined}
          help={inst ? <>قيمة القسط <bdi dir="ltr">{formatMoney(inst.amount)}</bdi> ر.س</> : undefined}
        />
        <div className="grid gap-4 sm:grid-cols-2">
          <AmountField
            label="المبلغ"
            requiredMark
            value={amount}
            onValueChange={(v) => {
              setAmount(v);
              setAmountTouched(true);
              changed("amount");
            }}
            error={errors.amount || undefined}
          />
          <DateField
            label="تاريخ الاستلام"
            requiredMark
            value={receivedOn}
            max={today}
            onValueChange={(v) => {
              setReceivedOn(v);
              changed("receivedOn");
            }}
            error={errors.receivedOn || undefined}
          />
        </div>
        {differs ? (
          <Textarea
            label="سبب اختلاف المبلغ عن القسط"
            requiredMark
            value={variance}
            onChange={(e) => {
              setVariance(e.target.value);
              changed("varianceReason");
            }}
            maxLength={500}
            error={errors.varianceReason || undefined}
            help="مثال: سداد جزئي متفق عليه، أو دفعة تغطي رسوماً إضافية."
          />
        ) : null}
        <div className="flex flex-col gap-1.5">
          <TextField
            label="مرجع التحويل البنكي"
            requiredMark
            ltr
            mono
            autoComplete="off"
            spellCheck={false}
            value={bankRef}
            onChange={(e) => {
              setBankRef(e.target.value);
              changed("bankReference");
            }}
            maxLength={40}
            error={errors.bankReference || (refState?.available === false ? refState.message : undefined)}
          />
          <span aria-live="polite" className={cn("flex min-h-5 items-center gap-1.5 text-13", refState?.available ? "text-ok" : "text-muted")}>
            {checking ? (
              <>
                <Icon name="progress_activity" size={16} className="animate-rh-spin" />
                جارٍ التحقق من المرجع…
              </>
            ) : refState?.available ? (
              <>
                <Icon name="check_circle" size={16} />
                {refState.message}
              </>
            ) : refState?.available === null ? (
              refState.message
            ) : null}
          </span>
        </div>
        <div role="note" className="flex items-start gap-2.5 rounded-md border border-info-line bg-info-bg p-3 text-13 leading-5">
          <Icon name="rule" size={20} className="text-info" />
          <span>
            تُحفظ الدفعة بحالة <strong>مسجلة — بانتظار المطابقة</strong>، ويطابقها موظف مالية آخر. لا تظهر للمالك كمطابقة قبل ذلك.
          </span>
        </div>
      </div>
      <div className="sticky bottom-0 flex gap-2.5 border-t border-divider bg-white px-5 py-3.5">
        <Button variant="secondary" onClick={onCancel}>
          إلغاء
        </Button>
        <Button type="submit" loading={busy} disabled={checking || refState?.available === false} className="flex-1 sm:flex-none">
          تسجيل وإرسال للمطابقة
        </Button>
      </div>
    </form>
  );
}
