"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { IndividualFrame, useRequestCopy, useRequestSubmit } from "@/components/individual/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { AmountField, Select, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { apiSend, apiUpload } from "@/lib/api/client";
import type { MyRequestDetail } from "@/lib/api/requests";
import { formatDate, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

/**
 * E03: amount, transfer date, optional reference and installment, and the proof (uploaded as a request document of kind
 * payment_proof). The report stays «بانتظار تأكيد جهتك» until the lender's confirmation is recorded and verified.
 */
export function ReportPayment({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const P = c.execution.payment;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  const router = useRouter();
  const ref = encodeURIComponent(detail.reference);
  const schedule = detail.execution?.schedule ?? [];
  const input = useRef<HTMLInputElement>(null);
  const [amount, setAmount] = useState<number | null>(null);
  const [transferDate, setTransferDate] = useState("");
  const [bankReference, setBankReference] = useState("");
  const [installment, setInstallment] = useState("");
  const [proof, setProof] = useState<{ id: string; name: string } | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const upload = useRequestSubmit();
  const send = useRequestSubmit();

  const onFile = async (file: File | undefined) => {
    if (!file) return;
    const form = new FormData();
    form.append("file", file);
    form.append("kind", "payment_proof");
    const res = await upload.run(() => apiUpload<{ documentId: string }>(`/my/requests/${ref}/documents`, form));
    if (res.ok) setProof({ id: res.data.documentId, name: file.name });
    if (input.current) input.current.value = "";
  };

  const submit = async () => {
    if (!proof) {
      setFieldErrors({ proofDocumentId: P.proofRequired });
      return;
    }
    setFieldErrors({});
    const res = await send.run(
      (key) =>
        apiSend(
          "POST",
          `/my/requests/${ref}/payment-reports`,
          { amount, transferDate: transferDate || null, bankReference: bankReference.trim() || null, scheduleItemNo: installment ? Number(installment) : null, proofDocumentId: proof.id },
          { idempotencyKey: key },
        ),
      (e) => {
        const errors = (e as { errors?: Record<string, string[]> }).errors;
        if (errors) setFieldErrors(Object.fromEntries(Object.entries(errors).map(([k, v]) => [k, v[0] ?? ""])));
        return undefined;
      },
    );
    if (res.ok) {
      router.push(`/my/requests/${ref}/execution?reported=1`);
      router.refresh();
    }
  };

  return (
    <IndividualFrame title={P.title} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: `/my/requests/${ref}/execution` }}>
      <h1 className="m-0 text-24 leading-9 font-bold">{P.heading}</h1>
      <p className="m-0 text-16 leading-7 text-charcoal">{P.intro}</p>
      {detail.execution ? <p className="m-0 rounded-md bg-subtle p-3 text-15 leading-6">{detail.execution.noFundsNote}</p> : null}
      <AmountField label={P.amount} value={amount} onValueChange={setAmount} error={fieldErrors.amount} size="lg" />
      <TextField label={P.transferDate} type="date" ltr value={transferDate} onChange={(e) => setTransferDate(e.target.value)} error={fieldErrors.transferDate} />
      <TextField label={P.bankReference} optionalMark ltr maxLength={100} value={bankReference} onChange={(e) => setBankReference(e.target.value)} />
      {schedule.length > 0 ? (
        <Select
          label={P.installment}
          placeholder={P.installmentNone}
          value={installment}
          onChange={(e) => setInstallment(e.target.value)}
          options={schedule.map((s) => ({ value: String(s.no), label: `${c.execution.installmentNo(s.no)} · ${formatMoney(s.amount, fmt)} · ${formatDate(s.dueDate, fmt)}` }))}
          error={fieldErrors.scheduleItemNo}
        />
      ) : null}
      <div className="flex flex-col gap-2 rounded-[12px] border border-line bg-white px-3.5 py-3">
        <div className="flex items-center gap-3">
          <Icon name={proof ? "task" : "receipt_long"} size={24} className={proof ? "text-ok" : "text-muted"} />
          <div className="flex min-w-0 flex-1 flex-col">
            <strong className="text-15">{P.proof}</strong>
            <span className="truncate text-13 text-muted">{proof ? <>{P.uploaded} · <bdi dir="ltr">{proof.name}</bdi></> : P.proofHelp}</span>
          </div>
          <input ref={input} type="file" accept="application/pdf,image/jpeg,image/png" className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => void onFile(e.target.files?.[0])} />
          <Button variant="secondary" size="lg" loading={upload.busy} onClick={() => input.current?.click()} className="min-h-11 text-15">
            {P.uploadProof}
          </Button>
        </div>
        {fieldErrors.proofDocumentId || upload.error ? (
          <span role="alert" className="flex items-center gap-1 text-14 text-err">
            <Icon name="error" size={16} />
            {fieldErrors.proofDocumentId || upload.error}
          </span>
        ) : null}
      </div>
      {send.error && Object.keys(fieldErrors).length === 0 ? <Alert tone="err">{send.error}</Alert> : null}
      <Button size="xl" fullWidth loading={send.busy} onClick={() => void submit()}>
        {P.submit}
      </Button>
    </IndividualFrame>
  );
}
