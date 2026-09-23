"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import type { OwnerInstallment } from "@/components/owner/types";
import { TONE_TEXT, toTone } from "@/components/owner/ui";
import { useSubmit } from "@/components/owner/useSubmit";
import { ServerText, useOwnerCopy } from "@/components/owner/values";
import { Alert, AmountField, Button, DateField, DateText, Dialog, TextField } from "@/components/ui";
import { apiSend } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";

/** «كيف أدفع؟» — the bank-transfer steps in a dialog (payments never go through Rahoon). */
export function HowToPay({ title, steps, note }: { title: string; steps: string[]; note: string }) {
  const c = useOwnerCopy();
  const { locale } = useI18n();
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button variant="text" size="lg" className="self-start px-0 text-15" onClick={() => setOpen(true)}>
        {c.payments.howToPay}
      </Button>
      <Dialog open={open} onClose={() => setOpen(false)} title={locale === "en" ? c.payments.howToPay : title} size="sm">
        <div className="flex flex-col gap-3 p-5">
          <ol className="m-0 flex flex-col gap-2 ps-5 text-16 leading-[26px]">
            {steps.map((s, i) => (
              <li key={i}>
                <ServerText text={s} />
              </li>
            ))}
          </ol>
          <Alert tone="info" role="none" compact>
            <ServerText text={note} />
          </Alert>
        </div>
      </Dialog>
    </>
  );
}

/** D10 installment rows with status tones; «أرسلت التحويل» opens the payment-notice form for that installment. */
export function InstallmentList({ items, today }: { items: OwnerInstallment[]; today: string }) {
  const c = useOwnerCopy();
  const P = c.payments;
  const [notice, setNotice] = useState<OwnerInstallment | null>(null);
  const [announce, setAnnounce] = useState<string | null>(null);
  return (
    <>
      <div role="status" aria-live="polite">
        {announce ? (
          <Alert tone="ok" role="none">
            <ServerText text={announce} />
          </Alert>
        ) : null}
      </div>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {items.map((i) => {
          const tone = toTone(i.tone);
          return (
            <li key={i.no} className="flex flex-col gap-1 rounded-[12px] border border-line bg-white px-4 py-3.5">
              <div className="flex items-baseline justify-between gap-2">
                <strong className="text-16">
                  {P.installment} <bdi>{i.no}</bdi> · <DateText value={i.dueDate} />
                </strong>
                <ServerText text={i.status} className={cn("text-14 font-semibold", TONE_TEXT[tone])} />
              </div>
              {i.meta && i.meta !== "—" ? <ServerText text={i.meta} className="text-14 text-muted" /> : null}
              {i.canNotify ? (
                <Button variant="secondary" size="lg" icon="outgoing_mail" className="mt-1 self-start" onClick={() => setNotice(i)}>
                  {P.notify}
                </Button>
              ) : null}
            </li>
          );
        })}
      </ul>
      {notice ? (
        <NoticeDialog
          key={notice.no}
          installment={notice}
          today={today}
          onClose={() => setNotice(null)}
          onSent={(m) => {
            setNotice(null);
            setAnnounce(m);
          }}
        />
      ) : null}
    </>
  );
}

function NoticeDialog({ installment, today, onClose, onSent }: { installment: OwnerInstallment; today: string; onClose: () => void; onSent: (message: string) => void }) {
  const c = useOwnerCopy();
  const P = c.payments;
  const router = useRouter();
  const [date, setDate] = useState(today);
  const [amount, setAmount] = useState<number | null>(installment.amount);
  const [reference, setReference] = useState("");
  const [fieldErrors, setFieldErrors] = useState<{ date?: string; amount?: string }>({});
  const { run, busy, error, resetKey } = useSubmit();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs = { date: date ? undefined : P.dateRequired, amount: amount && amount > 0 ? undefined : P.amountRequired };
    setFieldErrors(errs);
    if (errs.date || errs.amount) return;
    const res = await run((k) =>
      apiSend<{ message: string }>("POST", `/owner/payments/${installment.no}/notice`, { transferDate: date, amount, reference: reference.trim() || null }, { idempotencyKey: k }),
    );
    if (res.ok) {
      onSent(res.data.message);
      router.refresh();
    }
  };

  return (
    <Dialog
      open
      onClose={onClose}
      title={P.notifyTitle(installment.no)}
      footer={
        <>
          <Button type="submit" form="notice-form" size="lg" loading={busy} loadingLabel={c.sending}>
            {P.send}
          </Button>
          <Button size="lg" variant="secondary" onClick={onClose}>
            {c.back}
          </Button>
        </>
      }
    >
      <form id="notice-form" noValidate onSubmit={submit} className="flex flex-col gap-4 p-5">
        <p className="m-0 text-16 leading-[26px]">{P.notifyIntro}</p>
        <DateField
          label={P.transferDate}
          size="xl"
          value={date}
          max={today}
          error={fieldErrors.date}
          onValueChange={(v) => {
            setDate(v);
            resetKey();
          }}
        />
        <AmountField
          label={P.amount}
          size="xl"
          value={amount}
          error={fieldErrors.amount}
          onValueChange={(v) => {
            setAmount(v);
            resetKey();
          }}
        />
        <TextField
          label={P.reference}
          optionalMark
          help={P.referenceHelp}
          size="xl"
          ltr
          mono
          autoComplete="off"
          value={reference}
          onChange={(e) => {
            setReference(e.target.value);
            resetKey();
          }}
        />
        <div aria-live="assertive">{error ? <Alert tone="err" role="none">{error}</Alert> : null}</div>
      </form>
    </Dialog>
  );
}
