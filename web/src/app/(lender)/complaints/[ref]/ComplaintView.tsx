"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { PageHeader } from "@/components/shell/PageHeader";
import { Alert, Button, Checkbox, DateText, Icon, Ref, Select, Tag, Textarea, TextField, useToast } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import {
  COMPLAINT_DECISIONS,
  COMPLAINT_STATUS_ICON,
  COMPLAINT_STATUS_TONE,
  parseFinding,
  type ComplaintDetail,
} from "@/lib/api/complaints";
import { cn } from "@/lib/cn";

const FINDING_META = {
  ok: { icon: "check_circle", className: "text-ok", label: "سليم" },
  issue: { icon: "error", className: "text-err", label: "خلل" },
  info: { icon: "info", className: "text-info", label: "ملاحظة" },
} as const;

type DecisionValue = (typeof COMPLAINT_DECISIONS)[number]["value"];

export function ComplaintView({ d }: { d: ComplaintDetail }) {
  const fullText = d.body !== null;
  const open = d.status !== "Resolved" && d.status !== "Closed";
  const dueTone = d.dueText.startsWith("الرد خلال") ? "warn" : "err";

  return (
    <div className="flex flex-col gap-2">
      <p className="m-0 flex flex-wrap items-center gap-x-2 gap-y-1 text-14 text-muted">
        <Ref>{d.reference}</Ref>
        <span aria-hidden="true">·</span>
        <span>الحالة <Link href={`/cases/${d.caseRef}`}><Ref strong={false}>{d.caseRef}</Ref></Link></span>
        <span aria-hidden="true">·</span>
        <span>قُدمت عبر {d.via}</span>
      </p>
      <PageHeader title={fullText && d.subject ? `«${d.subject}»` : <>الشكوى <bdi dir="ltr" className="font-mono">{d.reference}</bdi></>} />
      <div className="-mt-3 mb-4 flex flex-wrap gap-2">
        <Tag tone={COMPLAINT_STATUS_TONE[d.status] ?? "neutral"} icon={COMPLAINT_STATUS_ICON[d.status]}>{d.statusLabel}</Tag>
        {open ? <Tag tone={dueTone} icon="alarm">{d.dueText}</Tag> : null}
        <Tag tone="neutral" icon="person">
          {d.reviewer ? `المراجِع: ${d.reviewer} (مستقل عن فريق الحالة)` : "بانتظار إسناد مراجِع مستقل"}
        </Tag>
      </div>

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_380px]">
        <div className="flex min-w-0 flex-col gap-4">
          {fullText ? (
            <>
              <section aria-labelledby="body-h" className="flex flex-col gap-2 rounded-lg border border-line bg-white p-5">
                <h2 id="body-h" className="m-0 text-17 font-semibold">نص الشكوى</h2>
                <p className="m-0 text-15 leading-7 whitespace-pre-wrap">{d.body}</p>
                <span className="text-13 text-muted">
                  {d.submittedBy} · <DateText value={d.submittedAt} mode="datetime" />
                </span>
              </section>
              <FindingsCard d={d} />
              {d.canDecide ? (
                <DecisionCard d={d} />
              ) : open ? (
                <Alert tone="info" role="none" title="للقراءة فقط">
                  {d.reviewer ? `المراجعة مسندة إلى ${d.reviewer}؛ القرار والرد له وحده.` : "لم تُسند المراجعة بعد لمراجِع مستقل."}
                </Alert>
              ) : (
                <SentResponse d={d} />
              )}
            </>
          ) : (
            <Alert tone="info" role="none" icon="visibility_off" title="يظهر لك وسم الشكوى فقط">
              تراجعها جهة مستقلة عن فريق الحالة (الامتثال). لا يُعرض نص الشكوى ولا ما وجدته المراجعة ولا الرد لغير المراجِع.
            </Alert>
          )}
        </div>

        <aside className="flex flex-col gap-4">
          {d.impact.length ? (
            <Alert tone="err" role="none" icon="block" title="أثر الشكوى المفتوحة على الحالة">
              <ul className="m-0 flex list-none flex-col gap-1 p-0">
                {d.impact.map((i) => <li key={i}>• {i}</li>)}
              </ul>
            </Alert>
          ) : null}
          <PathCard path={d.path} />
          <section aria-labelledby="others-h" className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4">
            <h2 id="others-h" className="m-0 text-16 font-semibold">شكاوى مفتوحة أخرى</h2>
            {d.otherOpen.length === 0 ? <p className="m-0 text-14 text-muted">لا شكاوى مفتوحة أخرى على الحالة.</p> : null}
            <ul className="m-0 flex list-none flex-col gap-2 p-0">
              {d.otherOpen.map((o) => (
                <li key={o.reference} className="flex flex-wrap items-center gap-2 text-14">
                  <Link href={`/complaints/${o.reference}`}><Ref>{o.reference}</Ref></Link>
                  <span className="text-muted">{o.type} · المهلة <DateText value={o.dueOn} /></span>
                </li>
              ))}
            </ul>
          </section>
        </aside>
      </div>
    </div>
  );
}

