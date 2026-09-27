"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ConfirmDialog } from "@/components/case/ConfirmDialog";
import { ReasonDialog } from "@/components/case/ReasonDialog";
import { Alert, ApprovalChain, Button, Icon, Tag, useToast, type ApprovalStep } from "@/components/ui";
import { Checkbox } from "@/components/ui/Field";
import { apiSend } from "@/lib/api/client";
import type { ChainStep, ClosureOverview } from "@/lib/api/closure";
import { versionFileHref } from "@/lib/api/documents";
import { cn } from "@/lib/cn";
import { formatDateTime, formatMoney } from "@/lib/format";

type DialogKey = null | "review" | "reviewReturn" | "approve" | "approveReturn" | "request" | "decide" | "reject" | "summary";

const chainStatus = (s: ChainStep, i: number): ApprovalStep["status"] =>
  s.status === "done" ? (i === 0 ? "prepared" : i === 1 ? "reviewed" : "approved") : s.status === "in_progress" ? "pending" : "notice";

/**
 * L26 (B5 spec): «المطابقة المالية» with every amount's source and the difference formula, «مستندات الإغلاق», and the
 * aside «مراجعة قبل الإغلاق» (what happens, the approval chain, the next action with its reasons). Distribution belongs to
 * the judicial path (Phase 3, on hold V10) and is not shown here. A closed case is read-only.
 */
