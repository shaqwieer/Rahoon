"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useId, useState } from "react";
import { RevealParty } from "@/components/case/OverviewActions";
import {
  Alert,
  Button,
  Checkbox,
  DateField,
  Dialog,
  EmptyState,
  Icon,
  KeyValueList,
  Select,
  Textarea,
  TextField,
  useToast,
} from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey, type ApiError } from "@/lib/api/client";
import type { ContactPreferencesDto, InvitationResult, NoteTone, PartiesData, PartyDto, PartyRoleKey } from "@/lib/api/caseInfo";
import { cn } from "@/lib/cn";

const NOTE_TONE: Record<NoteTone, string> = { ok: "text-ok", warn: "text-warn", info: "text-info", muted: "text-muted", err: "text-err" };

function errorOf(e: unknown, fallback: string): ApiError | { title: string; reasons?: string[]; fieldError: (f: string) => string | undefined } {
  if (isApiError(e)) return e.title ? e : Object.assign(e, { title: fallback });
  return { title: fallback, fieldError: () => undefined };
}

function isoInDays(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toLocaleDateString("en-CA", { timeZone: "Asia/Riyadh" });
}

export function PartiesView({ reference, data }: { reference: string; data: PartiesData }) {
  const [editing, setEditing] = useState<PartyDto | "new" | null>(null);
  const [poaFor, setPoaFor] = useState<PartyDto | null>(null);
  // Remount key bumped on open only, so a dialog starts fresh but closes through dialog.close() (focus returns to the opener).
  const [seq, setSeq] = useState(0);
  const openEdit = (v: PartyDto | "new") => {
    setSeq((n) => n + 1);
    setEditing(v);
  };
  const openPoa = (v: PartyDto) => {
    setSeq((n) => n + 1);
    setPoaFor(v);
  };

  return (
    <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
      <section aria-labelledby="parties-h" className="flex min-w-0 flex-col gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <h2 id="parties-h" className="m-0 flex-1 text-20 font-bold">
            الأطراف (<bdi dir="ltr">{data.count}</bdi>)
          </h2>
          {data.canAdd ? (
            <Button variant="secondary" icon="person_add" onClick={() => openEdit("new")}>
              إضافة طرف
            </Button>
          ) : null}
        </div>

        {data.items.length === 0 ? (
          <EmptyState icon="group" title="لا أطراف مسجلة" body="لم يُسجَّل أي طرف لهذه الحالة بعد." />
        ) : (
          <ul className="m-0 flex list-none flex-col gap-3 p-0">
            {data.items.map((p) => (
              <li key={p.id}>
                <PartyCard reference={reference} party={p} onEdit={() => openEdit(p)} onPoa={() => openPoa(p)} />
              </li>
            ))}
          </ul>
        )}
      </section>

      <aside aria-label="التواصل مع المالك" className="flex flex-col gap-4">
        <ContactCard reference={reference} contact={data.contact} canEdit={data.canAdd} />
        {data.contact.communicationNeeds ? (
          <Alert tone="warn" icon="diversity_1" role="none">
            <strong>احتياج تواصل:</strong> {data.contact.communicationNeeds}
          </Alert>
        ) : null}
        <Alert tone="neutral" icon="shield_person" role="none" compact>
          {data.revealNote}
        </Alert>
      </aside>

      <PartyDialog
        key={`party-${seq}`}
        reference={reference}
        party={editing}
        roles={data.addableRoles}
        onClose={() => setEditing(null)}
      />
      <PoaDialog key={`poa-${seq}`} reference={reference} party={poaFor} onClose={() => setPoaFor(null)} />
    </div>
  );
}

/* ───────── Party card ───────── */