function FindingsCard({ d }: { d: ComplaintDetail }) {
  const router = useRouter();
  const toast = useToast();
  const [severity, setSeverity] = useState<"ok" | "issue" | "info">("info");
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const findings = d.findings.map(parseFinding);

  const add = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", `/complaints/${encodeURIComponent(d.reference)}/findings`, { severity, text: text.trim() });
      setText("");
      toast.toast({ tone: "ok", message: "أُضيفت الملاحظة." });
      router.refresh();
    } catch (e) {
      setError(isApiError(e) ? e.fieldError("text") ?? e.title : "تعذّر إضافة الملاحظة.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section aria-labelledby="find-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
      <h2 id="find-h" className="m-0 text-17 font-semibold">ما وجدته المراجعة</h2>
      {findings.length === 0 ? <p className="m-0 text-14 text-muted">لم تُسجّل ملاحظات بعد.</p> : null}
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {findings.map((f, i) => {
          const m = FINDING_META[f.severity];
          return (
            <li key={i} className="flex gap-2 text-15 leading-6">
              <Icon name={m.icon} size={20} label={m.label} className={m.className} />
              <span>{f.text}</span>
            </li>
          );
        })}
      </ul>
      {d.canDecide ? (
        <div className="flex flex-col gap-3 border-t border-divider pt-3">
          {error ? <Alert tone="err" title={error} /> : null}
          <div className="grid gap-3 sm:grid-cols-[180px_minmax(0,1fr)]">
            <Select
              label="النوع"
              value={severity}
              onChange={(e) => setSeverity(e.target.value as typeof severity)}
              options={[
                { value: "ok", label: "سليم" },
                { value: "issue", label: "خلل" },
                { value: "info", label: "ملاحظة" },
              ]}
            />
            <TextField label="الملاحظة" value={text} onChange={(e) => setText(e.target.value)} maxLength={500} />
          </div>
          <Button variant="secondary" size="sm" icon="add" className="self-start" onClick={() => void add()} loading={busy} disabled={text.trim().length === 0}>
            إضافة ملاحظة
          </Button>
        </div>
      ) : null}
    </section>
  );
}

