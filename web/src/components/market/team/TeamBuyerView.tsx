"use client";

import Link from "next/link";
import { useState } from "react";
import { ActionDialog, ActionError, useTeamAction } from "@/components/market/team/useTeamAction";
import { Amount, Badge, Card, DemoBadge, StatusBadge, Timeline } from "@/components/market/ui";
import { Button } from "@/components/ui/Button";
import { Textarea, TextField } from "@/components/ui/Field";
import { label } from "@/lib/market/catalog";
import { day } from "@/lib/market/format";
import { toLatinDigits } from "@/lib/market/numbers";
import type { BuyerRequestView, Catalog, MarketEvent } from "@/lib/market/types";

export interface TeamBuyer {
  request: BuyerRequestView;
  phoneMasked: string | null;
  events: MarketEvent[];
  completionRequests: { id: string; note: string; requestedAt: string; requestedByLabel: string; answeredAt: string | null }[];
  interests: { reference: string; status: string; opportunity: string; title: string; createdAt: string }[];
  assignedTo: { id: string; label: string } | null;
  actions: { assign: boolean; assignOthers: boolean; startReview: boolean; requestCompletion: boolean; approve: boolean; reject: boolean; capacity: boolean; financeApproval: boolean; note: boolean };
}

/** Team review of a buyer request: completion, approval for matching, capacity proof review and recorded financing approval — kept apart. */
export function TeamBuyerView({ d, catalog }: { d: TeamBuyer; catalog: Catalog }) {
  const r = d.request;
  const base = `/team/market/buyer-requests/${r.reference}`;
  const { busy, error, setError, run } = useTeamAction();
  const [dlg, setDlg] = useState<null | "completion" | "approve" | "reject" | "capacity" | "finance" | "note">(null);
  const [text, setText] = useState("");
  const [amount, setAmount] = useState("");
  const [fin, setFin] = useState({ status: "pre_approval", source: "", date: "", amount: "" });
  const open = (k: typeof dlg) => { setError(null); setText(""); setDlg(k); };
  const num = (v: string) => (v ? Number(toLatinDigits(v).replace(/[^\d.]/g, "")) : null);
  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <Link href="/team/buyers" className="text-14 font-semibold">← طلبات المشترين</Link>
          <h1 className="m-0 flex flex-wrap items-center gap-2 text-24 font-bold"><bdi dir="ltr" className="font-mono">{r.reference}</bdi><StatusBadge status={r.status} label={r.statusLabel} />{r.isDemo ? <DemoBadge /> : null}</h1>
          <span className="text-15">{r.contactName} · <bdi dir="ltr">{d.phoneMasked}</bdi></span>
        </div>
        <div className="flex flex-wrap gap-2">
          {d.actions.assign ? <Button variant="secondary" onClick={() => void run("assign", "POST", `${base}/assign`, {})}>إسناد لي</Button> : null}
          {d.actions.startReview ? <Button onClick={() => void run("start", "POST", `${base}/start-review`)} loading={busy === "start"}>بدء المراجعة</Button> : null}
          {d.actions.requestCompletion ? <Button variant="secondary" onClick={() => open("completion")}>طلب استكمال</Button> : null}
          {d.actions.approve ? <Button onClick={() => open("approve")}>اعتماد للمطابقة</Button> : null}
          {d.actions.reject ? <Button variant="sensitive" onClick={() => open("reject")}>رفض مسبب</Button> : null}
        </div>
      </div>
      <ActionError error={dlg ? null : error} />
      <div className="grid gap-5 xl:grid-cols-2">
        <Card title="القدرة الشرائية">
          <div className="flex flex-col gap-3">
            <div className="rounded-md bg-subtle p-3">
              <span className="block text-13 font-semibold text-muted">ما صرّح به المشتري</span>
              المتاح الآن <Amount value={r.availableNow} strong /> · القسط <Amount value={r.installmentComfort} size="sm" unknown="—" /> {r.installmentFrequency ? label(catalog.frequencies, r.installmentFrequency) : ""} · الحد الأقصى <Amount value={r.maxPrice} size="sm" unknown="—" />
              <span className="block text-13">طريقة الشراء: {r.purchaseMode === "cash" ? "نقدًا" : r.purchaseMode === "external_finance" ? "تمويل خارجي" : "لم يقرر"}</span>
              {r.preferredFinancierName ? <span className="block text-13">جهة التمويل المفضلة: {r.preferredFinancierName}</span> : null}
            </div>
            <div className="rounded-md bg-subtle p-3">
              <span className="block text-13 font-semibold text-muted">ما راجعه الفريق</span>
              {r.capacity.reviewed ? <><Amount value={r.capacity.reviewed.amount} strong /> <span className="block text-13">{r.capacity.reviewed.note} · {r.capacity.reviewed.by} · {day(r.capacity.reviewed.at)}</span></> : <span className="text-14 text-muted">لم يُراجع.</span>}
              {d.actions.capacity ? <Button size="sm" variant="text" onClick={() => { setAmount(""); open("capacity"); }}>تسجيل مراجعة الإثبات</Button> : null}
            </div>
            <div className="rounded-md bg-subtle p-3">
              <span className="block text-13 font-semibold text-muted">موافقة جهة تمويل (تصدر من الجهة، لا من رهون)</span>
              <span className="text-14">{r.capacity.financeApproval.statusLabel}{r.capacity.financeApproval.source ? ` · ${r.capacity.financeApproval.source} · ${day(r.capacity.financeApproval.date)}` : ""}</span>
              {d.actions.financeApproval ? <Button size="sm" variant="text" onClick={() => open("finance")}>تسجيل حالة الموافقة</Button> : null}
            </div>
          </div>
        </Card>
        <Card title="التفضيلات">
          <div className="flex flex-wrap gap-2">
            {r.cities.map((x) => <Badge key={x}>{catalog.cities.find((y) => y.key === x)?.label ?? x}</Badge>)}
            {r.propertyTypes.map((x) => <Badge key={x} tone="info">{label(catalog.propertyTypes, x)}</Badge>)}
            {r.bedroomsMin ? <Badge>{r.bedroomsMin}+ غرف</Badge> : null}
            {r.areaMin || r.areaMax ? <Badge>{r.areaMin ?? "—"} – {r.areaMax ?? "—"} م²</Badge> : null}
            {r.readiness && r.readiness !== "any" ? <Badge>{r.readiness === "ready" ? "جاهز" : "تحت الإنشاء"}</Badge> : null}
            {r.deliveryBy ? <Badge>قبل {r.deliveryBy}</Badge> : null}
          </div>
          {r.areasText ? <p className="m-0 mt-2 text-14">الأحياء/المشاريع: {r.areasText}</p> : null}
          <h3 className="m-0 mt-4 mb-2 text-15 font-bold">اهتماماته</h3>
          {d.interests.length === 0 ? <p className="m-0 text-14 text-muted">لا توجد.</p> : (
            <ul className="m-0 ps-5 text-14">{d.interests.map((i) => <li key={i.reference}><Link href={`/team/interests/${i.reference}`}>{i.title}</Link> ({i.opportunity})</li>)}</ul>
          )}
        </Card>
      </div>
      <Card title="السجل" actions={d.actions.note ? <Button size="sm" variant="text" onClick={() => open("note")}>ملاحظة داخلية</Button> : null}><Timeline events={d.events} /></Card>

      <ActionDialog open={dlg === "completion"} onClose={() => setDlg(null)} title="طلب استكمال" confirm="إرسال" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/request-completion`, { items: [], note: text })}>
        <ActionError error={error} /><Textarea label="ما المطلوب من المشتري؟" value={text} onChange={(e) => setText(e.target.value)} rows={3} />
      </ActionDialog>
      <ActionDialog open={dlg === "approve"} onClose={() => setDlg(null)} title="اعتماد للمطابقة" confirm="اعتماد" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/approve`, { reason: text })}>
        <ActionError error={error} /><p className="m-0 text-14">الاعتماد يجعل الملف جاهزًا للمطابقة، ولا يعني موافقة تمويل.</p><Textarea label="ملاحظة (اختياري)" value={text} onChange={(e) => setText(e.target.value)} rows={2} />
      </ActionDialog>
      <ActionDialog open={dlg === "reject"} onClose={() => setDlg(null)} title="رفض الطلب" confirm="رفض" tone="sensitive" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/reject`, { reason: text })}>
        <ActionError error={error} /><Textarea label="السبب (يظهر للمشتري)" value={text} onChange={(e) => setText(e.target.value)} rows={3} />
      </ActionDialog>
      <ActionDialog open={dlg === "capacity"} onClose={() => setDlg(null)} title="مراجعة إثبات القدرة" confirm="حفظ" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/capacity-review`, { reviewedAvailableNow: num(amount), note: text })}>
        <ActionError error={error} />
        <TextField label="المبلغ الذي ثبت للفريق" value={amount} onChange={(e) => setAmount(e.target.value)} ltr inputMode="numeric" />
        <Textarea label="ما الذي راجعته (نوع الإثبات وتاريخه)" value={text} onChange={(e) => setText(e.target.value)} rows={2} />
      </ActionDialog>
      <ActionDialog open={dlg === "finance"} onClose={() => setDlg(null)} title="موافقة جهة التمويل" confirm="حفظ" busy={busy === "dlg"}
        onConfirm={() => run("dlg", "POST", `${base}/finance-approval`, { status: fin.status, source: fin.source || null, date: fin.date || null, amount: num(fin.amount) })}>
        <ActionError error={error} />
        <label className="flex flex-col gap-1 text-14 font-semibold">الحالة
          <select className="min-h-11 rounded-sm border border-line-strong bg-white px-3" value={fin.status} onChange={(e) => setFin({ ...fin, status: e.target.value })}>
            <option value="none">لا توجد</option><option value="pre_approval">موافقة مبدئية</option><option value="approved">موافقة تمويل</option>
          </select>
        </label>
        <TextField label="جهة التمويل" value={fin.source} onChange={(e) => setFin({ ...fin, source: e.target.value })} />
        <TextField label="التاريخ" type="date" ltr value={fin.date} onChange={(e) => setFin({ ...fin, date: e.target.value })} />
        <TextField label="المبلغ (اختياري)" ltr inputMode="numeric" value={fin.amount} onChange={(e) => setFin({ ...fin, amount: e.target.value })} />
      </ActionDialog>
      <ActionDialog open={dlg === "note"} onClose={() => setDlg(null)} title="ملاحظة داخلية" confirm="حفظ" busy={busy === "dlg"} onConfirm={() => run("dlg", "POST", `${base}/note`, { note: text })}>
        <ActionError error={error} /><Textarea label="الملاحظة" value={text} onChange={(e) => setText(e.target.value)} rows={3} />
      </ActionDialog>
    </div>
  );
}
