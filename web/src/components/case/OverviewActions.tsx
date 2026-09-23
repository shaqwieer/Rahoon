"use client";


import { useEffect, useState } from "react";
import { Alert, Button, Dialog, Icon, IconButton, NextActionCard, Textarea, useToast } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { AvailableAction, NextActionDto, WorkspaceData } from "@/lib/api/lender";
import { TransitionDialog } from "./TransitionDialog";

/** C04 next-action card bound to the API's next-action (available / blocked-with-reasons / not-yours). */
export function NextActionPanel({ reference, status, na, actions }: { reference: string; status: string; na: NextActionDto; actions: AvailableAction[] }) {
  const toast = useToast();
  const key = useIdempotencyKey();
  const [dialog, setDialog] = useState<AvailableAction | null>(null);
  const [busy, setBusy] = useState(false);
  const p = na.primary;
  const blocked = p !== null && !p.enabled;
  const state = !na.forYou ? "not-yours" : blocked ? "blocked" : "available";
  const slaTone = na.dueTone === "err" ? "err" : na.dueTone === "warn" ? "warn" : na.dueTone === "ok" ? "ok" : "info";

  const runSecondary = async () => {
    if (na.secondary?.action !== "remind_approver") return;
    setBusy(true);
    try {
      const inbox = await apiSend<{ items: Array<{ id: string; caseRef: string }> }>("GET", "/approvals").catch(() => null);
      const id = inbox?.items.find((i) => i.caseRef === reference)?.id;
      if (!id) throw new Error();
      await apiSend("POST", `/approvals/${id}/remind`, {}, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: "أُرسل تذكير للمعتمد." });
    } catch (e) {
      key.reset();
      toast.toast({ tone: "err", message: isApiError(e) ? e.title : "تعذّر إرسال التذكير." });
    } finally {
      setBusy(false);
    }
  };

  const primaryAction = p
    ? p.href
      ? { label: p.label, href: p.href }
      : p.action
        ? { label: p.label, onClick: () => setDialog(actions.find((a) => a.key === p.action) ?? null) }
        : undefined
    : undefined;

  return (
    <>
      <NextActionCard
        state={state}
        eyebrow={na.eyebrow}
        title={na.title}
        sla={na.dueText ? { tone: slaTone, text: na.dueText } : undefined}
        checklist={na.checks.length > 0 && !blocked ? na.checks.map((c) => ({ label: c.text, meta: c.ok ? undefined : "غير مستوفى" })) : undefined}
        blocked={blocked ? { title: "غير مؤهل بعد", reasons: (p?.disabledReason ?? "").split(" · ").filter(Boolean) } : undefined}
        after={na.body ?? undefined}
        action={state !== "not-yours" ? primaryAction : undefined}
        owner={state === "not-yours" && na.assigneeName ? { name: na.assigneeName, initials: initials(na.assigneeName), detail: na.assigneeRole ?? undefined } : undefined}
        secondaryAction={na.secondary ? (na.secondary.href ? { label: na.secondary.label, href: na.secondary.href } : { label: busy ? "جارٍ الإرسال…" : na.secondary.label, onClick: () => void runSecondary() }) : undefined}
      />
      <TransitionDialog reference={reference} action={dialog} expectedStatus={status} open={dialog !== null} onClose={() => setDialog(null)} />
    </>
  );
}

function initials(name: string) {
  const parts = name.split(" ").filter(Boolean);
  return parts.length >= 2 ? `${parts[0][0]} ${parts[parts.length - 1][0]}` : name.slice(0, 1);
}

