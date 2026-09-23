"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { BidiText } from "@/components/case/BidiText";
import { ReasonDialog } from "@/components/case/ReasonDialog";
import { Alert, Button, Dialog, Icon, RadioCardGroup, TextField, useToast } from "@/components/ui";
import type { AlertTone } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { BreachData } from "@/lib/api/lender";
import { cn } from "@/lib/cn";
import { formatDate } from "@/lib/format";

type Review = BreachData["reviews"][number];

const CLOSED_LOOK: Record<string, { tone: AlertTone; title: string }> = {
  Cured: { tone: "ok", title: "صُحح التأخر وعادت التسوية طبيعية" },
  Restructuring: { tone: "info", title: "أُغلقت المراجعة بمسار «إعادة هيكلة»" },
  OtherOptions: { tone: "info", title: "أُغلقت المراجعة بمسار «تقييم خيارات أخرى»" },
  Closed: { tone: "neutral", title: "أُغلقت مراجعة الإخلال" },
};

const TONE_CLS: Record<string, string> = { err: "text-err", warn: "text-warn", ok: "text-ok", info: "text-info" };

const OUTCOMES = [
  { value: "restructuring", label: "إعادة هيكلة", description: "تعود الحالة إلى «حل مقترح» لإعداد حل جديد يمر بالمراجعة والاعتماد." },
  { value: "other_options", label: "تقييم خيارات أخرى", description: "بيع طوعي بموافقة المالك، أو تقييم إحالة منفصل بقرار مستقل." },
  { value: "closed", label: "إغلاق المراجعة", description: "تُغلق المراجعة وتستمر التسوية دون تغيير." },
];

function missedTitle(missed: number[]): string {
  const list = missed.join(" و");
  return missed.length === 2 ? `لم يُسدَّد قسطان متتاليان (${list})` : missed.length > 2 ? `لم تُسدَّد ${missed.length} أقساط متتالية (${list})` : `لم يُسدَّد القسط ${list}`;
}

