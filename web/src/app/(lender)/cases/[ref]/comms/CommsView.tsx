"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import {
  Alert,
  Button,
  DateField,
  DateText,
  Dialog,
  Icon,
  IconButton,
  KeyValueList,
  RadioCardGroup,
  Select,
  Tabs,
  Tag,
  Textarea,
  TextField,
  useToast,
} from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import {
  APPOINTMENT_STATUS_LABEL,
  APPOINTMENT_TYPE_LABEL,
  CHANNEL_LABEL,
  HARDSHIP_REASON_LABEL,
  HARDSHIP_STATUS_LABEL,
  INVITATION_LABEL,
  PREF_CHANNEL_LABEL,
  type AppointmentDto,
  type CaseCommsData,
  type CaseMessageDto,
  type CaseTaskDto,
  type OrgMember,
} from "@/lib/api/comms";
import { cn } from "@/lib/cn";
import { TIME_ZONE, formatNumber, formatTime } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

type Lane = "owner" | "internal";

interface Props {
  reference: string;
  data: CaseCommsData;
  canSend: boolean;
  canManageTasks: boolean;
  members: OrgMember[];
  myName: string | null;
}

export function CommsView({ reference, data, canSend, canManageTasks, members, myName }: Props) {
  const [lane, setLane] = useState<Lane>("owner");
  const [apptOpen, setApptOpen] = useState(false);
  const ownerMsgs = data.messages.filter((m) => !m.internalOnly);
  const internalMsgs = data.messages.filter((m) => m.internalOnly);
  const shown = lane === "owner" ? ownerMsgs : internalMsgs;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="m-0 flex-1 text-20 font-bold">التواصل والمهام</h2>
        {data.openComplaints > 0 ? (
          <Tag tone="err" icon="support_agent">
            {data.openComplaints === 1 ? "شكوى مفتوحة" : <>شكاوى مفتوحة · <bdi dir="ltr">{formatNumber(data.openComplaints)}</bdi></>}
          </Tag>
        ) : null}
      </div>

      {data.openComplaints > 0 ? (
        <Alert tone="neutral" role="none" icon="support_agent" compact>
          تراجعها جهة مستقلة عن فريق الحالة؛ يظهر لك وسم فقط دون نص الشكوى. تُحجب الإحالة والإلغاء حتى المعالجة.
        </Alert>
      ) : null}

      {data.hardship ? (
        <Alert tone="warn" role="none" icon="volunteer_activism" title="أبلغ المالك عن ظرف طارئ">
          {data.hardship.reasonKey ? HARDSHIP_REASON_LABEL[data.hardship.reasonKey] ?? data.hardship.reasonKey : "لم يُذكر السبب"} ·{" "}
          <DateText value={data.hardship.createdAt} /> · {HARDSHIP_STATUS_LABEL[data.hardship.status] ?? data.hardship.status}
        </Alert>
      ) : null}

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
        <section aria-labelledby="thread-h" className="flex min-w-0 flex-col overflow-hidden rounded-lg border border-line bg-white">
          <h3 id="thread-h" className="sr-only">المراسلات</h3>
          <div className="flex flex-wrap items-center gap-x-3 border-b border-line px-3">
            <Tabs
              label="مسار التواصل"
              bare
              active={lane}
              onChange={(k) => setLane(k as Lane)}
              tabs={[
                { key: "owner", label: "مع المالك", count: ownerMsgs.length, panelId: "lane-panel" },
                { key: "internal", label: "ملاحظات داخلية", count: internalMsgs.length, panelId: "lane-panel" },
              ]}
            />
            <span className="ms-auto flex items-center gap-1 py-2 text-12 text-muted">
              <Icon name="history" size={16} />
              كل رسالة تُحفظ في سجل الحالة
            </span>
          </div>

          <div id="lane-panel" role="tabpanel" aria-label={lane === "owner" ? "مع المالك" : "ملاحظات داخلية"} className={cn("flex flex-col gap-3 p-4", lane === "internal" && "bg-warm")}>
            {lane === "internal" ? (
              <p className="m-0 flex items-center gap-1.5 text-13 text-muted">
                <Icon name="visibility_off" size={16} />
                ملاحظات لفريق الحالة فقط — لا تظهر للمالك ولا تُرسل له.
              </p>
            ) : null}
            {shown.length === 0 ? (
              <p role="status" className="m-0 rounded-md border border-dashed border-line-strong p-4 text-14 text-muted">
                {lane === "owner" ? "لا مراسلات مع المالك بعد." : "لا ملاحظات داخلية بعد."}
              </p>
            ) : (
              <ol className="m-0 flex list-none flex-col gap-3 p-0" aria-label={lane === "owner" ? "رسائل مع المالك" : "ملاحظات داخلية"}>
                {shown.map((m) => (
                  <MessageBubble key={m.id} m={m} templates={data.templates} />
                ))}
              </ol>
            )}
          </div>

          {canSend ? (
            <Composer
              key={lane}
              reference={reference}
              lane={lane}
              templates={data.templates}
              onPropose={() => setApptOpen(true)}
            />
          ) : (
            <p className="m-0 border-t border-divider px-4 py-3 text-13 text-muted">للقراءة فقط — دورك لا يتيح مراسلة المالك.</p>
          )}
        </section>

        <aside className="flex flex-col gap-4">
          <AppointmentsCard items={data.appointments} canPropose={canSend} onPropose={() => setApptOpen(true)} />
          <TasksCard reference={reference} tasks={data.tasks} canManage={canManageTasks} members={members} myName={myName} />
          <PreferencesCard reference={reference} prefs={data.preferences} />
        </aside>
      </div>

      {canSend ? <AppointmentDialog reference={reference} open={apptOpen} onClose={() => setApptOpen(false)} /> : null}
    </div>
  );
}