/** Masked ID with an audited 60-second reveal that requires a reason (A-10). */
export function RevealParty({ reference, partyId, masked, label }: { reference: string; partyId: string; masked: string; label: string }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [value, setValue] = useState<{ nationalId: string | null; phone: string | null; expiresAt: string } | null>(null);
  const [left, setLeft] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!value) return;
    const tick = () => {
      const s = Math.max(0, Math.round((new Date(value.expiresAt).getTime() - Date.now()) / 1000));
      setLeft(s);
      if (s === 0) setValue(null);
    };
    tick();
    const t = setInterval(tick, 1000);
    return () => clearInterval(t);
  }, [value]);

  const reveal = async () => {
    setBusy(true);
    setError(null);
    try {
      setValue(await apiSend("POST", `/cases/${reference}/parties/${partyId}/reveal`, { reason }));
      setOpen(false);
      setReason("");
    } catch (e) {
      setError(isApiError(e) ? e.title : "تعذّر الكشف.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <span className="inline-flex items-center gap-1">
        {label} <bdi dir="ltr" className="font-mono">{value?.nationalId ?? masked}</bdi>
        {value ? <span className="text-12 text-warn">· يُخفى بعد {left} ث</span> : null}
      </span>
      {!value ? <IconButton icon="visibility" label="كشف الهوية كاملة (يتطلب سبباً ويُسجَّل)" size={36} onClick={() => setOpen(true)} /> : null}
      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        size="sm"
        title="كشف البيانات الشخصية"
        description="يُعرض رقم الهوية والجوال كاملين لمدة 60 ثانية، ويُسجَّل الكشف وسببه في سجل الحالة."
        footer={<Button onClick={() => void reveal()} loading={busy} disabled={reason.trim().length < 5}>كشف لمدة 60 ثانية</Button>}
      >
        <div className="flex flex-col gap-3 p-5">
          {error ? <Alert tone="err" title={error} /> : null}
          <Textarea label="سبب الكشف" requiredMark value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} placeholder="مثال: مطابقة مع الصك" />
        </div>
      </Dialog>
    </>
  );
}

/** «إجراءات حساسة» panel: pause/resume via review dialog; voluntary sale disabled with reason; referral hidden for non-legal roles. */
export function SensitivePanel({ ws }: { ws: WorkspaceData }) {
  const [dialog, setDialog] = useState<AvailableAction | null>(null);
  const s = ws.sensitive;
  return (
    <section aria-labelledby="sens-h" className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4">
      <h2 id="sens-h" className="m-0 text-16 font-semibold">إجراءات حساسة</h2>
      {s.actions.map((a) => (
        <Button key={a.key} variant="secondary" icon={a.key === "pause" ? "pause_circle" : "play_circle"} review={a.requiresReason} onClick={() => setDialog(a)} className="justify-start">
          {a.key === "pause" ? "إيقاف الحالة مؤقتاً" : a.labelAr}
        </Button>
      ))}
      {s.canRequestCancel ? (
        <Button variant="sensitive" icon="cancel" review href={`/cases/${ws.header.reference}/cancel`} className="justify-start">إلغاء الحالة</Button>
      ) : null}
      {s.sale ? (
        <div className="flex flex-col gap-1">
          <Button variant="secondary" icon="sell" disabled={!s.sale.enabled} href={s.sale.enabled ? s.sale.href : undefined} aria-describedby="vs-why" className="justify-start">
            {s.sale.label}
          </Button>
          {!s.sale.enabled ? <span id="vs-why" className="text-12 text-muted">معطّل: {s.sale.reasons.join(" · ")}</span> : null}
        </div>
      ) : null}
      {s.canInitiateReferral ? (
        <Button variant="secondary" icon="outbound" href={`/cases/${ws.header.reference}/referral`} className="justify-start">تقييم جاهزية الإحالة القضائية</Button>
      ) : s.referralNote ? (
        <p className="m-0 flex gap-1.5 text-12 text-muted"><Icon name="info" size={16} />{s.referralNote}</p>
      ) : null}
      <TransitionDialog reference={ws.header.reference} action={dialog} expectedStatus={ws.header.status} open={dialog !== null} onClose={() => setDialog(null)} />
    </section>
  );
}
