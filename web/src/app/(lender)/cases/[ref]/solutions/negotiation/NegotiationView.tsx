"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { BidiText } from "@/components/case/BidiText";
import { ReasonDialog } from "@/components/case/ReasonDialog";
import { Alert, Button, EmptyState, Icon, Tag, Textarea, useToast } from "@/components/ui";
import type { Tone } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { NegotiationData, NegotiationEntryDto } from "@/lib/api/lender";
import { cn } from "@/lib/cn";
import { formatDate, formatDateTime } from "@/lib/format";

const OFFER_STATUS: Record<string, { label: string; tone: Tone }> = {
  Sent: { label: "مُرسل · بانتظار رد المالك", tone: "info" },
  Countered: { label: "طلب المالك تعديلاً", tone: "warn" },
  Accepted: { label: "قبله المالك", tone: "ok" },
  Declined: { label: "اعتذر عنه المالك", tone: "neutral" },
  Expired: { label: "انتهت صلاحيته", tone: "neutral" },
  Withdrawn: { label: "سُحب", tone: "neutral" },
};

const ENTRY_ICON: Record<string, string> = {
  Offer: "send",
  OwnerCounter: "person",
  OwnerMessage: "chat",
  OwnerAccept: "task_alt",
  OwnerDecline: "do_not_disturb_on",
  InternalNote: "forum",
  LenderClarification: "reply",
  LenderDecline: "undo",
};

type DialogKind = "clarify" | "decline" | { approve: boolean; id: string; title: string } | null;

