"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Checkbox, Textarea } from "@/components/ui/Field";
import { apiSend, isApiError } from "@/lib/api/client";

/** Confirm the prepared summary or ask for changes. Confirming does not publish: the team publishes after its checks. */
export function OwnerConfirm({ reference, termsId, versionNo }: { reference: string; termsId: string; versionNo: number }) {
  const router = useRouter();
  const [agree, setAgree] = useState(false);
  const [busy, setBusy] = useState<"confirm" | "changes" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [note, setNote] = useState("");

  const send = async (kind: "confirm" | "changes") => {
    setError(null);
    if (kind === "confirm" && !agree) {
      setError("ضع علامة على الإقرار قبل التأكيد.");
      return;
    }
    if (kind === "changes" && note.trim().length < 5) {
      setError("اكتب ما تريد تعديله.");
      return;
    }
    setBusy(kind);
    try {
      await apiSend("POST", `/market/sale-requests/${reference}/opportunity/${kind === "confirm" ? "confirm" : "request-changes"}`, { termsId, note: kind === "changes" ? note.trim() : null });
      setOpen(false);
      router.refresh();
    } catch (err) {
      setError(isApiError(err) && err.title ? err.title : "تعذّر الإرسال. حاول مرة أخرى.");
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-rust-200 bg-rust-50 p-4 md:p-5">
      <strong className="text-17">راجع الملخص (الإصدار {versionNo}) ثم أكده أو اطلب تعديله</strong>
      <p className="m-0 text-14 leading-6 text-charcoal">تأكيدك لا ينشر الفرصة مباشرة؛ ينشرها الفريق بعد استكمال شروط النشر. أي تغيير لاحق في السعر أو الرصيد أو شروط النقل يحتاج تأكيدك من جديد.</p>
      {error ? <Alert tone="err" compact>{error}</Alert> : null}
      <Checkbox checked={agree} onChange={(e) => setAgree(e.target.checked)} label="أؤكد صحة الملخص والأرقام وشروط النقل ودقة الموقع والصور كما تظهر هنا." />
      <div className="flex flex-col gap-2 sm:flex-row">
        <Button onClick={() => void send("confirm")} loading={busy === "confirm"}>تأكيد الملخص</Button>
        <Button variant="secondary" onClick={() => setOpen(true)}>طلب تعديل</Button>
      </div>
      <Dialog open={open} onClose={() => setOpen(false)} title="طلب تعديل الملخص"
        footer={<Button onClick={() => void send("changes")} loading={busy === "changes"}>إرسال للفريق</Button>}>
        <Textarea label="ما الذي تريد تعديله؟" value={note} onChange={(e) => setNote(e.target.value)} rows={4} maxLength={1000} />
      </Dialog>
    </div>
  );
}
