"use client";

import Link from "next/link";
import { useState } from "react";
import { ActionDialog, ActionError, useTeamAction } from "@/components/market/team/useTeamAction";
import { Amount, Badge, Card, StatusBadge, Timeline } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Field";
import { apiSend } from "@/lib/api/client";
import type { BuyerRequestView, Fit, MarketEvent } from "@/lib/market/types";

export interface TeamInterest {
  reference: string;
  status: string;
  statusLabel: string;
  message: string | null;
  contactPreference: string | null;
  buyerName: string | null;
  phoneMasked: string | null;
  createdAt: string;
  assignedTo: { id: string; label: string } | null;
  closeReason: string | null;
  opportunity: { reference: string; title: string; status: string; statusLabel: string };
  terms: { versionNo: number; current: boolean; dueNow: number | null; buyerTotal: number | null; quality: string };
  buyerRequest: BuyerRequestView | null;
  fit: Fit | null;
  events: MarketEvent[];
  notice: string;
  /** Server-computed: follow-up actions need market.follow for this interest (its own scope). */
  actions: { follow: boolean; assignOthers: boolean };
}

/** Follow-up of one interest by the team. It never reserves the property or accepts an offer. */
export function TeamInterestView({ i }: { i: TeamInterest }) {
  const base = `/team/market/interests/${i.reference}`;
  const { busy, error, setError, run } = useTeamAction();
  const [dlg, setDlg] = useState<null | "close" | "note" | "follow">(null);
  const [text, setText] = useState("");
  const [phone, setPhone] = useState<string | null>(null);
  const open = (k: typeof dlg) => { setError(null); setText(""); setDlg(k); };
  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <Link href="/team/interests" className="text-14 font-semibold">← الاهتمامات</Link>
          <h1 className="m-0 flex items-center gap-2 text-24 font-bold"><bdi dir="ltr" className="font-mono">{i.reference}</bdi><StatusBadge status={i.status} label={i.statusLabel} /></h1>
        </div>
        <div className="flex flex-wrap gap-2">
          {i.actions.follow && i.status === "received" ? <Button onClick={() => open("follow")}>بدء المتابعة</Button> : null}
          {i.actions.follow && (i.status === "received" || i.status === "inFollowUp") ? <Button variant="secondary" onClick={() => open("close")}>إغلاق مسبب</Button> : null}
          {i.actions.follow ? <Button variant="secondary" onClick={() => void run("assign", "POST", `${base}/assign`, {})}>إسناد لي</Button> : null}
        </div>
      </div>
      <Alert tone="info" compact>{i.notice}</Alert>
      <ActionError error={dlg ? null : error} />
      <div className="grid gap-5 xl:grid-cols-2">
        <Card title="المشتري">
          <dl className="m-0 flex flex-col gap-2 text-14">
            <div><dt className="text-muted">الاسم</dt><dd className="m-0 font-semibold">{i.buyerName ?? "—"}</dd></div>
            <div><dt className="text-muted">الجوال</dt><dd className="m-0 flex items-center gap-2"><bdi dir="ltr" className="font-mono">{phone ?? i.phoneMasked}</bdi>
              {!phone && i.actions.follow ? <button type="button" className="text-13 text-rust" onClick={async () => { const r = await apiSend<{ phone: string }>("GET", `${base}/contact`).catch(() => null); if (r) setPhone(r.phone); }}>إظهار (يُسجل)</button> : null}</dd></div>
            <div><dt className="text-muted">التواصل المفضل</dt><dd className="m-0">{i.contactPreference === "call" ? "اتصال" : i.contactPreference === "whatsapp" ? "واتساب" : "أيهما"}</dd></div>
            {i.message ? <div><dt className="text-muted">رسالته</dt><dd className="m-0">{i.message}</dd></div> : null}
            <div><dt className="text-muted">طلب الشراء</dt><dd className="m-0">{i.buyerRequest ? <Link href={`/team/buyers/${i.buyerRequest.reference}`}>{i.buyerRequest.reference} — {i.buyerRequest.statusLabel}</Link> : "لا يوجد طلب شراء"}</dd></div>
          </dl>
          {i.fit ? (
            <div className={i.fit.fits ? "mt-3 rounded-md border border-ok-line bg-ok-bg p-3 text-13" : "mt-3 rounded-md border border-warn-line bg-warn-bg p-3 text-13"}>
              <strong>{i.fit.fits ? "يناسب ما صرّح به المشتري" : "لا يناسب كل ما صرّح به"}</strong>
              {[...i.fit.reasons, ...i.fit.limits].map((x) => <span key={x} className="block">• {x}</span>)}
            </div>
          ) : null}
        </Card>
        <Card title="الفرصة">
          <Link href={`/team/opportunities/${i.opportunity.reference}`} className="text-16 font-bold">{i.opportunity.title}</Link>
          <p className="m-0 mt-1 text-14">{i.opportunity.statusLabel}</p>
          <p className="m-0 mt-3 text-14">أرقام الإصدار {i.terms.versionNo} الذي اهتم به: المطلوب الآن <Amount value={i.terms.dueNow} size="sm" /> · الإجمالي <Amount value={i.terms.buyerTotal} size="sm" /></p>
          {!i.terms.current ? <Badge tone="warn" className="mt-2">تغيّرت أرقام الفرصة بعد هذا الاهتمام</Badge> : null}
          {i.closeReason ? <p className="m-0 mt-2 text-14">سبب الإغلاق: {i.closeReason}</p> : null}
          <p className="m-0 mt-3 text-13 text-muted">المسؤول: {i.assignedTo?.label ?? "بدون"}</p>
        </Card>
      </div>
      <Card title="السجل" actions={i.actions.follow ? <Button size="sm" variant="text" onClick={() => open("note")}>ملاحظة داخلية</Button> : null}><Timeline events={i.events} /></Card>

      <ActionDialog open={dlg === "follow"} onClose={() => setDlg(null)} title="بدء المتابعة" confirm="بدء" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/follow-up`, { note: text })}>
        <ActionError error={error} /><Textarea label="رسالة للمشتري (اختياري)" value={text} onChange={(e) => setText(e.target.value)} rows={2} />
      </ActionDialog>
      <ActionDialog open={dlg === "close"} onClose={() => setDlg(null)} title="إغلاق الاهتمام" confirm="إغلاق" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/close`, { reason: text })}>
        <ActionError error={error} /><Textarea label="السبب (يظهر للمشتري)" value={text} onChange={(e) => setText(e.target.value)} rows={3} />
      </ActionDialog>
      <ActionDialog open={dlg === "note"} onClose={() => setDlg(null)} title="ملاحظة داخلية" confirm="حفظ" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/note`, { note: text })}>
        <ActionError error={error} /><Textarea label="الملاحظة" value={text} onChange={(e) => setText(e.target.value)} rows={3} />
      </ActionDialog>
    </div>
  );
}
