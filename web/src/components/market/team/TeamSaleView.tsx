"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { LocationMap } from "@/components/market/LocationMap";
import { ActionDialog, ActionError, useTeamAction } from "@/components/market/team/useTeamAction";
import { TermsBreakdown } from "@/components/market/TermsBreakdown";
import { Badge, Card, DemoBadge, StatusBadge, Timeline } from "@/components/market/ui";
import { Button } from "@/components/ui/Button";
import { Checkbox, Textarea, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { apiSend } from "@/lib/api/client";
import { label, obligationFields, propertyFields, UNKNOWN } from "@/lib/market/catalog";
import { day, dayTime } from "@/lib/market/format";
import type { Catalog, MarketEvent, SaleFile } from "@/lib/market/types";

export interface TeamSaleDetail {
  file: SaleFile;
  events: MarketEvent[];
  applicant: { name: string | null; phoneMasked: string | null; email: string | null; relationship: string | null; declarationsAcceptedAt: string | null; declarationsVersion: string | null };
  assignedTo: { id: string; label: string } | null;
  decisionReason: string | null;
  ownerMarkedCompleteAt: string | null;
  verifications: { fieldKey: string; label: string; value: string; source: string; sourceLabel: string; sourceDocumentId: string | null; sourceDate: string; note: string | null; verifiedByLabel: string; verifiedAt: string }[];
  approvals: { obligationId: string; partyName: string; kind: string; current: Approval | null; history: Approval[] }[];
  completionRequests: { id: string; items: { key: string; label: string }[]; note: string; requestedAt: string; requestedByLabel: string; answeredAt: string | null }[];
  opportunities: { reference: string; status: string; statusLabel: string }[];
  itemOptions: { key: string; label: string }[];
  verifiableKeys: { key: string; label: string; current: string | null; type: string; options: { value: string; label: string }[] | null }[];
  actions: { assign: boolean; assignOthers: boolean; startReview: boolean; requestCompletion: boolean; approve: boolean; reject: boolean; verify: boolean; correct: boolean; reviewFiles: boolean; externalApproval: boolean; createOpportunity: boolean; note: boolean; openDocuments: boolean; contact: boolean };
}
interface Approval { id: string; status: string; statusLabel: string; conditions: string | null; documentId: string | null; decisionDate: string | null; expiresOn: string | null; note: string | null; recordedByLabel: string; recordedAt: string }

const SOURCES = [
  { value: "developer_statement", label: "كشف المطور" }, { value: "financier_statement", label: "كشف جهة التمويل" }, { value: "payoff_letter", label: "خطاب مبلغ السداد" },
  { value: "contract", label: "العقد" }, { value: "title_deed", label: "الصك" }, { value: "owner_declared", label: "إقرار صاحب العقار" }, { value: "other", label: "مصدر آخر" },
];
const APPROVAL = [
  { value: "NotRequested", label: "لم تُطلب" }, { value: "Requested", label: "قيد الطلب" }, { value: "Conditional", label: "مشروطة" },
  { value: "Approved", label: "موافق عليها" }, { value: "Rejected", label: "مرفوضة" }, { value: "Expired", label: "انتهت صلاحيتها" },
];
const sel = "min-h-11 w-full rounded-sm border border-line-strong bg-white px-3 text-15";

function show(v: string | undefined, f?: { type: string; options?: { value: string; label: string }[] | null }) {
  if (v === undefined || v === "") return "—";
  if (v === UNKNOWN) return "لا أعرف";
  if (f?.type === "boolean") return v === "true" ? "نعم" : "لا";
  if ((f?.type === "select" || f?.type === "multiSelect") && f.options) return v.split(",").map((x) => label(f.options!, x)).join("، ");
  if (f?.type === "money") return `${Number(v).toLocaleString("en-US")} ر.س`;
  return v;
}

/** Team review of one sale request: assignment, completion requests, figure verification with source, corrections, file review, approvals. */
export function TeamSaleView({ d, catalog, members }: { d: TeamSaleDetail; catalog: Catalog; members: { id: string; name: string; role: string | null }[] }) {
  const f = d.file;
  const router = useRouter();
  const { busy, error, setError, run } = useTeamAction();
  const base = `/team/market/sale-requests/${f.reference}`;
  const [dlg, setDlg] = useState<null | "completion" | "approve" | "reject" | "verify" | "correct" | "approval" | "note" | "fileReject">(null);
  const [rejectFile, setRejectFile] = useState<{ kind: "documents" | "photos"; id: string } | null>(null);
  const [items, setItems] = useState<string[]>([]);
  const [text, setText] = useState("");
  const [vf, setVf] = useState({ fieldKey: "", value: "", source: "developer_statement", sourceDocumentId: "", sourceDate: "", note: "" });
  const [ap, setAp] = useState({ obligationId: "", status: "Requested", conditions: "", documentId: "", decisionDate: "", expiresOn: "", note: "" });
  const [phone, setPhone] = useState<string | null>(null);
  const open = (k: typeof dlg) => { setError(null); setText(""); setDlg(k); };
  const verified = (key: string) => d.verifications.find((v) => v.fieldKey === key);

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <Link href="/team/sale" className="text-14 font-semibold">← طلبات البيع</Link>
          <h1 className="m-0 flex flex-wrap items-center gap-2 text-24 font-bold">
            <bdi dir="ltr" className="font-mono">{f.reference}</bdi>
            <StatusBadge status={f.status} label={f.statusLabel} />
            {f.isDemo ? <DemoBadge /> : null}
          </h1>
          <span className="text-15 text-charcoal">{label(catalog.propertyTypes, f.propertyType)} · {catalog.cities.find((c) => c.key === f.city)?.label}{f.district ? `، ${f.district}` : ""}{f.project ? ` · ${f.project}` : ""}</span>
        </div>
        <div className="flex flex-wrap gap-2">
          {d.actions.startReview ? <Button onClick={() => void run("start", "POST", `${base}/start-review`)} loading={busy === "start"}>بدء المراجعة</Button> : null}
          {d.actions.requestCompletion ? <Button variant="secondary" onClick={() => { setItems([]); open("completion"); }}>طلب استكمال</Button> : null}
          {d.actions.approve ? <Button onClick={() => open("approve")}>اعتماد لإعداد فرصة</Button> : null}
          {d.actions.reject ? <Button variant="sensitive" onClick={() => open("reject")}>رفض مسبب</Button> : null}
          {d.actions.createOpportunity ? (
            <Button onClick={async () => {
              const res = await apiSend<{ reference: string }>("POST", `${base}/opportunity`).catch(() => null);
              if (res) router.push(`/team/opportunities/${res.reference}`);
            }}>تجهيز الفرصة</Button>
          ) : null}
          {d.opportunities.map((o) => <Link key={o.reference} href={`/team/opportunities/${o.reference}`} className="inline-flex min-h-10 items-center rounded-sm border border-line px-3 text-14">الفرصة {o.reference}: {o.statusLabel}</Link>)}
        </div>
      </div>
      <ActionError error={dlg ? null : error} />

      <div className="grid gap-5 xl:grid-cols-[minmax(0,1.5fr)_minmax(320px,1fr)]">
        <div className="flex flex-col gap-5">
          <Card title="ما ينقص الملف">
            <ul className="m-0 grid list-none gap-3 p-0 md:grid-cols-2">
              {f.completeness.map((g) => (
                <li key={g.key} className="rounded-md border border-line p-3">
                  <span className="flex items-center gap-2 font-semibold"><Icon name={g.done ? "check_circle" : "pending"} size={20} className={g.done ? "text-ok" : "text-warn"} />{g.label}</span>
                  {g.missing.length ? <ul className="m-0 mt-1 ps-6 text-13 text-charcoal">{g.missing.map((m) => <li key={m.key}>{m.label}</li>)}</ul> : null}
                </li>
              ))}
            </ul>
          </Card>

          <Card title="العقار والموقع">
            <dl className="m-0 grid gap-x-6 gap-y-2 sm:grid-cols-2">
              {propertyFields(catalog, f.propertyType, f.answers).filter((x) => f.answers[x.key]).map((x) => (
                <div key={x.key}><dt className="text-13 text-muted">{x.label}</dt><dd className="m-0 font-semibold">{show(f.answers[x.key], x)}{x.unit && f.answers[x.key] !== UNKNOWN ? ` ${x.unit}` : ""}</dd></div>
              ))}
            </dl>
            {f.location.lat !== null && f.location.lng !== null ? (
              <div className="mt-4 flex flex-col gap-2">
                <span className="text-13 text-muted">الموقع الحقيقي (للفريق فقط) · رغبة المالك في العرض: {f.location.displayWish === "exact" ? "الموقع الدقيق" : "منطقة تقريبية"}</span>
                <LocationMap lat={f.location.lat} lng={f.location.lng} precision="exact" height={220} />
              </div>
            ) : <p className="m-0 mt-3 text-14 text-warn">لم يحدد المالك الموقع بعد.</p>}
          </Card>

          {f.obligations.map((o) => (
            <Card key={o.id} title={<span className="flex items-center gap-2">{o.kind === "developer" ? "الالتزام لدى المطور" : "التمويل"} <Badge>{o.partyName}</Badge></span>}>
              {o.relationNote ? <p className="m-0 mb-3 text-14">علاقته بالالتزامات الأخرى: {o.relationNote}</p> : null}
              <ul className="m-0 flex list-none flex-col p-0">
                {obligationFields(catalog, o.kind, o.answers).map((x) => {
                  const key = `o:${o.id}:${x.key}`;
                  const v = verified(key);
                  return (
                    <li key={x.key} className="flex flex-wrap items-center justify-between gap-2 border-b border-divider py-2 last:border-b-0">
                      <span className="text-14">{x.label}</span>
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="font-semibold">{show(o.answers[x.key], x)}</span>
                        {v ? <Badge tone="ok" icon="verified">{v.sourceLabel} · {day(v.sourceDate)}{v.value !== o.answers[x.key] ? ` (${show(v.value, x)})` : ""}</Badge> : null}
                        {d.actions.verify && x.publishKnown ? (
                          <button type="button" className="text-13 font-semibold text-rust" onClick={() => { setVf({ fieldKey: key, value: o.answers[x.key] && o.answers[x.key] !== UNKNOWN ? o.answers[x.key] : "", source: o.kind === "developer" ? "developer_statement" : "payoff_letter", sourceDocumentId: "", sourceDate: "", note: "" }); open("verify"); }}>
                            اعتماد الرقم
                          </button>
                        ) : null}
                        {d.actions.correct ? (
                          <button type="button" className="text-13 text-muted underline" onClick={() => { setVf({ ...vf, fieldKey: key, value: o.answers[x.key] ?? "" }); open("correct"); }}>تصحيح</button>
                        ) : null}
                      </span>
                    </li>
                  );
                })}
              </ul>
            </Card>
          ))}

          <Card title="المستندات الخاصة">
            {f.documents.length === 0 ? <p className="m-0 text-14 text-muted">لم تُرفع مستندات.</p> : (
              <ul className="m-0 flex list-none flex-col gap-2 p-0">
                {f.documents.map((doc) => (
                  <li key={doc.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md bg-subtle px-3 py-2">
                    <span className="flex flex-col">
                      {d.actions.openDocuments ? (
                        <a href={doc.url} target="_blank" rel="noopener" className="font-semibold">{doc.kindLabel}</a>
                      ) : (
                        <span className="font-semibold" title="فتح المستند يحتاج صلاحية المستندات لهذا الملف">{doc.kindLabel}</span>
                      )}
                      <span className="text-12 text-muted">{doc.fileName} · {dayTime(doc.uploadedAt)}</span>
                      {doc.reviewNote ? <span className="text-12">ملاحظة: {doc.reviewNote}</span> : null}
                    </span>
                    <span className="flex items-center gap-2">
                      <Badge tone={doc.reviewStatus === "accepted" ? "ok" : doc.reviewStatus === "rejected" ? "err" : "info"}>{doc.reviewStatus === "accepted" ? "مقبول" : doc.reviewStatus === "rejected" ? "مرفوض" : "بانتظار المراجعة"}</Badge>
                      {d.actions.reviewFiles ? (
                        <>
                          <Button size="sm" variant="secondary" loading={busy === `doc-ok-${doc.id}`} onClick={() => void run(`doc-ok-${doc.id}`, "POST", `${base}/documents/${doc.id}/review`, { status: "accepted" })}>قبول</Button>
                          <Button size="sm" variant="text" onClick={() => { setRejectFile({ kind: "documents", id: doc.id }); open("fileReject"); }}>رفض</Button>
                        </>
                      ) : null}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </Card>

          <Card title="صور العرض">
            {f.photos.length === 0 ? <p className="m-0 text-14 text-muted">لا توجد صور.</p> : (
              <ul className="m-0 grid list-none grid-cols-2 gap-3 p-0 md:grid-cols-4">
                {f.photos.map((p) => (
                  <li key={p.id} className="flex flex-col gap-1 rounded-md border border-line p-1.5">
                    {/* eslint-disable-next-line @next/next/no-img-element -- private review image */}
                    <img src={p.url} alt="" className="aspect-[4/3] w-full rounded-sm object-cover" />
                    <span className="flex flex-wrap items-center gap-1">
                      {p.isCover ? <Badge tone="rust">غلاف</Badge> : null}
                      <Badge tone={p.reviewStatus === "accepted" ? "ok" : p.reviewStatus === "rejected" ? "err" : "info"}>{p.reviewStatus === "accepted" ? "مقبولة" : p.reviewStatus === "rejected" ? "مرفوضة" : "للمراجعة"}</Badge>
                    </span>
                    {d.actions.reviewFiles ? (
                      <span className="flex gap-1">
                        <Button size="sm" variant="secondary" onClick={() => void run(`ph-${p.id}`, "POST", `${base}/photos/${p.id}/review`, { status: "accepted" })}>قبول</Button>
                        <Button size="sm" variant="text" onClick={() => { setRejectFile({ kind: "photos", id: p.id }); open("fileReject"); }}>رفض</Button>
                      </span>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}
          </Card>

          <Card title="موافقات المطور / جهة التمويل (منفصلة عن اعتماد رهون)">
            <ul className="m-0 flex list-none flex-col gap-3 p-0">
              {d.approvals.map((a) => (
                <li key={a.obligationId} className="flex flex-col gap-1 rounded-md border border-line p-3">
                  <span className="flex flex-wrap items-center justify-between gap-2">
                    <strong>{a.partyName}</strong>
                    <Badge tone={a.current?.status === "approved" ? "ok" : a.current?.status === "conditional" ? "info" : "neutral"}>{a.current?.statusLabel ?? "لم تُطلب"}</Badge>
                  </span>
                  {a.current?.conditions ? <span className="text-13">الشروط: {a.current.conditions}</span> : null}
                  {a.history.length > 1 ? <span className="text-12 text-muted">{a.history.length} قيود في السجل</span> : null}
                  {d.actions.externalApproval ? (
                    <button type="button" className="self-start text-13 font-semibold text-rust" onClick={() => { setAp({ obligationId: a.obligationId, status: "Requested", conditions: "", documentId: "", decisionDate: "", expiresOn: "", note: "" }); open("approval"); }}>تسجيل حالة</button>
                  ) : null}
                </li>
              ))}
            </ul>
          </Card>
        </div>

        <aside className="flex flex-col gap-5">
          <Card title="صاحب الطلب">
            <dl className="m-0 flex flex-col gap-2 text-14">
              <div><dt className="text-muted">الاسم</dt><dd className="m-0 font-semibold">{d.applicant.name ?? "—"}</dd></div>
              <div><dt className="text-muted">الجوال</dt><dd className="m-0 flex items-center gap-2"><bdi dir="ltr" className="font-mono">{phone ?? d.applicant.phoneMasked}</bdi>
                {!phone && d.actions.contact ? <button type="button" className="text-13 text-rust" onClick={async () => { const r = await apiSend<{ phone: string }>("GET", `${base}/contact`).catch(() => null); if (r) setPhone(r.phone); }}>إظهار (يُسجل)</button> : null}</dd></div>
              {d.applicant.email ? <div><dt className="text-muted">البريد</dt><dd className="m-0"><bdi dir="ltr">{d.applicant.email}</bdi></dd></div> : null}
              <div><dt className="text-muted">العلاقة المقرّة</dt><dd className="m-0">{d.applicant.relationship === "owner" ? "صاحب العقار" : d.applicant.relationship === "authorized" ? "مخوّل" : "—"} {d.applicant.declarationsAcceptedAt ? `· ${day(d.applicant.declarationsAcceptedAt)}` : ""}</dd></div>
            </dl>
          </Card>
          <Card title="المسؤول">
            <p className="m-0 mb-2 text-15">{d.assignedTo?.label ?? "بدون مسؤول"}</p>
            {d.actions.assign ? (
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="secondary" onClick={() => void run("assign", "POST", `${base}/assign`, {})}>إسناد لي</Button>
                {d.actions.assignOthers ? (
                  <select aria-label="إسناد إلى" className={`${sel} w-auto`} defaultValue="" onChange={(e) => e.target.value && void run("assign", "POST", `${base}/assign`, { userId: e.target.value })}>
                    <option value="">إسناد إلى…</option>
                    {members.map((m) => <option key={m.id} value={m.id}>{m.name}{m.role ? ` (${m.role})` : ""}</option>)}
                  </select>
                ) : null}
              </div>
            ) : null}
          </Card>
          <Card title="التقدير الأولي">
            <TermsBreakdown result={f.estimate} audience="team" />
          </Card>
          {d.completionRequests.length ? (
            <Card title="طلبات الاستكمال">
              <ul className="m-0 flex list-none flex-col gap-3 p-0 text-14">
                {d.completionRequests.map((c) => (
                  <li key={c.id} className="rounded-md bg-subtle p-3">
                    <span className="block font-semibold">{c.note}</span>
                    <span className="block text-13">{c.items.map((i) => i.label).join("، ")}</span>
                    <span className="block text-12 text-muted">{c.requestedByLabel} · {day(c.requestedAt)} · {c.answeredAt ? `أُجيب ${day(c.answeredAt)}` : "مفتوح"}</span>
                  </li>
                ))}
              </ul>
            </Card>
          ) : null}
          <Card title="السجل" actions={d.actions.note ? <Button size="sm" variant="text" onClick={() => open("note")}>ملاحظة داخلية</Button> : null}>
            <Timeline events={d.events} />
          </Card>
        </aside>
      </div>

      <ActionDialog open={dlg === "completion"} onClose={() => setDlg(null)} title="طلب استكمال محدد" confirm="إرسال لصاحب الطلب" busy={busy === "dlg"}
        onConfirm={() => run("dlg", "POST", `${base}/request-completion`, { items, note: text })}>
        <ActionError error={error} />
        <fieldset className="m-0 flex max-h-64 flex-col gap-1 overflow-y-auto border-0 p-0">
          <legend className="mb-2 text-14 font-semibold">ما الذي ينقص؟</legend>
          {d.itemOptions.map((o) => (
            <Checkbox key={o.key} checked={items.includes(o.key)} onChange={(e) => setItems((x) => (e.target.checked ? [...x, o.key] : x.filter((y) => y !== o.key)))} label={o.label} />
          ))}
        </fieldset>
        <Textarea label="ملاحظة واضحة لصاحب الطلب" value={text} onChange={(e) => setText(e.target.value)} rows={3} />
      </ActionDialog>
      <ActionDialog open={dlg === "approve"} onClose={() => setDlg(null)} title="اعتماد الطلب لإعداد فرصة" confirm="اعتماد" busy={busy === "dlg"}
        onConfirm={() => run("dlg", "POST", `${base}/approve`, { reason: text })}>
        <ActionError error={error} />
        <p className="m-0 text-14">الاعتماد لا ينشر شيئًا: بعده تجهز الفرصة ويؤكدها المالك ثم تُنشر.</p>
        <Textarea label="ملاحظة (اختياري)" value={text} onChange={(e) => setText(e.target.value)} rows={2} />
      </ActionDialog>
      <ActionDialog open={dlg === "reject"} onClose={() => setDlg(null)} title="رفض الطلب" confirm="رفض" tone="sensitive" busy={busy === "dlg"}
        onConfirm={() => run("dlg", "POST", `${base}/reject`, { reason: text })}>
        <ActionError error={error} />
        <Textarea label="السبب (يظهر لصاحب الطلب)" value={text} onChange={(e) => setText(e.target.value)} rows={3} />
      </ActionDialog>
      <ActionDialog open={dlg === "verify"} onClose={() => setDlg(null)} title="اعتماد رقم مع مصدره وتاريخه" confirm="اعتماد الرقم" busy={busy === "dlg"}
        onConfirm={() => run("dlg", "POST", `${base}/verify-figure`, { fieldKey: vf.fieldKey, value: vf.value, source: vf.source, sourceDocumentId: vf.sourceDocumentId || null, sourceDate: vf.sourceDate || null, note: vf.note || null })}>
        <ActionError error={error} />
        <span className="text-14 font-semibold">{d.verifiableKeys.find((k) => k.key === vf.fieldKey)?.label}</span>
        <TextField label="القيمة كما في المستند" value={vf.value} onChange={(e) => setVf({ ...vf, value: e.target.value })} ltr />
        <label className="flex flex-col gap-1 text-14 font-semibold">المصدر
          <select className={sel} value={vf.source} onChange={(e) => setVf({ ...vf, source: e.target.value })}>{SOURCES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}</select>
        </label>
        <label className="flex flex-col gap-1 text-14 font-semibold">المستند
          <select className={sel} value={vf.sourceDocumentId} onChange={(e) => setVf({ ...vf, sourceDocumentId: e.target.value })}>
            <option value="">بدون مستند مرفق</option>
            {f.documents.map((doc) => <option key={doc.id} value={doc.id}>{doc.kindLabel} — {doc.fileName}</option>)}
          </select>
        </label>
        <TextField label="تاريخ المستند" type="date" ltr value={vf.sourceDate} onChange={(e) => setVf({ ...vf, sourceDate: e.target.value })} />
        <TextField label="ملاحظة (اختياري)" value={vf.note} onChange={(e) => setVf({ ...vf, note: e.target.value })} />
      </ActionDialog>
      <ActionDialog open={dlg === "correct"} onClose={() => setDlg(null)} title="تصحيح معلومة" confirm="حفظ التصحيح" busy={busy === "dlg"}
        onConfirm={() => run("dlg", "POST", `${base}/correct`, { fieldKey: vf.fieldKey, value: vf.value, reason: text })}>
        <ActionError error={error} />
        <TextField label="القيمة الصحيحة (فارغ = حذف)" value={vf.value} onChange={(e) => setVf({ ...vf, value: e.target.value })} ltr />
        <Textarea label="سبب التصحيح (يظهر لصاحب الطلب)" value={text} onChange={(e) => setText(e.target.value)} rows={2} />
      </ActionDialog>
      <ActionDialog open={dlg === "approval"} onClose={() => setDlg(null)} title="موافقة المطور أو جهة التمويل" confirm="تسجيل" busy={busy === "dlg"}
        onConfirm={() => run("dlg", "POST", `${base}/external-approvals`, { obligationId: ap.obligationId, status: ap.status, conditions: ap.conditions || null, documentId: ap.documentId || null, decisionDate: ap.decisionDate || null, expiresOn: ap.expiresOn || null, note: ap.note || null })}>
        <ActionError error={error} />
        <label className="flex flex-col gap-1 text-14 font-semibold">الحالة
          <select className={sel} value={ap.status} onChange={(e) => setAp({ ...ap, status: e.target.value })}>{APPROVAL.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}</select>
        </label>
        <Textarea label="الشروط كما في مستند الجهة" value={ap.conditions} onChange={(e) => setAp({ ...ap, conditions: e.target.value })} rows={2} />
        <label className="flex flex-col gap-1 text-14 font-semibold">مستند الجهة
          <select className={sel} value={ap.documentId} onChange={(e) => setAp({ ...ap, documentId: e.target.value })}>
            <option value="">—</option>
            {f.documents.map((doc) => <option key={doc.id} value={doc.id}>{doc.kindLabel} — {doc.fileName}</option>)}
          </select>
        </label>
        <div className="grid grid-cols-2 gap-3">
          <TextField label="تاريخ القرار" type="date" ltr value={ap.decisionDate} onChange={(e) => setAp({ ...ap, decisionDate: e.target.value })} />
          <TextField label="صالحة حتى" type="date" ltr value={ap.expiresOn} onChange={(e) => setAp({ ...ap, expiresOn: e.target.value })} />
        </div>
      </ActionDialog>
      <ActionDialog open={dlg === "fileReject"} onClose={() => setDlg(null)} title={rejectFile?.kind === "photos" ? "عدم قبول الصورة" : "عدم قبول المستند"} confirm="حفظ القرار" tone="sensitive" busy={busy === "dlg"}
        onConfirm={() => run("dlg", "POST", `${base}/${rejectFile?.kind}/${rejectFile?.id}/review`, { status: "rejected", note: text })}>
        <ActionError error={error} />
        <Textarea label="السبب (يظهر لصاحب الطلب ليعرف المطلوب)" value={text} onChange={(e) => setText(e.target.value)} rows={3} />
      </ActionDialog>
      <ActionDialog open={dlg === "note"} onClose={() => setDlg(null)} title="ملاحظة داخلية (لا تظهر للعميل)" confirm="حفظ" busy={busy === "dlg"}
        onConfirm={() => run("dlg", "POST", `${base}/note`, { note: text })}>
        <ActionError error={error} />
        <Textarea label="الملاحظة" value={text} onChange={(e) => setText(e.target.value)} rows={3} />
      </ActionDialog>
    </div>
  );
}