export function BreachView({ reference, d, review, canDecide, canContact }: { reference: string; d: BreachData; review: Review; canDecide: boolean; canContact: boolean }) {
  const router = useRouter();
  const toast = useToast();
  const [dialog, setDialog] = useState<"call" | "help" | "outcome" | null>(null);
  const [outcome, setOutcome] = useState("restructuring");
  const base = `/cases/${reference}`;
  const open = review.status === "Open";
  const openedOn = formatDate(review.triggeredAt);
  const cureDays = Math.round((Date.parse(review.cureDeadline) - Date.parse(openedOn)) / 86_400_000);
  const closed = CLOSED_LOOK[review.status] ?? CLOSED_LOOK.Closed;

  return (
    <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_380px]">
      <h2 className="sr-only">مراجعة الإخلال</h2>
      {open ? (
        <Alert tone="warn" icon="warning" role="alert" title={missedTitle(review.missed)} className="xl:col-start-1">
          حسب شرط الاتفاق، فُتحت مراجعة إخلال ومُنح المالك <bdi dir="ltr">{cureDays}</bdi> يوماً للتصحيح منذ <bdi dir="ltr">{openedOn}</bdi>. الحالة العامة تبقى «تسوية نشطة» أثناء المراجعة.
        </Alert>
      ) : (
        <Alert tone={closed.tone} role="status" title={closed.title} className="xl:col-start-1">
          {review.outcomeNote ? <BidiText text={review.outcomeNote} /> : null}
        </Alert>
      )}

      {/* Aside comes second in the DOM so the 390 layout reads: alert → next action → timeline. */}
      <aside className="flex flex-col gap-4 xl:col-start-2 xl:row-span-2 xl:row-start-1" aria-label="الإجراء التالي">
        {open ? (
          <section aria-labelledby="bna-h" className="flex flex-col gap-2.5 rounded-lg border border-t-[3px] border-line border-t-orange bg-white p-5">
            <span className="text-12 font-semibold text-muted">الإجراء التالي</span>
            <h3 id="bna-h" className="m-0 text-18 leading-7 font-bold">التواصل مع المالك قبل انتهاء المهلة</h3>
            <p className="m-0 text-14 leading-[22px] text-charcoal">
              {d.contactHours ? <>جرّب مكالمة في الوقت المفضل (<BidiText text={d.contactHours} />).</> : "ابدأ بالمساعدة لا بالعواقب: جرّب مكالمة في الوقت المفضل للمالك."}
              {" "}مهلة التصحيح حتى <bdi dir="ltr">{formatDate(review.cureDeadline)}</bdi>.
            </p>
            {canContact ? (
              <>
                <Button size="lg" fullWidth className="min-h-11 text-15" icon="call" onClick={() => setDialog("call")}>جدولة مكالمة</Button>
                <Button size="lg" variant="secondary" fullWidth className="min-h-11 text-15" onClick={() => setDialog("help")}>إرسال رسالة «هل تحتاج مساعدة؟»</Button>
              </>
            ) : (
              <p className="m-0 text-13 text-muted">التواصل مع المالك من صلاحيات فريق الحالة.</p>
            )}
          </section>
        ) : null}

        {open && canDecide ? (
          <section aria-labelledby="out-h" className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4">
            <h3 id="out-h" className="m-0 text-15 font-bold">قرار المسار بعد المهلة</h3>
            <p className="m-0 text-13 leading-5 text-muted">التصحيح يُغلق المراجعة تلقائياً بعد مطابقة المتأخر. غير ذلك يتطلب قراراً مسبباً.</p>
            <Button variant="secondary" review className="self-start" onClick={() => setDialog("outcome")}>تسجيل قرار المسار</Button>
          </section>
        ) : null}

        <div role="note" className="flex items-start gap-2.5 rounded-md border border-info-line bg-info-bg p-3.5 text-13 leading-5">
          <Icon name="info" size={20} className="text-info" />
          <span>{d.note}</span>
        </div>
      </aside>

      <div className="flex min-w-0 flex-col gap-5 xl:col-start-1">
        <section aria-labelledby="events-h" className="flex flex-col gap-1 rounded-lg border border-line bg-white p-5">
          <h3 id="events-h" className="m-0 mb-2 text-16 font-bold">ما حدث</h3>
          {d.timeline.length === 0 ? (
            <p className="m-0 text-14 text-muted">لا أحداث مسجلة بعد.</p>
          ) : (
            <ol className="m-0 flex list-none flex-col p-0">
              {d.timeline.map((t, i) => (
                <li key={i} className="grid grid-cols-[24px_96px_minmax(0,1fr)] items-start gap-3 border-t border-divider py-2.5 text-14 first:border-t-0 md:grid-cols-[24px_140px_minmax(0,1fr)]">
                  <Icon name={t.icon} size={20} className={TONE_CLS[t.tone] ?? "text-muted"} />
                  <bdi dir="ltr" className="text-muted tabular-nums">{t.date}</bdi>
                  <BidiText text={t.text} />
                </li>
              ))}
            </ol>
          )}
        </section>

        <section aria-labelledby="paths-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <h3 id="paths-h" className="m-0 text-16 font-bold">المسارات المتاحة بعد المهلة</h3>
          <ul className="m-0 grid list-none gap-3 p-0 md:grid-cols-3">
            {d.paths.map((p) => (
              <li key={p.key} className={cn("flex flex-col gap-1.5 rounded-md border p-4 text-14", p.key === "cure" ? "border-ok-line bg-ok-bg" : "border-line bg-white")}>
                <strong className="text-15">{p.title}</strong>
                <span className="leading-[22px]">{p.description}</span>
                <span className="text-13 text-muted">{p.owner}</span>
              </li>
            ))}
          </ul>
        </section>
      </div>

      <AppointmentDialog
        open={dialog === "call"}
        onClose={() => setDialog(null)}
        reference={reference}
        onDone={() => {
          toast.toast({ tone: "ok", message: "اقتُرح موعد المكالمة وأُبلغ المالك." });
          router.refresh();
        }}
      />
      <ReasonDialog
        open={dialog === "help"}
        onClose={() => setDialog(null)}
        title="إرسال رسالة «هل تحتاج مساعدة؟»"
        description="تصل للمالك عبر البوابة. ابدأ بالمساعدة لا بالعواقب."
        label="نص الرسالة"
        initialText="هل تحتاج مساعدة؟"
        maxLength={4000}
        confirmLabel="إرسال الرسالة"
        onSubmit={async (body, key) => {
          await apiSend("POST", `${base}/comms/messages`, { body, internal: false, channel: "portal" }, { idempotencyKey: key });
          toast.toast({ tone: "ok", message: "أُرسلت الرسالة للمالك." });
          router.refresh();
        }}
      />
      <ReasonDialog
        open={dialog === "outcome"}
        onClose={() => setDialog(null)}
        title="قرار مسار مراجعة الإخلال…"
        description="القرار يُسجَّل مع مبرره في سجل الحالة. لا يوجد انتقال تلقائي إلى الإحالة."
        label="مبرر المسار"
        confirmLabel="تأكيد القرار"
        onSubmit={async (note, key) => {
          const r = await apiSend<{ caseStatus: string }>("POST", `${base}/breach/${review.id}/outcome`, { outcome, note }, { idempotencyKey: key });
          toast.toast({ tone: "ok", message: r.caseStatus === "proposed_solution" ? "سُجّل القرار · الحالة الآن «حل مقترح» لإعداد حل جديد." : "سُجّل قرار المسار." });
          router.refresh();
        }}
      >
        <RadioCardGroup legend="المسار" name="breach-outcome" columns={1} value={outcome} onChange={setOutcome} options={OUTCOMES} />
      </ReasonDialog>
    </div>
  );
}

