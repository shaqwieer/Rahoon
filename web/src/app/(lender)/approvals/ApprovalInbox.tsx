"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { StepUpDialog } from "@/components/case/StepUpDialog";
import { PageHeader } from "@/components/shell/PageHeader";
import { Alert, Button, EmptyState, Icon, RadioCardGroup, SlaBadge, Tabs, Tag, Textarea, useToast } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { toSlaTone } from "@/lib/api/lender";
import { cn } from "@/lib/cn";

export interface InboxItem {
  id: string;
  caseRef: string;
  title: string;
  requestTitle: string;
  meta: string;
  slaTone: string;
  slaText: string;
  dueOn: string;
  amount: number | null;
  status: string;
  assignedToMe: boolean;
  assignee: string | null;
  escalated: boolean;
  canDecide: boolean;
  decidedAt: string | null;
  decisionReason: string | null;
}

export interface ApprovalDetail {
  id: string;
  caseRef: string;
  status: string;
  eyebrow: string;
  title: string;
  version: number;
  openedVersion: number;
  figures: Array<{ label: string; value: string; sub: string; tone: string }>;
  reviewerNote: string;
  evidence: string[];
  links: { solution: string; compare: string; preview: string };
  effects: { approve: string[]; return: string[]; reject: string[] };
  canDecide: boolean;
  escalated: boolean;
  blockedReason: string | null;
  stepUpActive: boolean;
}

const DECISIONS = [
  { value: "approve", label: "اعتماد وإرسال للمالك", confirm: "تأكيد الاعتماد" },
  { value: "return", label: "إعادة للتعديل", confirm: "تأكيد الإعادة" },
  { value: "reject", label: "رفض الحل", confirm: "تأكيد الرفض" },
] as const;