/* ───────── Thread ───────── */

function MessageBubble({ m, templates }: { m: CaseMessageDto; templates: CaseCommsData["templates"] }) {
  const fromOwner = m.authorType === "owner";
  const template = m.templateKey ? templates.find((t) => t.code === m.templateKey)?.title ?? m.templateKey : null;
  const meta = [
    template ? `قالب «${template}»` : null,
    !m.internalOnly && !fromOwner && m.readByOwner ? "قرأها المالك" : null,
  ].filter(Boolean);
  return (
    <li className={cn("flex", fromOwner ? "justify-end" : "justify-start")}>
      <article
        className={cn(
          "flex max-w-[92%] flex-col gap-1.5 rounded-md border px-4 py-3 sm:max-w-[78%]",
          fromOwner ? "border-rust-200 bg-rust-50" : m.internalOnly ? "border-dashed border-line-strong bg-white" : "border-line bg-warm",
        )}
      >
        <header className="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-12 text-muted">
          <strong className="text-14 text-ink">{m.author}</strong>
          <DateText value={m.at} mode="datetime" />
          <span>· {m.internalOnly ? "ملاحظة داخلية" : CHANNEL_LABEL[m.channel] ?? m.channel}</span>
        </header>
        <p className="m-0 text-15 leading-7 whitespace-pre-wrap">{m.body}</p>
        {meta.length ? <span className="text-12 text-muted">{meta.join(" · ")}</span> : null}
      </article>
    </li>
  );
}