export function ClosureView({ reference, d, me, perms }: { reference: string; d: ClosureOverview; me: string; perms: { prepare: boolean; approve: boolean; upload: boolean } }) {
  const router = useRouter();
  const toast = useToast();
  const base = `/cases/${encodeURIComponent(reference)}`;
  const [dialog, setDialog] = useState<DialogKey>(null);
  const [traceAck, setTraceAck] = useState(false);
  const r = d.reconciliation;
  const closed = d.caseStatus === "closed";
  const preparer = r?.chain[0]?.user;
  const reviewer = r?.chain[1]?.user;
  const request = d.actions.find((a) => a.key === "request_closure");
  const decide = d.actions.find((a) => a.key === "decide_closure");
  const summaryDoc = d.documents.find((x) => x.type === "owner_final_summary" && x.status === "pending");
  const done = (message: string) => {
    toast.toast({ tone: "ok", message });
    router.refresh();
  };

  return (
    <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_400px]">
      <div className="flex min-w-0 flex-col gap-5">
        {closed ? (
          <Alert tone="ok" role="status" title="أُغلقت الحالة">
            {d.closedAt ? <>أُغلقت في <bdi dir="ltr">{formatDateTime(d.closedAt)}</bdi>. </> : null}
            الحالة مقفلة ولا يمكن تعديلها، ونُشرت مستندات الإغلاق للمالك في بوابته.
          </Alert>
        ) : null}

        <section aria-labelledby="rec-h" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex flex-col gap-0.5">
              <h2 id="rec-h" className="m-0 text-20 font-bold">المطابقة المالية</h2>
              {r ? <span className="text-14 text-muted">{r.basisLabel}{r.expectedSource ? ` · ${r.expectedSource}` : ""}</span> : null}
            </div>
            {r ? (
              <Tag tone={r.differenceTone === "ok" ? "ok" : r.differenceTone === "warn" ? "warn" : "err"} icon={r.differenceTone === "ok" ? "check_circle" : "error"}>
                {r.differenceLabel}
              </Tag>
            ) : null}
          </div>
          {r === null ? (
            <p className="m-0 text-14 text-muted">لم تبدأ التسوية المالية بعد.</p>
          ) : (
            <>
              <div role="table" aria-label="بنود المطابقة" className="flex flex-col text-14">
                <div role="row" className="grid grid-cols-[minmax(0,1.6fr)_1fr_1.4fr] gap-3 border-b border-line pb-2 font-semibold text-muted">
                  <span role="columnheader">البند</span>
                  <span role="columnheader">المبلغ (ر.س)</span>
                  <span role="columnheader">المصدر</span>
                </div>
                <Row label="المبلغ المتفق عليه" amount={r.expectedAmount} source={r.expectedSource ?? "—"} />
                {r.lines.map((l) => (
                  <Row
                    key={l.id}
                    label={l.label}
                    amount={l.amount}
                    source={l.reference ? `${l.reference}${l.kind === "receipt" ? " · مطابق" : ""}` : "—"}
                    tone={l.countsIn === "none" ? "muted" : undefined}
                  />
                ))}
                <Row label="إجمالي المستلم" amount={r.receivedAmount} source="مجموع المراجع" tone="total" />
                <Row label="الفرق" amount={r.difference} source={r.differenceExplanation ?? "—"} tone={r.difference === 0 ? "ok" : "diff"} />
              </div>
              <p className="m-0 text-13 text-muted">{r.formula}</p>
              {r.returnReason && r.status === "Returned" ? <Alert tone="warn" title="أُعيدت التسوية">{r.returnReason}</Alert> : null}
            </>
          )}
        </section>

        <section aria-labelledby="docs-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 id="docs-h" className="m-0 text-18 font-bold">مستندات الإغلاق</h2>
            {summaryDoc && perms.prepare && !closed ? (
              <Button size="sm" variant="secondary" icon="description" onClick={() => setDialog("summary")}>توليد ملخص المالك</Button>
            ) : null}
          </div>
          <ul className="m-0 flex list-none flex-col divide-y divide-divider p-0">
            {d.documents.map((doc) => (
              <li key={doc.id} className="flex flex-wrap items-center gap-3 py-2.5">
                <Icon
                  name={doc.status === "ready" ? "check_circle" : "schedule"}
                  size={22}
                  className={doc.status === "ready" ? "text-ok" : doc.blocksClosure ? "text-err" : "text-warn"}
                />
                <div className="flex min-w-0 flex-1 flex-col">
                  {doc.documentVersionId ? (
                    <a href={versionFileHref(reference, doc.documentVersionId)} className="truncate text-15 font-semibold">{doc.title}</a>
                  ) : (
                    <span className="text-15 font-semibold">{doc.title}</span>
                  )}
                  <span className="text-13 text-muted">
                    {doc.meta}
                    {doc.externalReference ? <> · <bdi dir="ltr">{doc.externalReference}</bdi></> : null}
                  </span>
                </div>
                {doc.shareWithOwner ? (
                  <Tag tone={doc.visibleToOwner ? "ok" : "neutral"} icon={doc.visibleToOwner ? "visibility" : "schedule_send"}>
                    {doc.visibleToOwner ? "منشور للمالك" : "يُنشر للمالك عند الإغلاق"}
                  </Tag>
                ) : null}
              </li>
            ))}
          </ul>
        </section>

        <details className="rounded-lg border border-line bg-white p-5">
          <summary className="cursor-pointer text-16 font-bold">
            تتبع المصادر ({d.trace.length} بنداً{d.missingSources > 0 ? ` · ${d.missingSources} بلا مصدر` : ""})
          </summary>
          <ul className="m-0 mt-3 flex list-none flex-col gap-2 p-0 text-14">
            {d.trace.map((t) => (
              <li key={t.key} className={cn("flex flex-wrap justify-between gap-2 rounded-md border p-2.5", t.hasSource ? "border-line" : "border-err-line bg-err-bg")}>
                <span className="font-semibold">{t.label}</span>
                <span>{t.amount !== null ? <bdi dir="ltr">{formatMoney(t.amount)}</bdi> : "—"}</span>
                <span className="w-full text-13 text-muted">{t.sourceTypeLabel} · {t.source}{t.sourceDate ? ` · ${t.sourceDate}` : ""}</span>
              </li>
            ))}
          </ul>
        </details>
      </div>

      <aside className="flex flex-col gap-4" aria-label="مراجعة قبل الإغلاق">
        <section aria-labelledby="review-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <h2 id="review-h" className="m-0 text-18 font-bold">مراجعة قبل الإغلاق</h2>
          <strong className="text-14">ما سيحدث:</strong>
          <ul className="m-0 flex flex-col gap-1.5 ps-5 text-14 leading-6">
            {d.consequences.map((x) => <li key={x}>{x}</li>)}
          </ul>
          {r ? (
            <>
              <strong className="text-14">الموافقات المطلوبة</strong>
              <ApprovalChain
                steps={r.chain.map((s, i) => ({
                  role: s.role,
                  who: s.user ?? undefined,
                  status: chainStatus(s, i),
                  state: s.status === "done" ? "مكتمل" : s.status === "in_progress" ? "بانتظار القرار" : "لاحقاً",
                  meta: s.at ? <bdi dir="ltr">{formatDateTime(s.at)}</bdi> : undefined,
                }))}
              />
              <p className="m-0 text-13 text-muted">المُعِدّ ≠ المدقق ≠ المعتمد: كل خطوة لشخص مختلف، والاعتماد برمز تحقق.</p>
            </>
          ) : null}

          {!closed && r ? (
            <div className="flex flex-col gap-2 border-t border-divider pt-3">
              {r.status === "Submitted" && perms.prepare ? (
                preparer === me ? (
                  <p className="m-0 text-13 text-muted">أعددت هذه التسوية؛ يدققها موظف مالية آخر.</p>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    <Button onClick={() => setDialog("review")} icon="fact_check">تدقيق التسوية…</Button>
                    <Button variant="secondary" onClick={() => setDialog("reviewReturn")}>إعادة للمُعِدّ…</Button>
                  </div>
                )
              ) : null}
              {r.status === "Reviewed" && perms.approve ? (
                preparer === me || reviewer === me ? (
                  <p className="m-0 text-13 text-muted">شاركت في إعداد أو تدقيق هذه التسوية؛ يعتمدها شخص ثالث.</p>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    <Button review onClick={() => setDialog("approve")} icon="verified">اعتماد التسوية…</Button>
                    <Button variant="secondary" onClick={() => setDialog("approveReturn")}>إعادة للمُعِدّ…</Button>
                  </div>
                )
              ) : null}
            </div>
          ) : null}
        </section>

        {!closed && (request || decide) ? (
          <section aria-labelledby="close-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
            <h2 id="close-h" className="m-0 text-17 font-bold">الإغلاق</h2>
            {d.request?.status === "Pending" ? (
              <p className="m-0 text-14">
                طلب الإغلاق بانتظار القرار · أرسله {d.request.requestedBy ?? "—"} · <bdi dir="ltr">{formatDateTime(d.request.requestedAt)}</bdi>
              </p>
            ) : null}
            {d.request?.status === "Rejected" ? <Alert tone="warn" title="رُفض طلب الإغلاق">{d.request.decisionReason}</Alert> : null}
            {[request, decide].filter((a) => a !== undefined).map((a) => (
              <div key={a!.key} className="flex flex-col gap-1.5">
                <Button
                  review={a!.key === "decide_closure"}
                  softDisabled={!a!.enabled}
                  aria-describedby={a!.enabled ? undefined : `why-${a!.key}`}
                  onClick={() => setDialog(a!.key === "request_closure" ? "request" : "decide")}
                >
                  {a!.label}
                </Button>
                {!a!.enabled ? (
                  <ul id={`why-${a!.key}`} className="m-0 flex list-none flex-col gap-1 p-0 text-13 text-muted">
                    {a!.reasons.map((x) => (
                      <li key={x} className="flex items-start gap-1"><Icon name="block" size={16} />{x}</li>
                    ))}
                  </ul>
                ) : null}
                {a!.key === "decide_closure" && a!.enabled ? (
                  <Button variant="text" size="sm" className="self-start" onClick={() => setDialog("reject")}>رفض طلب الإغلاق…</Button>
                ) : null}
              </div>
            ))}
          </section>
        ) : null}
      </aside>

      <ReasonDialog
        open={dialog === "review"}
        onClose={() => setDialog(null)}
        title="تدقيق التسوية المالية"
        description="خطوة المدقق: تأكد أن كل مبلغ مستلم يطابق مرجعه في كشف الحساب وأن الفرق صفر أو مفسَّر."
        label="ملاحظة التدقيق"
        confirmLabel="تأكيد التدقيق"
        onSubmit={async (reason, key) => {
          await apiSend("POST", `${base}/reconciliation/review`, { decision: "approve", reason }, { idempotencyKey: key });
          done("اكتمل تدقيق التسوية · بانتظار الاعتماد.");
        }}
      />
      {(["reviewReturn", "approveReturn"] as const).map((k) => (
        <ReasonDialog
          key={k}
          open={dialog === k}
          onClose={() => setDialog(null)}
          title="إعادة التسوية للمُعِدّ"
          label="سبب الإعادة"
          confirmLabel="إعادة"
          confirmVariant="secondary"
          onSubmit={async (reason, key) => {
            await apiSend("POST", `${base}/reconciliation/${k === "reviewReturn" ? "review" : "approve"}`, { decision: "return", reason }, { idempotencyKey: key });
            done("أُعيدت التسوية للمُعِدّ.");
          }}
        />
      ))}
      <ReasonDialog
        open={dialog === "approve"}
        onClose={() => setDialog(null)}
        title="اعتماد التسوية المالية"
        description="يُطلب رمز التحقق لتأكيد الاعتماد."
        label="سبب القرار"
        confirmLabel="اعتماد"
        onSubmit={async (reason, key) => {
          await apiSend("POST", `${base}/reconciliation/approve`, { decision: "approve", reason }, { idempotencyKey: key });
          done("اعتُمدت التسوية المالية.");
        }}
      />
      <ConfirmDialog
        open={dialog === "summary"}
        onClose={() => setDialog(null)}
        title="توليد ملخص الحالة النهائي للمالك"
        confirmLabel="توليد الملخص"
        onConfirm={async (key) => {
          await apiSend("POST", `${base}/closure/documents/owner-summary`, {}, { idempotencyKey: key });
          done("وُلّد ملخص الحالة النهائي للمالك.");
        }}
      >
        <p className="m-0 text-14 leading-6">يُولَّد الملخص من أرقام التتبع الحالية ويُنشر للمالك عند الإغلاق فقط.</p>
      </ConfirmDialog>
      <ReasonDialog
        open={dialog === "request"}
        onClose={() => setDialog(null)}
        title="إرسال الإغلاق للاعتماد"
        description="يُحفظ تتبع المصادر الحالي مع الطلب؛ أي تغيير في الأرقام بعده يستلزم طلباً جديداً."
        label="ملاحظة الإغلاق"
        minLength={10}
        confirmLabel="إرسال للاعتماد"
        onSubmit={async (note, key) => {
          await apiSend("POST", `${base}/closure/request`, { note }, { idempotencyKey: key });
          done("أُرسل الإغلاق للاعتماد.");
        }}
      />
      <ReasonDialog
        open={dialog === "decide"}
        onClose={() => {
          setDialog(null);
          setTraceAck(false);
        }}
        title="اعتماد الإغلاق"
        description="إجراء نهائي: تُقفل الحالة وتُنشر مستندات الإغلاق للمالك. يُطلب رمز التحقق."
        label="سبب القرار"
        minLength={10}
        confirmLabel="اعتماد الإغلاق"
        onSubmit={async (reason, key) => {
          await apiSend("POST", `${base}/closure/decision`, { decision: "approve", reason, traceAcknowledged: traceAck }, { idempotencyKey: key });
          done("أُغلقت الحالة ونُشرت مستنداتها للمالك.");
        }}
      >
        <Checkbox label="راجعت تتبع المصادر ولا يوجد رقم دون مصدر" checked={traceAck} onChange={(e) => setTraceAck(e.target.checked)} />
      </ReasonDialog>
      <ReasonDialog
        open={dialog === "reject"}
        onClose={() => setDialog(null)}
        title="رفض طلب الإغلاق"
        label="سبب الرفض"
        minLength={10}
        confirmLabel="رفض الطلب"
        confirmVariant="sensitive"
        onSubmit={async (reason, key) => {
          await apiSend("POST", `${base}/closure/decision`, { decision: "reject", reason, traceAcknowledged: false }, { idempotencyKey: key });
          done("رُفض طلب الإغلاق وأُبلغ مقدمه.");
        }}
      />
    </div>
  );
}

function Row({ label, amount, source, tone }: { label: string; amount: number; source: string; tone?: "total" | "ok" | "diff" | "muted" }) {
  return (
    <div
      role="row"
      className={cn(
        "grid grid-cols-[minmax(0,1.6fr)_1fr_1.4fr] gap-3 border-b border-divider py-2.5",
        tone === "total" && "bg-warm font-bold",
        tone === "ok" && "bg-ok-bg font-bold text-ok",
        tone === "diff" && "bg-err-bg font-bold text-err",
        tone === "muted" && "text-muted",
      )}
    >
      <span role="cell">{label}</span>
      <span role="cell">
        <bdi dir="ltr">{formatMoney(amount)}</bdi>
      </span>
      <span role="cell" className="break-words">{source}</span>
    </div>
  );
}