function PartyCard({ reference, party: p, onEdit, onPoa }: { reference: string; party: PartyDto; onEdit: () => void; onPoa: () => void }) {
  const titleId = useId();
  const idFactKeys = ["الهوية", "السجل"];
  return (
    <article aria-labelledby={titleId} className="grid grid-cols-[48px_minmax(0,1fr)] gap-x-4 gap-y-3 rounded-lg border border-line bg-white p-4 lg:grid-cols-[48px_minmax(0,1fr)_auto] lg:p-5">
      <span
        aria-hidden="true"
        className={cn("grid size-12 place-items-center rounded-full text-15 font-semibold", p.avatar === "primary" ? "bg-ink text-white" : "bg-subtle text-ink")}
      >
        {p.initials}
      </span>
      <div className="flex min-w-0 flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <strong id={titleId} className="text-17">{p.name}</strong>
          <span className="rounded-pill border border-line bg-warm px-2.5 py-0.5 text-12 font-semibold">{p.roleLabel}</span>
          {!p.financialVisibility ? (
            <span className="inline-flex items-center gap-1 text-12 text-muted">
              <Icon name="lock" size={14} />
              لا يطّلع على البيانات المالية
            </span>
          ) : null}
        </div>
        <dl className="m-0 grid grid-cols-1 gap-x-6 gap-y-2.5 text-14 sm:grid-cols-2 xl:grid-cols-3">
          {p.facts.map((f) => (
            <div key={f.k} className="flex flex-col gap-0.5">
              <dt className="text-13 text-muted">{f.k}</dt>
              <dd className="m-0 flex flex-wrap items-center gap-1">
                {idFactKeys.includes(f.k) && p.actions.reveal && p.nationalIdMasked ? (
                  <RevealParty reference={reference} partyId={p.id} masked={p.nationalIdMasked} label="" />
                ) : f.ltr ? (
                  <bdi dir="ltr" className="font-mono">{f.v}</bdi>
                ) : (
                  f.v
                )}
              </dd>
            </div>
          ))}
        </dl>
        {p.note ? (
          <p className={cn("m-0 flex items-start gap-1.5 text-14", NOTE_TONE[p.note.tone] ?? "text-muted")}>
            <Icon name={p.note.icon} size={18} />
            <span>{p.note.text}</span>
          </p>
        ) : null}
      </div>
      <div className="col-span-2 flex flex-wrap items-start gap-2 lg:col-span-1 lg:flex-col lg:items-stretch">
        {p.actions.message ? (
          <Button variant="secondary" size="sm" icon="chat" href={`/cases/${reference}/comms?party=${p.id}`}>
            مراسلة
          </Button>
        ) : null}
        {p.actions.requestPoa ? (
          <Button variant="secondary" size="sm" icon="gavel" onClick={onPoa}>
            طلب وكالة
          </Button>
        ) : null}
        {p.actions.edit ? (
          <Button variant={p.actions.message || p.actions.requestPoa ? "text" : "secondary"} size="sm" icon="edit" onClick={onEdit}>
            تعديل
          </Button>
        ) : null}
      </div>
    </article>
  );
}

/* ───────── Contact preferences + invitation ───────── */