function AppointmentDialog({ open, onClose, reference, onDone }: { open: boolean; onClose: () => void; reference: string; onDone: () => void }) {
  const key = useIdempotencyKey();
  const [at, setAt] = useState("");
  const [attendees, setAttendees] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setError(null);
    // datetime-local has no offset; business time is Riyadh (UTC+3, no DST).
    const startsAt = at ? `${at.length === 16 ? `${at}:00` : at}+03:00` : "";
    if (!startsAt || Number.isNaN(Date.parse(startsAt))) return setError("اختر تاريخ الموعد ووقته.");
    if (Date.parse(startsAt) <= Date.now()) return setError("الموعد يجب أن يكون في المستقبل.");
    setBusy(true);
    try {
      await apiSend("POST", `/cases/${reference}/comms/appointments`, { type: "call", startsAt, attendees: attendees.trim() || null }, { idempotencyKey: key.get() });
      key.reset();
      setAt("");
      setAttendees("");
      onClose();
      onDone();
    } catch (e) {
      if (!isApiError(e) || e.status !== 0) key.reset();
      setError(isApiError(e) ? (e.status === 0 ? "تعذّر الاتصال بالخادم. أعد المحاولة." : e.fieldError("startsAt") ?? e.title) : "تعذّر اقتراح الموعد.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="جدولة مكالمة مع المالك"
      description="يُقترح الموعد على المالك في البوابة، ويستطيع قبوله أو طلب وقت آخر. الوقت بتوقيت الرياض."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void submit()} loading={busy} disabled={!at}>اقتراح الموعد</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {error ? <Alert tone="err" title={error} /> : null}
        <TextField
          label="تاريخ المكالمة ووقتها"
          requiredMark
          type="datetime-local"
          ltr
          value={at}
          onChange={(e) => {
            setAt(e.target.value);
            key.reset();
          }}
        />
        <TextField
          label="الحضور"
          optionalMark
          value={attendees}
          onChange={(e) => {
            setAttendees(e.target.value);
            key.reset();
          }}
          maxLength={200}
          help="مثال: مدير الحالة والمالك."
        />
      </div>
    </Dialog>
  );
}