export function ApprovalInbox({ items, status, detail }: { items: InboxItem[]; status: string; detail: ApprovalDetail | null }) {
  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="الموافقات" description="مرتبة حسب أقرب مهلة. الطلبات فوق حدك تظهر «مُصعّد» ولا يمكنك اعتمادها، ولا يظهر لك طلب أعددته أو راجعته." />
      <Tabs label="حالة الطلبات" active={status} tabs={[
        { key: "pending", label: "بانتظار القرار", count: status === "pending" ? items.length : undefined, href: "/approvals" },
        { key: "decided", label: "قراراتي", href: "/approvals?status=decided" },
      ]} />
      {items.length === 0 ? (
        <EmptyState icon="approval" title={status === "pending" ? "لا طلبات بانتظار قرارك" : "لم تتخذ قرارات بعد"} body="ستصلك إشعارات عند إسناد طلب جديد إليك." />
      ) : (
        <div className="grid items-start gap-5 xl:grid-cols-[400px_minmax(0,1fr)]">
          <ul className="m-0 flex list-none flex-col gap-2 p-0" aria-label="طلبات الموافقة">
            {items.map((i) => (
              <li key={i.id}>
                <Link
                  href={`/approvals?${status === "decided" ? "status=decided&" : ""}request=${i.id}`}
                  aria-current={detail?.id === i.id ? "true" : undefined}
                  className={cn("flex flex-col gap-1 rounded-md border bg-white p-4 text-ink no-underline hover:bg-warm",
                    detail?.id === i.id ? "border-ink bar-start" : "border-line")}
                >
                  <span className="flex items-center justify-between gap-2">
                    <bdi dir="ltr" className="font-mono text-13 font-semibold">{i.caseRef}</bdi>
                    {i.status === "Pending" ? <SlaBadge tone={toSlaTone(i.slaTone)} size="sm">{i.slaText}</SlaBadge> : <Tag tone={i.status === "Approved" ? "ok" : "warn"}>{i.status === "Approved" ? "معتمد" : i.status === "Returned" ? "أُعيد" : "مرفوض"}</Tag>}
                  </span>
                  <strong className="text-15">{i.title}</strong>
                  <span className="text-13 text-muted">{i.meta}</span>
                  <span className="flex flex-wrap gap-1.5">
                    {i.escalated ? <Tag tone="warn" icon="trending_up">مُصعّد · فوق حدك</Tag> : null}
                    {!i.assignedToMe && i.assignee ? <Tag tone="neutral">مسند إلى {i.assignee}</Tag> : null}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          {detail ? <DecisionPane key={detail.id} d={detail} /> : null}
        </div>
      )}
    </div>
  );
}

function DecisionPane({ d }: { d: ApprovalDetail }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [decision, setDecision] = useState<(typeof DECISIONS)[number]["value"]>("approve");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [stepUp, setStepUp] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [stale, setStale] = useState(false);
  const effects = decision === "approve" ? d.effects.approve : decision === "return" ? d.effects.return : d.effects.reject;
  const confirmLabel = DECISIONS.find((x) => x.value === decision)!.confirm;

  const decide = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend<{ caseStatus: string }>("POST", `/approvals/${d.id}/decision`, { decision, reason, openedVersion: d.openedVersion }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: decision === "approve" ? "اعتُمد الحل وأُرسل العرض للمالك." : decision === "return" ? "أُعيد الحل للتعديل." : "رُفض الحل." });
      router.push("/approvals");
      router.refresh();
    } catch (e) {
      key.reset();
      if (isApiError(e) && e.code === "step_up_required") setStepUp(true);
      else if (isApiError(e) && e.code === "request_changed") setStale(true);
      else setError(isApiError(e) ? e.title : "تعذّر تسجيل القرار.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section aria-labelledby="dp-h" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-col gap-1">
        <span className="text-13 text-muted">{d.eyebrow}</span>
        <h2 id="dp-h" className="m-0 text-20 font-bold">{d.title}</h2>
      </div>
      {stale ? (
        <Alert tone="warn" title="تغيّر الطلب بعد فتحه" action={<Button variant="secondary" size="sm" onClick={() => router.refresh()}>إعادة التحميل</Button>}>
          أعد تحميل الصفحة وراجع أحدث إصدار قبل القرار.
        </Alert>
      ) : null}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {d.figures.map((f) => (
          <div key={f.label} className="flex flex-col gap-1 rounded-md bg-warm p-3">
            <span className="text-13 text-muted">{f.label}</span>
            <strong className="text-18"><bdi dir="ltr">{f.value}</bdi></strong>
            <span className={cn("text-12", f.tone === "ok" ? "text-ok" : f.tone === "err" ? "text-err" : "text-muted")}>{f.sub}</span>
          </div>
        ))}
      </div>
      <div className="flex flex-col gap-2 rounded-md border border-line p-4 text-14">
        <strong>ملاحظة المراجِع</strong>
        <p className="m-0 leading-7">«{d.reviewerNote}»</p>
        <div className="flex flex-wrap gap-3 text-13">
          <Link href={d.links.solution}>الحل v{d.version} (مقفل)</Link>
          <Link href={d.links.compare}>الفروق عن الإصدار السابق</Link>
          <Link href={d.links.preview}>معاينة ما سيراه المالك</Link>
        </div>
        <span className="flex flex-wrap gap-1.5">{d.evidence.map((e) => <Tag key={e} tone="neutral" icon="attach_file">{e}</Tag>)}</span>
      </div>

      {d.canDecide ? (
        <div className="flex flex-col gap-4 rounded-md border border-line p-4">
          <strong className="text-16">قرارك</strong>
          <RadioCardGroup legend="القرار" name="decision" columns={3} value={decision}
            onChange={(v) => setDecision(v as typeof decision)} options={DECISIONS.map((x) => ({ value: x.value, label: x.label }))} />
          <div className="rounded-md bg-subtle p-3 text-14">
            <strong>{decision === "approve" ? "عند الاعتماد" : decision === "return" ? "عند الإعادة" : "عند الرفض"}</strong>
            <ul className="m-0 mt-1 flex flex-col gap-1 ps-5">{effects.map((e) => <li key={e}>{e}</li>)}</ul>
          </div>
          <Textarea label="سبب القرار" requiredMark value={reason} onChange={(e) => setReason(e.target.value)} maxLength={1000}
            placeholder="مثال: الحل قابل للسداد وضمن حدودي، والتنازل مبرر بظرف موثق." />
          {error ? <Alert tone="err" title={error} /> : null}
          <div className="flex flex-wrap items-center gap-3">
            <span className="flex items-center gap-1.5 text-13 text-muted"><Icon name="passkey" size={18} />سيُطلب رمز التحقق لتأكيد القرار</span>
            <Button className="ms-auto" onClick={() => (d.stepUpActive ? void decide() : setStepUp(true))} loading={busy} disabled={reason.trim().length < 10 || stale}>
              {confirmLabel}
            </Button>
          </div>
        </div>
      ) : (
        <Alert tone="info" role="none" title={d.blockedReason ?? "للقراءة فقط"}>
          {d.escalated ? "يُحال الطلب لمعتمد أعلى وفق حدود الموافقة؛ لا يمكنك اعتماده." : undefined}
        </Alert>
      )}
      <p className="m-0 text-12 text-muted">القرار متاح على الجوال بنفس شاشة المراجعة ورمز التحقق؛ لا اعتماد بالسحب أو بلمسة واحدة.</p>
      <StepUpDialog open={stepUp} onClose={() => setStepUp(false)} onVerified={() => { setStepUp(false); void decide(); }} />
    </section>
  );
}