function ContactCard({ reference, contact: c, canEdit }: { reference: string; contact: ContactPreferencesDto; canEdit: boolean }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const reasonId = useId();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState<InvitationResult | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [seq, setSeq] = useState(0);
  const inv = c.invitation;

  const invite = async () => {
    setBusy(true);
    setError(null);
    try {
      const res = await apiSend<InvitationResult>("POST", `/cases/${reference}/owner-invitation`, {}, { idempotencyKey: key.get() });
      key.reset();
      setSent(res);
      toast.toast({ tone: "ok", message: res.refreshed ? "جُدّدت الدعوة وأُبطل الرابط السابق." : "أُرسلت الدعوة للمالك." });
      router.refresh();
    } catch (e) {
      key.reset();
      setError(errorOf(e, "تعذّر إرسال الدعوة.").title);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section aria-labelledby="contact-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
      <div className="flex items-center gap-2">
        <h2 id="contact-h" className="m-0 flex-1 text-16 font-semibold">التواصل مع المالك</h2>
        {canEdit ? (
          <Button variant="text" size="sm" icon="edit" onClick={() => { setSeq((n) => n + 1); setEditOpen(true); }}>
            تعديل التفضيلات
          </Button>
        ) : null}
      </div>
      <KeyValueList
        dense
        rows={[
          { key: "الدعوة", value: <bdi dir="auto">{inv.label}</bdi> },
          { key: "التحقق من الهوية", value: c.identityVerification.label },
          { key: "القنوات المسموحة", value: c.allowedChannelsLabel },
          { key: "أوقات التواصل", value: c.contactHours ?? "غير محددة" },
          { key: "آخر تواصل", value: c.lastContactAt ? <bdi dir="ltr">{c.lastContactLabel}</bdi> : c.lastContactLabel },
        ]}
      />
      {inv.canInvite || inv.disabledReason ? (
        <div className="flex flex-col gap-1.5 border-t border-divider pt-3">
          <Button
            variant="secondary"
            icon="send"
            loading={busy}
            softDisabled={!inv.canInvite}
            aria-describedby={!inv.canInvite ? reasonId : undefined}
            onClick={() => void invite()}
          >
            {inv.actionLabel}
          </Button>
          {!inv.canInvite && inv.disabledReason ? (
            <span id={reasonId} className="text-12 text-muted">معطّل: {inv.disabledReason}</span>
          ) : (
            <span className="text-12 text-muted">رسالة نصية برابط صالح 14 يوماً؛ التجديد يُبطل الرابط السابق.</span>
          )}
        </div>
      ) : null}
      {error ? <Alert tone="err" title={error} compact /> : null}
      {sent ? (
        <div className="flex flex-col gap-2">
          <Alert tone="ok" compact role="status">
            أُرسلت الدعوة إلى <bdi dir="ltr">{sent.destination}</bdi> · صالحة حتى <bdi dir="ltr">{sent.expiresAt.slice(0, 10)}</bdi>
            {sent.smsState === "simulated" ? " · الرسالة النصية محاكاة ولا تُرسل فعلياً." : null}
          </Alert>
          {sent.devLink ? (
            <div role="note" className="flex flex-col gap-1 rounded-md border border-dashed border-info-line bg-info-bg px-3 py-2.5 text-13">
              <span className="flex items-center gap-1.5 font-semibold text-info">
                <Icon name="science" size={18} />
                رابط تطويري — بيئة تجريبية فقط، لا يظهر في الإنتاج
              </span>
              <Link href={sent.devLink} className="break-all font-mono" dir="ltr">
                {sent.devLink}
              </Link>
            </div>
          ) : null}
        </div>
      ) : null}
      <PreferencesDialog key={`prefs-${seq}`} reference={reference} contact={c} open={editOpen} onClose={() => setEditOpen(false)} />
    </section>
  );
}

function PreferencesDialog({ reference, contact: c, open, onClose }: { reference: string; contact: ContactPreferencesDto; open: boolean; onClose: () => void }) {
  const router = useRouter();
  const toast = useToast();
  const [channels, setChannels] = useState<string[]>(c.allowedChannels);
  const [hours, setHours] = useState(c.contactHours ?? "");
  const [needs, setNeeds] = useState(c.communicationNeeds ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ReturnType<typeof errorOf> | null>(null);

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("PUT", `/cases/${reference}/contact-preferences`, { allowedChannels: channels, contactHours: hours.trim(), communicationNeeds: needs.trim() });
      toast.toast({ tone: "ok", message: "حُفظت تفضيلات التواصل." });
      onClose();
      router.refresh();
    } catch (e) {
      setError(errorOf(e, "تعذّر حفظ التفضيلات."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="تفضيلات التواصل مع المالك"
      description="تُطبَّق على كل الرسائل والمواعيد؛ يُسجَّل التعديل في سجل الحالة."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void save()} loading={busy} disabled={channels.length === 0}>حفظ</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {error ? <Alert tone="err" title={error.title} /> : null}
        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-2 text-14 font-semibold">القنوات المسموحة</legend>
          {c.channelOptions.map((o) => (
            <Checkbox
              key={o.key}
              label={o.label}
              checked={channels.includes(o.key)}
              onChange={(e) => setChannels((prev) => (e.target.checked ? [...prev, o.key] : prev.filter((x) => x !== o.key)))}
            />
          ))}
          {error?.fieldError("allowedChannels") ? <span className="text-13 text-err">{error.fieldError("allowedChannels")}</span> : null}
        </fieldset>
        <TextField label="أوقات التواصل" value={hours} onChange={(e) => setHours(e.target.value)} maxLength={100} placeholder="مثال: 9 ص – 5 م، أيام العمل" error={error?.fieldError("contactHours")} />
        <Textarea
          label="احتياجات التواصل"
          value={needs}
          onChange={(e) => setNeeds(e.target.value)}
          maxLength={500}
          help="تظهر بشكل بارز لفريق الحالة، مثل حضور أحد الأقارب في المكالمات."
          error={error?.fieldError("communicationNeeds")}
        />
      </div>
    </Dialog>
  );
}

/* ───────── Add / edit party ───────── */

interface PartyForm {
  role: string;
  kind: "individual" | "organization";
  fullName: string;
  nationalId: string;
  phone: string;
  email: string;
  language: string;
  relation: string;
  isContractParty: boolean;
  contactAllowed: boolean;
  employmentStatus: string;
  specialNeeds: string;
  notes: string;
}

function initialForm(p: PartyDto | null): PartyForm {
  return {
    role: p?.role ?? "",
    kind: p?.kind ?? "individual",
    fullName: "",
    nationalId: "",
    phone: "",
    email: "",
    language: p?.language ?? "ar",
    relation: p?.relation ?? "",
    isContractParty: p?.isContractParty ?? false,
    contactAllowed: p?.contactAllowed ?? true,
    employmentStatus: p?.employmentStatus ?? "",
    specialNeeds: p?.specialNeeds ?? "",
    notes: p?.notes ?? "",
  };
}

function PartyDialog({ reference, party, roles, onClose }: {
  reference: string;
  party: PartyDto | "new" | null;
  roles: Array<{ key: PartyRoleKey; label: string }>;
  onClose: () => void;
}) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const existing = party && party !== "new" ? party : null;
  const isNew = party === "new";
  const [f, setF] = useState<PartyForm>(() => initialForm(existing));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ReturnType<typeof errorOf> | null>(null);
  const primary = existing?.isPrimary ?? false;
  const set = <K extends keyof PartyForm>(k: K, v: PartyForm[K]) => {
    key.reset();
    setF((prev) => ({ ...prev, [k]: v }));
  };

  /** PATCH carries only the fields the user changed; blank identity fields mean «unchanged». */
  const patchBody = () => {
    const base = initialForm(existing);
    const body: Record<string, unknown> = {};
    if (!primary && f.role !== base.role) body.role = f.role;
    if (!primary && f.fullName.trim()) body.fullName = f.fullName.trim();
    if (!primary && f.phone.trim()) body.phone = f.phone.trim();
    if (!primary && !existing?.nationalIdMasked && f.nationalId.trim()) body.nationalId = f.nationalId.trim();
    if (f.email.trim()) body.email = f.email.trim();
    for (const k of ["language", "relation", "employmentStatus", "specialNeeds", "notes"] as const) if (f[k] !== base[k]) body[k] = f[k];
    if (!primary && f.isContractParty !== base.isContractParty) body.isContractParty = f.isContractParty;
    if (f.contactAllowed !== base.contactAllowed) body.contactAllowed = f.contactAllowed;
    return body;
  };

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      if (isNew) {
        await apiSend("POST", `/cases/${reference}/parties`, {
          role: f.role, kind: f.kind, fullName: f.fullName.trim(), nationalId: f.nationalId.trim() || null, phone: f.phone.trim() || null,
          email: f.email.trim() || null, language: f.language, relation: f.relation.trim() || null, isContractParty: f.isContractParty,
          contactAllowed: f.contactAllowed, employmentStatus: f.employmentStatus.trim() || null, specialNeeds: f.specialNeeds.trim() || null,
          notes: f.notes.trim() || null,
        }, { idempotencyKey: key.get() });
        key.reset();
        toast.toast({ tone: "ok", message: "أُضيف الطرف وسُجّل ذلك في السجل." });
      } else if (existing) {
        const body = patchBody();
        if (Object.keys(body).length === 0) {
          onClose();
          return;
        }
        const res = await apiSend<{ changed: string[] }>("PATCH", `/cases/${reference}/parties/${existing.id}`, body);
        toast.toast({ tone: "ok", message: res.changed.length ? `حُفظ التعديل: ${res.changed.join("، ")}` : "لا تغييرات." });
      }
      onClose();
      router.refresh();
    } catch (e) {
      key.reset();
      setError(errorOf(e, "تعذّر حفظ بيانات الطرف."));
    } finally {
      setBusy(false);
    }
  };

  const needsId = f.role === "co_borrower" || f.role === "guarantor";
  const needsRelation = f.role === "occupant" || f.role === "informal_representative";
  const blankHint = "اتركه فارغاً للإبقاء على القيمة الحالية.";

  return (
    <Dialog
      open={party !== null}
      onClose={onClose}
      size="lg"
      title={isNew ? "إضافة طرف" : `تعديل بيانات ${existing?.name ?? ""}`}
      description="البيانات الشخصية تُحفظ مشفرة وتُعرض مخفية؛ يُسجَّل كل تعديل بأسماء الحقول دون قيمها."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void save()} loading={busy} disabled={isNew && (!f.role || f.fullName.trim().length < 3)}>
            {isNew ? "إضافة الطرف" : "حفظ التعديل"}
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 p-5 sm:grid-cols-2">
        {error ? (
          <Alert tone="err" title={error.title} className="sm:col-span-2">
            {error.fieldError("primary")}
          </Alert>
        ) : null}
        {primary ? (
          <Alert tone="info" role="none" compact className="sm:col-span-2">
            اسم المالك الأساسي وهويته وجواله لا تُعدّل من هنا؛ اطلب تصحيح البيانات من المصدر.
          </Alert>
        ) : null}
        {!primary ? (
          <Select
            label="الدور"
            requiredMark
            placeholder="اختر الدور"
            value={f.role}
            onChange={(e) => set("role", e.target.value)}
            options={roles.map((r) => ({ value: r.key, label: r.label }))}
            error={error?.fieldError("role")}
          />
        ) : null}
        {isNew ? (
          <Select
            label="نوع الطرف"
            value={f.kind}
            onChange={(e) => set("kind", e.target.value as PartyForm["kind"])}
            options={[{ value: "individual", label: "فرد" }, { value: "organization", label: "منشأة" }]}
          />
        ) : null}
        {!primary ? (
          <TextField
            label="الاسم كما في الهوية"
            requiredMark={isNew}
            value={f.fullName}
            onChange={(e) => set("fullName", e.target.value)}
            maxLength={150}
            help={isNew ? undefined : blankHint}
            error={error?.fieldError("fullName")}
            containerClassName="sm:col-span-2"
          />
        ) : null}
        {!primary && (isNew || !existing?.nationalIdMasked) ? (
          <TextField
            label={f.kind === "organization" ? "رقم السجل" : "رقم الهوية"}
            requiredMark={isNew && needsId}
            optionalMark={!needsId}
            ltr
            mono
            inputMode="numeric"
            maxLength={10}
            value={f.nationalId}
            onChange={(e) => set("nationalId", e.target.value)}
            help={isNew ? (needsId ? "مطلوب للمقترض المشارك والكفيل." : undefined) : "يُضاف مرة واحدة ولا يُعدّل بعد ذلك."}
            error={error?.fieldError("nationalId")}
          />
        ) : null}
        {!primary ? (
          <TextField
            label="الجوال"
            optionalMark
            ltr
            inputMode="tel"
            value={f.phone}
            onChange={(e) => set("phone", e.target.value)}
            placeholder={existing?.phoneMasked ?? "05xxxxxxxx"}
            help={isNew ? undefined : blankHint}
            error={error?.fieldError("phone")}
          />
        ) : null}
        <TextField
          label="البريد الإلكتروني"
          optionalMark
          ltr
          type="email"
          value={f.email}
          onChange={(e) => set("email", e.target.value)}
          placeholder={existing?.emailMasked ?? undefined}
          help={isNew ? undefined : blankHint}
          error={error?.fieldError("email")}
        />
        <Select
          label="لغة التواصل"
          value={f.language}
          onChange={(e) => set("language", e.target.value)}
          options={[{ value: "ar", label: "العربية" }, { value: "en", label: "الإنجليزية" }]}
          error={error?.fieldError("language")}
        />
        {!primary ? (
          <TextField
            label="العلاقة بالمالك أو بالعقار"
            requiredMark={isNew && needsRelation}
            optionalMark={!needsRelation}
            value={f.relation}
            onChange={(e) => set("relation", e.target.value)}
            maxLength={120}
            placeholder="مثال: ابن المالك"
            error={error?.fieldError("relation")}
          />
        ) : null}
        <TextField
          label="الحالة الوظيفية"
          optionalMark
          value={f.employmentStatus}
          onChange={(e) => set("employmentStatus", e.target.value)}
          maxLength={120}
          error={error?.fieldError("employmentStatus")}
        />
        <div className="flex flex-col gap-3 sm:col-span-2">
          {!primary ? (
            <Checkbox label="طرف في العقد" checked={f.isContractParty} onChange={(e) => set("isContractParty", e.target.checked)} />
          ) : null}
          <Checkbox
            label="التواصل مسموح"
            description="عند المنع لا تُرسل رسائل لهذا الطرف ولا تُشارك معه بيانات مالية."
            checked={f.contactAllowed}
            onChange={(e) => set("contactAllowed", e.target.checked)}
          />
        </div>
        <Textarea
          label="احتياجات خاصة"
          optionalMark
          value={f.specialNeeds}
          onChange={(e) => set("specialNeeds", e.target.value)}
          maxLength={300}
          error={error?.fieldError("specialNeeds")}
          containerClassName="sm:col-span-2"
        />
        <Textarea
          label="ملاحظات"
          optionalMark
          value={f.notes}
          onChange={(e) => set("notes", e.target.value)}
          maxLength={500}
          error={error?.fieldError("notes")}
          containerClassName="sm:col-span-2"
        />
      </div>
    </Dialog>
  );
}