function DecisionCard({ d }: { d: ComplaintDetail }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [decision, setDecision] = useState<DecisionValue | null>(COMPLAINT_DECISIONS.find((x) => x.api === d.decision)?.value ?? null);
  const [extend, setExtend] = useState(false);
  const [response, setResponse] = useState(d.response ?? "");
  const [busy, setBusy] = useState<"send" | "draft" | null>(null);
  const [error, setError] = useState<{ title: string; decision?: string; response?: string } | null>(null);
  const complete = decision !== null && response.trim().length > 0;

  const touch = () => key.reset();

  const saveDraft = async () => {
    setBusy("draft");
    setError(null);
    try {
      await apiSend("PUT", `/complaints/${encodeURIComponent(d.reference)}/draft`, { decision, response });
      toast.toast({ tone: "ok", message: "حُفظت المسودة." });
      router.refresh();
    } catch (e) {
      setError({ title: isApiError(e) && e.title ? e.title : "تعذّر حفظ المسودة." });
    } finally {
      setBusy(null);
    }
  };

  const send = async () => {
    setBusy("send");
    setError(null);
    try {
      await apiSend("POST", `/complaints/${encodeURIComponent(d.reference)}/decision`,
        { decision, response: response.trim(), extendOfferDeadline: extend, extensionDays: extend ? 7 : null }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: "أُرسل الرد للمالك وأُغلقت المراجعة." });
      router.refresh();
    } catch (e) {
      key.reset();
      setError(isApiError(e)
        ? { title: e.title || "تعذّر إرسال الرد.", decision: e.fieldError("decision"), response: e.fieldError("response") }
        : { title: "تعذّر إرسال الرد." });
    } finally {
      setBusy(null);
    }
  };

  return (
    <section aria-labelledby="dec-h" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
      <h2 id="dec-h" className="m-0 text-17 font-semibold">القرار والرد</h2>
      {error ? <Alert tone="err" title={error.title} /> : null}
      <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-2 text-14 font-semibold">القرار <span className="text-err">*</span></legend>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
          {COMPLAINT_DECISIONS.map((o) => {
            const selected = decision === o.value;
            return (
              <label
                key={o.value}
                className={cn(
                  "flex min-h-11 cursor-pointer items-center justify-center rounded-md px-3 text-15 has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ink",
                  selected ? "border-2 border-ink bg-rust-50 font-bold" : "border border-line-strong bg-white",
                )}
              >
                <input
                  type="radio"
                  name="complaint-decision"
                  value={o.value}
                  checked={selected}
                  onChange={() => { setDecision(o.value); touch(); }}
                  className="sr-only"
                />
                {o.label}
              </label>
            );
          })}
        </div>
        {error?.decision ? <span className="flex items-center gap-1 text-13 text-err"><Icon name="error" size={16} />{error.decision}</span> : null}
      </fieldset>
      <Checkbox
        label="تمديد مهلة العرض 7 أيام (يتطلب موافقة مدير الحالة)"
        description="يُنشأ طلب موافقة لمدير الحالة عند إرسال الرد، إن كان للمالك عرض قائم."
        checked={extend}
        onChange={(e) => { setExtend(e.target.checked); touch(); }}
      />
      <Textarea
        label="الرد المكتوب للمالك"
        requiredMark
        rows={6}
        maxLength={4000}
        value={response}
        onChange={(e) => { setResponse(e.target.value); touch(); }}
        error={error?.response}
        help="30 حرفاً على الأقل، ويجب أن يذكر حق طلب إعادة النظر أو التقدم للجهات المختصة."
      />
      <div className="flex flex-wrap items-center gap-3">
        <Button onClick={() => void send()} loading={busy === "send"} softDisabled={!complete || busy !== null} aria-describedby="dec-hint">
          إرسال الرد وإغلاق المراجعة
        </Button>
        <Button variant="secondary" onClick={() => void saveDraft()} loading={busy === "draft"} disabled={busy !== null}>
          حفظ مسودة
        </Button>
        <span id="dec-hint" className="text-13 text-muted">
          {complete ? "يصل الرد للمالك في البوابة ويُسجَّل في سجل الحالة." : "اختر القرار واكتب الرد لتفعيل الإرسال."}
        </span>
      </div>
    </section>
  );
}

function SentResponse({ d }: { d: ComplaintDetail }) {
  const label = COMPLAINT_DECISIONS.find((x) => x.api === d.decision)?.label;
  return (
    <section aria-labelledby="resp-h" className="flex flex-col gap-2 rounded-lg border border-line bg-white p-5">
      <h2 id="resp-h" className="m-0 text-17 font-semibold">القرار والرد</h2>
      {label ? <Tag tone="neutral" icon="rule" className="self-start">{label}</Tag> : null}
      {d.response ? <p className="m-0 text-15 leading-7 whitespace-pre-wrap">{d.response}</p> : <p className="m-0 text-14 text-muted">لا يوجد رد مكتوب.</p>}
      {d.respondedAt ? <span className="text-13 text-muted">أُرسل <DateText value={d.respondedAt} mode="datetime" /></span> : null}
    </section>
  );
}

function PathCard({ path }: { path: ComplaintDetail["path"] }) {
  return (
    <section aria-labelledby="path-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
      <h2 id="path-h" className="m-0 text-16 font-semibold">المسار</h2>
      <ol className="m-0 flex list-none flex-col gap-3 p-0">
        {path.map((s, i) => (
          <li key={i} aria-current={s.current ? "step" : undefined} className="flex items-start gap-2.5 text-14">
            {s.done ? (
              <Icon name="check_circle" size={20} className="text-ok" label="مكتمل" />
            ) : s.current ? (
              <span className="mt-0.5 flex size-5 flex-none items-center justify-center" aria-label="الخطوة الحالية" role="img">
                <span className="size-3 rounded-full bg-orange" />
              </span>
            ) : (
              <Icon name="radio_button_unchecked" size={20} className="text-soft" label="لم تبدأ" />
            )}
            <span className={cn("flex-1", s.current && "font-bold", !s.done && !s.current && "text-muted")}>{s.label}</span>
            <span className="text-13 text-muted">
              {s.done && s.date ? <DateText value={s.date} /> : s.current ? "الآن" : "—"}
            </span>
          </li>
        ))}
      </ol>
    </section>
  );
}