function Composer({ reference, lane, templates, onPropose }: { reference: string; lane: Lane; templates: CaseCommsData["templates"]; onPropose: () => void }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [body, setBody] = useState("");
  const [channel, setChannel] = useState<"portal" | "call">("portal");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const internal = lane === "internal";

  const update = (v: string) => {
    setBody(v);
    key.reset();
  };

  const send = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", `/cases/${encodeURIComponent(reference)}/comms/messages`, { body: body.trim(), internal, channel: internal ? null : channel }, { idempotencyKey: key.get() });
      key.reset();
      setBody("");
      toast.toast({ tone: "ok", message: internal ? "حُفظت الملاحظة الداخلية." : channel === "call" ? "سُجّلت المكالمة في سجل الحالة." : "أُرسلت الرسالة للمالك." });
      router.refresh();
    } catch (e) {
      setError(isApiError(e) ? e.fieldError("body") ?? e.title : "تعذّر الإرسال.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-3 border-t border-line p-4">
      {error ? <Alert tone="err" title={error} /> : null}
      <div className="flex flex-wrap items-center gap-2">
        {!internal && templates.length > 0 ? (
          <Button variant="secondary" size="sm" icon="text_snippet" onClick={() => setPickerOpen(true)}>قالب</Button>
        ) : null}
        {!internal ? <Button variant="secondary" size="sm" icon="event" onClick={onPropose}>اقتراح موعد</Button> : null}
        <span className="ms-auto text-12 text-muted">
          {internal ? "لا تظهر للمالك" : channel === "call" ? "تُسجَّل كمكالمة هاتفية في سجل الحالة" : "يُرسل عبر: البوابة + إشعار في حساب المالك"}
        </span>
      </div>
      {!internal ? (
        <Select
          label="القناة"
          value={channel}
          onChange={(e) => {
            setChannel(e.target.value as "portal" | "call");
            key.reset();
          }}
          options={[
            { value: "portal", label: "رسالة عبر البوابة" },
            { value: "call", label: "تسجيل مكالمة هاتفية" },
          ]}
          containerClassName="max-w-[280px]"
        />
      ) : null}
      <Textarea
        label={internal ? "ملاحظة داخلية" : channel === "call" ? "ملخص المكالمة" : "رسالة للمالك"}
        value={body}
        onChange={(e) => update(e.target.value)}
        maxLength={4000}
        rows={3}
        placeholder={internal ? "اكتب ملاحظة لفريق الحالة…" : "اكتب رسالة للمالك…"}
      />
      <div className="flex justify-end">
        <Button iconEnd="send" iconEndMirror onClick={() => void send()} loading={busy} disabled={body.trim().length === 0}>
          {internal ? "حفظ الملاحظة" : "إرسال"}
        </Button>
      </div>
      <TemplatePicker
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        templates={templates}
        onPick={(text) => {
          update(body.trim() ? `${body.trimEnd()}\n${text}` : text);
          setPickerOpen(false);
        }}
      />
    </div>
  );
}

function TemplatePicker({ open, onClose, templates, onPick }: { open: boolean; onClose: () => void; templates: CaseCommsData["templates"]; onPick: (text: string) => void }) {
  return (
    <Dialog open={open} onClose={onClose} title="إدراج قالب" description="قوالب منشورة معتمدة بلغة داعمة. يُدرج النص في الرسالة ويمكنك تعديله قبل الإرسال." size="lg">
      <ul className="m-0 flex list-none flex-col gap-2 p-5">
        {templates.map((t) => (
          <li key={t.code}>
            <button
              type="button"
              onClick={() => onPick(t.bodyAr)}
              className="flex w-full flex-col gap-1 rounded-md border border-line bg-white p-3 text-start hover:bg-warm"
            >
              <span className="flex flex-wrap items-center gap-2">
                <strong className="text-15">{t.title}</strong>
                <bdi dir="ltr" className="font-mono text-12 text-muted">{t.code}</bdi>
              </span>
              <span className="line-clamp-3 text-13 leading-5 text-muted">{t.bodyAr}</span>
            </button>
          </li>
        ))}
      </ul>
    </Dialog>
  );
}

/* ───────── Appointments ───────── */

function dayMonth(iso: string, locale: string) {
  const d = new Date(iso);
  const day = new Intl.DateTimeFormat("en-GB", { day: "numeric", timeZone: TIME_ZONE }).format(d);
  const month = new Intl.DateTimeFormat(locale === "en" ? "en-GB" : "ar-SA-u-ca-gregory", { month: "long", timeZone: TIME_ZONE }).format(d);
  return { day, month };
}