/* ───────── POA request ───────── */

function PoaDialog({ reference, party, onClose }: { reference: string; party: PartyDto | null; onClose: () => void }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [due, setDue] = useState(() => isoInDays(10));
  const [channels, setChannels] = useState<string[]>(["portal", "sms"]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ReturnType<typeof errorOf> | null>(null);

  const submit = async () => {
    if (!party) return;
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", `/cases/${reference}/parties/${party.id}/poa-request`, { dueOn: due, channels }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: "أُرسل طلب الوكالة الموثقة وأُضيف إلى المستندات المطلوبة." });
      onClose();
      router.refresh();
    } catch (e) {
      key.reset();
      setError(errorOf(e, "تعذّر إرسال طلب الوكالة."));
    } finally {
      setBusy(false);
    }
  };

  const channel = (k: string, label: string) => (
    <Checkbox
      label={label}
      checked={channels.includes(k)}
      onChange={(e) => {
        key.reset();
        setChannels((prev) => (e.target.checked ? [...prev, k] : prev.filter((x) => x !== k)));
      }}
    />
  );

  return (
    <Dialog
      open={party !== null}
      onClose={onClose}
      title="طلب وكالة موثقة"
      description={party ? `يُطلب من المالك رفع وكالة موثقة لـ${party.name} لمنحه صلاحية الاطلاع على البيانات المالية. لا تُشارك معه بيانات مالية قبل التحقق منها.` : undefined}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void submit()} loading={busy} disabled={!due || channels.length === 0}>إرسال الطلب</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {error ? <Alert tone="err" title={error.title} /> : null}
        <DateField label="المهلة" requiredMark value={due} onValueChange={(v) => { key.reset(); setDue(v); }} error={error?.fieldError("dueOn")} />
        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-2 text-14 font-semibold">قنوات الإشعار</legend>
          {channel("portal", "بوابة المالك")}
          {channel("sms", "رسالة نصية")}
        </fieldset>
        <p className="m-0 text-13 text-muted">يُصاغ نص الطلب من قالب طلب المستندات المعتمد، ويُسجَّل في سجل الحالة.</p>
      </div>
    </Dialog>
  );
}