export function NegotiationView({ reference, data, canManage, canPrepare }: { reference: string; data: NegotiationData; canManage: boolean; canPrepare: boolean }) {
  const router = useRouter();
  const toast = useToast();
  const [dialog, setDialog] = useState<DialogKind>(null);
  const base = `/cases/${reference}`;
  const offer = data.offer;
  const nextVersion = offer ? `v${offer.version + 1}` : "إصداراً جديداً";
  const notNegotiating = "متاح فقط أثناء مرحلة «تفاوض».";

  const post = async (path: string, body: unknown, key: string) => {
    await apiSend("POST", `${base}${path}`, body, { idempotencyKey: key });
  };

  return (
    <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_400px]">
      <section aria-labelledby="thread-h" className="flex min-w-0 flex-col gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <h2 id="thread-h" className="m-0 flex-1 text-20 font-bold">سلسلة العروض</h2>
          {offer ? (
            <span className="flex flex-wrap items-center gap-2 text-13 text-muted">
              <span>
                العرض <bdi dir="ltr" className="font-mono font-semibold text-ink">v{offer.version}</bdi> · صالح حتى{" "}
                <bdi dir="ltr">{formatDate(offer.validUntil)}</bdi>
              </span>
              <Tag tone={OFFER_STATUS[offer.status]?.tone ?? "neutral"}>{OFFER_STATUS[offer.status]?.label ?? offer.status}</Tag>
              {offer.status === "Accepted" ? <Link href={`${base}/agreement`} className="font-semibold">الاتفاق</Link> : null}
            </span>
          ) : null}
        </div>

        {data.thread.length === 0 ? (
          <EmptyState icon="forum" headingLevel={3} title="لا توجد عروض أو ردود بعد" body="تظهر هنا العروض الرسمية وطلبات المالك والملاحظات الداخلية بترتيبها الزمني بعد اعتماد أول عرض." />
        ) : (
          <ol className="m-0 flex list-none flex-col gap-3 p-0" aria-label="سلسلة العروض بالترتيب الزمني">
            {data.thread.map((e) => (
              <li key={e.id}>
                <ThreadEntry e={e} />
              </li>
            ))}
          </ol>
        )}

        {canManage ? <InternalNoteComposer reference={reference} onSaved={() => router.refresh()} /> : null}
      </section>

      <aside className="flex flex-col gap-4" aria-label="المقارنة والإجراء التالي">
        <details open className="group overflow-hidden rounded-lg border border-line bg-white">
          <summary className="flex min-h-12 cursor-pointer list-none items-center gap-2 px-4 text-15 font-bold [&::-webkit-details-marker]:hidden">
            <span className="flex-1">
              {data.comparison ? <>المقابل مقارنة بـ <bdi dir="ltr">{data.comparison.version}</bdi></> : "المقابل مقارنة بالعرض"}
            </span>
            <Icon name="expand_more" size={22} className="text-muted transition-transform group-open:rotate-180" />
          </summary>
          {data.comparison ? (
            <table className="w-full border-collapse text-14">
              <caption className="sr-only">طلب المالك مقارنة بالعرض {data.comparison.version}؛ القيم المتغيرة مميزة بلون ونص.</caption>
              <thead>
                <tr className="bg-warm text-13 text-muted">
                  <th scope="col" className="px-4 py-2 text-start font-semibold">البند</th>
                  <th scope="col" className="px-3 py-2 text-start font-semibold"><bdi dir="ltr">{data.comparison.version}</bdi></th>
                  <th scope="col" className="px-3 py-2 text-start font-semibold">طلب المالك</th>
                </tr>
              </thead>
              <tbody>
                {data.comparison.rows.map((r) => (
                  <tr key={r.item} className="border-t border-divider">
                    <th scope="row" className="px-4 py-2.5 text-start font-medium text-muted">{r.item}</th>
                    <td className="px-3 py-2.5 tabular-nums"><bdi dir="ltr">{r.offered}</bdi></td>
                    <td className={cn("px-3 py-2.5 tabular-nums", r.changed && "font-bold text-rust-700")}>
                      <bdi dir="ltr">{r.requested}</bdi>
                      {r.changed ? <span className="ms-1 text-12 font-semibold">(تغيّر)</span> : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="m-0 border-t border-divider px-4 py-3 text-14 text-muted">لم يقدّم المالك طلباً مقابلاً على العرض الحالي.</p>
          )}
        </details>

        {canManage ? (
          <section aria-labelledby="na-h" className="flex flex-col gap-2.5 rounded-lg border border-t-[3px] border-line border-t-orange bg-white p-5">
            <h3 id="na-h" className="m-0 text-16 font-bold">الإجراء التالي</h3>
            {!data.canAct ? (
              <p id="na-blocked" role="note" className="m-0 rounded-sm bg-warm p-3 text-13 text-muted">
                {notNegotiating}
              </p>
            ) : null}
            {canPrepare ? (
              <Button href={data.canAct ? `${base}/solutions/new` : undefined} softDisabled={!data.canAct} aria-describedby={!data.canAct ? "na-blocked" : undefined} fullWidth>
                إعداد {nextVersion} بناءً على الطلب
              </Button>
            ) : null}
            <Button variant="secondary" fullWidth softDisabled={!data.canAct} aria-describedby={!data.canAct ? "na-blocked" : undefined} onClick={() => setDialog("clarify")}>
              الرد بتوضيح دون تغيير
            </Button>
            <Button variant="secondary" fullWidth review softDisabled={!data.canAct} aria-describedby={!data.canAct ? "na-blocked" : undefined} onClick={() => setDialog("decline")}>
              الاعتذار عن الطلب مع السبب
            </Button>
            <p className="m-0 text-13 leading-5 text-muted">{data.note}</p>
          </section>
        ) : (
          <Alert tone="neutral" role="none" compact>{data.note}</Alert>
        )}

        {data.extensions.length > 0 ? (
          <section aria-labelledby="ext-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
            <h3 id="ext-h" className="m-0 text-15 font-bold">طلبات تمديد مهلة العرض</h3>
            <ul className="m-0 flex list-none flex-col gap-3 p-0">
              {data.extensions.map((x) => (
                <li key={x.id} className="flex flex-col gap-2 rounded-md bg-warm p-3 text-14">
                  <span className="font-semibold">{x.title}</span>
                  <span className="text-13 text-muted">
                    تمديد <bdi dir="ltr">{x.subjectVersionNo}</bdi> أيام · بانتظار قرار مدير الحالة
                  </span>
                  {x.mine && canManage ? (
                    <span className="flex flex-wrap gap-2">
                      <Button size="sm" onClick={() => setDialog({ approve: true, id: x.id, title: x.title })}>اعتماد التمديد</Button>
                      <Button size="sm" variant="secondary" review onClick={() => setDialog({ approve: false, id: x.id, title: x.title })}>رفض التمديد</Button>
                    </span>
                  ) : (
                    <span className="text-12 text-muted">مسند لمدير الحالة؛ لا يعتمد مقدم الطلب طلبه.</span>
                  )}
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </aside>

      <ReasonDialog
        open={dialog === "clarify"}
        onClose={() => setDialog(null)}
        title="الرد بتوضيح دون تغيير"
        description="تُرسل رسالة للمالك عبر البوابة دون تغيير شروط العرض، ويبقى العرض مفتوحاً حتى نهاية صلاحيته."
        label="نص التوضيح للمالك"
        placeholder="اكتب بلغة واضحة وداعمة، دون مصطلحات أو تهديد."
        maxLength={2000}
        confirmLabel="إرسال التوضيح"
        onSubmit={async (text, key) => {
          await post("/negotiation/notes", { body: text, internal: false }, key);
          toast.toast({ tone: "ok", message: "أُرسل التوضيح للمالك." });
          router.refresh();
        }}
      />
      <ReasonDialog
        open={dialog === "decline"}
        onClose={() => setDialog(null)}
        title="الاعتذار عن الطلب مع السبب…"
        label="سبب الاعتذار"
        help="يظهر السبب للمالك في رسالة الاعتذار ويُسجَّل في سجل الحالة."
        confirmLabel="تأكيد الاعتذار"
        confirmVariant="sensitive"
        onSubmit={async (text, key) => {
          await post("/negotiation/decline-counter", { reason: text }, key);
          toast.toast({ tone: "ok", message: "سُجّل الاعتذار · الحالة الآن «حل مقترح»." });
          router.refresh();
        }}
      >
        <ul className="m-0 flex list-none flex-col gap-2 rounded-md bg-subtle p-3 text-14 leading-[22px]">
          <li className="flex gap-2"><Icon name="swap_horiz" size={18} className="text-muted" />تعود الحالة إلى <strong>حل مقترح</strong>، لا إلى الإحالة.</li>
          <li className="flex gap-2"><Icon name="chat" size={18} className="text-muted" />يُبلَّغ المالك برسالة اعتذار تتضمن السبب، وأن الفريق سيعمل معه على خيار آخر.</li>
        </ul>
      </ReasonDialog>
      <ReasonDialog
        key={typeof dialog === "object" && dialog ? `${dialog.id}-${dialog.approve}` : "ext"}
        open={typeof dialog === "object" && dialog !== null}
        onClose={() => setDialog(null)}
        title={typeof dialog === "object" && dialog?.approve ? "اعتماد تمديد مهلة العرض" : "رفض تمديد مهلة العرض"}
        description={typeof dialog === "object" && dialog ? dialog.title : undefined}
        label={typeof dialog === "object" && dialog?.approve ? "ملاحظة" : "سبب الرفض"}
        minLength={typeof dialog === "object" && dialog?.approve ? 0 : 5}
        confirmLabel={typeof dialog === "object" && dialog?.approve ? "تأكيد الاعتماد" : "تأكيد الرفض"}
        onSubmit={async (text, key) => {
          if (typeof dialog !== "object" || !dialog) return;
          await post(`/offer-extensions/${dialog.id}/decision`, { approve: dialog.approve, reason: text || null }, key);
          toast.toast({ tone: "ok", message: dialog.approve ? "مُدّدت مهلة العرض." : "رُفض طلب التمديد." });
          router.refresh();
        }}
      />
    </div>
  );
}

function ThreadEntry({ e }: { e: NegotiationEntryDto }) {
  const owner = e.authorType === "owner";
  const internal = e.internalOnly;
  const official = e.kind === "Offer";
  return (
    <article
      className={cn(
        "flex flex-col gap-1.5 rounded-md border p-4 text-14",
        owner ? "ms-6 border-rust-200 bg-rust-50 md:ms-12" : internal ? "border-dashed border-line-strong bg-warm" : "border-line bg-white",
      )}
    >
      <header className="flex flex-wrap items-center gap-2">
        <Icon name={ENTRY_ICON[e.kind] ?? "chat"} size={20} className={owner ? "text-rust-700" : official ? "text-ink" : "text-muted"} />
        <strong className="text-15">{e.authorLabel}</strong>
        {internal ? <Tag tone="neutral" icon="visibility_off">داخلية</Tag> : null}
        <time dateTime={e.at} className="ms-auto text-13 text-muted">
          <bdi dir="ltr">{formatDateTime(e.at)}</bdi>
        </time>
      </header>
      <p className="m-0 leading-6 whitespace-pre-line">{owner ? <>«<BidiText text={e.body} />»</> : <BidiText text={e.body} />}</p>
      {e.terms ? <BidiText text={e.terms} className={cn("text-13", owner ? "font-semibold text-rust-700" : "text-muted")} /> : null}
    </article>
  );
}

function InternalNoteComposer({ reference, onSaved }: { reference: string; onSaved: () => void }) {
  const key = useIdempotencyKey();
  const toast = useToast();
  const [body, setBody] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", `/cases/${reference}/negotiation/notes`, { body: body.trim(), internal: true }, { idempotencyKey: key.get() });
      key.reset();
      setBody("");
      toast.toast({ tone: "ok", message: "حُفظت الملاحظة الداخلية." });
      onSaved();
    } catch (e) {
      if (!isApiError(e) || e.status !== 0) key.reset();
      setError(isApiError(e) ? (e.status === 0 ? "تعذّر الاتصال بالخادم. أعد المحاولة." : e.fieldError("body") ?? e.title) : "تعذّر حفظ الملاحظة.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <form
      className="flex flex-col gap-3 rounded-md border border-dashed border-line-strong bg-warm p-4"
      onSubmit={(e) => {
        e.preventDefault();
        void save();
      }}
    >
      <Textarea
        label="ملاحظة داخلية"
        help="مرئية لفريق الحالة فقط ولا تظهر للمالك."
        value={body}
        onChange={(e) => {
          setBody(e.target.value);
          key.reset();
        }}
        maxLength={2000}
        error={error ?? undefined}
      />
      <Button type="submit" variant="secondary" icon="forum" className="self-start" loading={busy} disabled={body.trim().length < 2}>
        إضافة ملاحظة داخلية
      </Button>
    </form>
  );
}