function AppointmentsCard({ items, canPropose, onPropose }: { items: AppointmentDto[]; canPropose: boolean; onPropose: () => void }) {
  const { locale, numerals } = useI18n();
  return (
    <section aria-labelledby="appt-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
      <div className="flex items-center justify-between gap-2">
        <h3 id="appt-h" className="m-0 text-16 font-semibold">المواعيد</h3>
        {canPropose ? <Button variant="text" size="sm" icon="event" onClick={onPropose}>اقتراح موعد</Button> : null}
      </div>
      {items.length === 0 ? <p className="m-0 text-14 text-muted">لا مواعيد مقترحة.</p> : null}
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {items.map((a) => {
          const dm = dayMonth(a.startsAt, locale);
          return (
            <li key={a.id} className={cn("flex items-start gap-3", a.status === "Cancelled" && "opacity-70")}>
              <span className="flex w-14 flex-none flex-col items-center rounded-md border border-line bg-warm py-1.5">
                <bdi dir="ltr" className="text-20 leading-7 font-bold tabular-nums">{dm.day}</bdi>
                <span className="text-12 text-muted">{dm.month}</span>
              </span>
              <div className="flex min-w-0 flex-col gap-0.5 text-14">
                <strong>{APPOINTMENT_TYPE_LABEL[a.type] ?? a.type}{a.proposedBy === "owner" ? " · اقترحه المالك" : ""}</strong>
                <span className="text-13 text-muted">
                  <bdi dir="ltr" className="tabular-nums">{formatTime(a.startsAt, { numerals })}</bdi>
                  {a.attendees ? ` · ${a.attendees}` : ""}
                </span>
                <span className={cn("text-13", a.status === "Confirmed" ? "text-ok" : a.status === "Proposed" ? "text-warn" : "text-muted")}>
                  {APPOINTMENT_STATUS_LABEL[a.status] ?? a.status}
                </span>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

function AppointmentDialog({ reference, open, onClose }: { reference: string; open: boolean; onClose: () => void }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [type, setType] = useState<string | null>(null);
  const [date, setDate] = useState("");
  const [time, setTime] = useState("");
  const [attendees, setAttendees] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ title: string; fields: Record<string, string | undefined> } | null>(null);

  const change = <T,>(set: (v: T) => void) => (v: T) => {
    set(v);
    key.reset();
  };

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", `/cases/${encodeURIComponent(reference)}/comms/appointments`,
        { type, startsAt: `${date}T${time}:00+03:00`, attendees: attendees.trim() || null }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: "أُرسل الموعد المقترح للمالك لتأكيده." });
      setType(null);
      setDate("");
      setTime("");
      setAttendees("");
      onClose();
      router.refresh();
    } catch (e) {
      setError(isApiError(e)
        ? { title: e.title || "تعذّر اقتراح الموعد.", fields: { type: e.fieldError("type"), startsAt: e.fieldError("startsAt") } }
        : { title: "تعذّر اقتراح الموعد.", fields: {} });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="اقتراح موعد"
      description="يصل المقترح للمالك في البوابة ليؤكده. الوقت بتوقيت الرياض."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void submit()} loading={busy} disabled={!type || !date || !time}>إرسال المقترح</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {error ? <Alert tone="err" title={error.title} /> : null}
        <RadioCardGroup
          legend="نوع الموعد"
          name="appt-type"
          columns={2}
          value={type}
          onChange={change(setType)}
          error={error?.fields.type}
          options={[
            { value: "call", label: "مكالمة" },
            { value: "visit", label: "زيارة" },
          ]}
        />
        <div className="grid gap-4 sm:grid-cols-2">
          <DateField label="التاريخ" requiredMark value={date} onValueChange={change(setDate)} error={error?.fields.startsAt} />
          <TextField label="الوقت" requiredMark type="time" ltr value={time} onChange={(e) => change(setTime)(e.target.value)} />
        </div>
        <TextField label="الحضور" optionalMark value={attendees} onChange={(e) => change(setAttendees)(e.target.value)} maxLength={200} help="مثال: بحضور ابن المالك" />
      </div>
    </Dialog>
  );
}

/* ───────── Tasks ───────── */

function TasksCard({ reference, tasks, canManage, members, myName }: { reference: string; tasks: CaseTaskDto[]; canManage: boolean; members: OrgMember[]; myName: string | null }) {
  const router = useRouter();
  const toast = useToast();
  const [createOpen, setCreateOpen] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const visible = tasks.filter((t) => t.status !== "Cancelled");

  const complete = async (t: CaseTaskDto) => {
    setBusyId(t.id);
    try {
      await apiSend("POST", `/tasks/${t.id}/complete`, {});
      toast.toast({ tone: "ok", message: `اكتملت المهمة: ${t.title}` });
      router.refresh();
    } catch (e) {
      toast.toast({ tone: "err", message: isApiError(e) && e.title ? e.title : "تعذّر إكمال المهمة." });
    } finally {
      setBusyId(null);
    }
  };

  return (
    <section aria-labelledby="tasks-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
      <div className="flex items-center justify-between gap-2">
        <h3 id="tasks-h" className="m-0 text-16 font-semibold">مهام الحالة</h3>
        {canManage ? <Button variant="secondary" size="sm" icon="add" onClick={() => setCreateOpen(true)}>مهمة</Button> : null}
      </div>
      {visible.length === 0 ? <p className="m-0 text-14 text-muted">لا مهام على الحالة.</p> : null}
      <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
        {visible.map((t) => {
          const done = t.status === "Done";
          const canComplete = !done && (canManage || (myName !== null && t.assignee === myName));
          return (
            <li key={t.id} className="flex items-start gap-2">
              <Icon name={done ? "task_alt" : "radio_button_unchecked"} size={20} className={done ? "text-ok" : "text-muted"} />
              <div className="flex min-w-0 flex-1 flex-col text-14">
                <span className={cn(done && "text-muted line-through")}>{t.title}</span>
                <span className="text-12 text-muted">
                  {t.assignee ?? "غير مسندة"}
                  {done ? " · مكتملة" : t.dueOn ? <> · <DateText value={t.dueOn} /></> : null}
                </span>
              </div>
              {canComplete ? (
                <IconButton icon="check" label={`تعليم «${t.title}» كمكتملة`} size={36} disabled={busyId === t.id} onClick={() => void complete(t)} />
              ) : null}
            </li>
          );
        })}
      </ul>
      {canManage ? <CreateTaskDialog reference={reference} open={createOpen} onClose={() => setCreateOpen(false)} members={members} /> : null}
    </section>
  );
}

function CreateTaskDialog({ reference, open, onClose, members }: { reference: string; open: boolean; onClose: () => void; members: OrgMember[] }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [title, setTitle] = useState("");
  const [assignee, setAssignee] = useState("");
  const [due, setDue] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", "/tasks", { caseReference: reference, title: title.trim(), assigneeMembershipId: assignee || null, dueOn: due || null }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: "أُنشئت المهمة." });
      setTitle("");
      setAssignee("");
      setDue("");
      onClose();
      router.refresh();
    } catch (e) {
      setError(isApiError(e) ? e.fieldError("title") ?? e.title : "تعذّر إنشاء المهمة.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="مهمة جديدة على الحالة"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void submit()} loading={busy} disabled={title.trim().length === 0}>إنشاء المهمة</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {error ? <Alert tone="err" title={error} /> : null}
        <TextField label="عنوان المهمة" requiredMark value={title} maxLength={200} onChange={(e) => { setTitle(e.target.value); key.reset(); }} />
        <Select
          label="المسؤول"
          value={assignee}
          onChange={(e) => { setAssignee(e.target.value); key.reset(); }}
          options={[{ value: "", label: "أنا" }, ...members.map((m) => ({ value: m.id, label: m.team ? `${m.name} · ${m.team}` : m.name }))]}
        />
        <DateField label="تاريخ الاستحقاق" value={due} onValueChange={(v) => { setDue(v); key.reset(); }} />
      </div>
    </Dialog>
  );
}

/* ───────── Contact preferences ───────── */

function PreferencesCard({ reference, prefs }: { reference: string; prefs: CaseCommsData["preferences"] }) {
  const channels = prefs.channels?.length ? prefs.channels.map((c) => PREF_CHANNEL_LABEL[c] ?? c).join("، ") : "غير محددة";
  return (
    <section aria-labelledby="pref-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
      <div className="flex items-center justify-between gap-2">
        <h3 id="pref-h" className="m-0 text-16 font-semibold">تفضيلات التواصل</h3>
        <Link href={`/cases/${reference}/parties`} className="text-13 font-semibold">تعديل في الأطراف</Link>
      </div>
      <KeyValueList
        dense
        rows={[
          { key: "أوقات التواصل", value: prefs.contactHours ?? "غير محددة" },
          { key: "القنوات المسموحة", value: channels },
          { key: "دعوة المالك", value: prefs.invitation ? INVITATION_LABEL[prefs.invitation] ?? prefs.invitation : "لم تُرسل" },
        ]}
      />
      <p className="m-0 flex gap-1.5 text-12 leading-5 text-muted">
        <Icon name="schedule_send" size={16} />
        راسل المالك واقترح المواعيد ضمن أوقات التواصل المفضلة له.
      </p>
    </section>
  );
}
