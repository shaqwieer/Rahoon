"use client";

import Link from "next/link";
import { useEffect, useState, type ReactNode } from "react";
import { requestCopy, type RequestStatusKey } from "@/components/individual/copy";
import { useTeamCopy } from "@/components/team/TeamShell";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Dialog, Drawer } from "@/components/ui/Dialog";
import { Checkbox, Select, Textarea, TextField } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { KeyValueList } from "@/components/ui/KeyValueList";
import { Tag } from "@/components/ui/Status";
import { Tabs } from "@/components/ui/Tabs";
import { apiSend } from "@/lib/api/client";
import type { CoordinationItem, TeamMember, TeamRequestDetail } from "@/lib/api/team";
import { cn } from "@/lib/cn";
import { formatDate, formatDateTime, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { TeamStatusTag, TimerTag } from "../../TeamQueueView";
import { useTeamAction } from "./teamActions";

type DialogKey = null | "assign" | "identity" | "info" | "coord" | "notEligible" | "update" | "upload";

/** Actions this screen performs in step 6; offer/response actions arrive with step 7. */
const HANDLED = new Set(["pick_up", "request_info", "start_coordination", "not_eligible"]);

export function TeamRequestView({ detail }: { detail: TeamRequestDetail }) {
  const c = useTeamCopy();
  const R = c.review;
  const { locale, numerals } = useI18n();
  const fmt = { locale, numerals };
  const ind = requestCopy(locale);
  const [dialog, setDialog] = useState<DialogKey>(null);
  const [correcting, setCorrecting] = useState<CoordinationItem | null>(null);
  const [tab, setTab] = useState<"coordination" | "timeline" | "messages">("coordination");
  const act = useTeamAction();
  const base = `/team/requests/${encodeURIComponent(detail.reference)}`;
  const f = detail.fields;
  const terminal = detail.status === "closed" || detail.status === "not_eligible" || detail.status === "withdrawn";
  const actions = detail.actions.filter((a) => HANDLED.has(a.key));

  const runAction = (key: string) => {
    if (key === "request_info") setDialog("info");
    else if (key === "not_eligible") setDialog("notEligible");
    else if (key === "pick_up") void act.run("POST", `${base}/pick-up`, { nextStep: null });
    else if (key === "start_coordination") void act.run("POST", `${base}/start-coordination`, { nextStep: null });
  };

  const card = (title: ReactNode, children: ReactNode, extra?: ReactNode, id?: string) => (
    <section aria-labelledby={id} className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id={id} className="m-0 text-17 font-bold">
          {title}
        </h2>
        {extra}
      </div>
      {children}
    </section>
  );

  return (
    <div className="flex flex-col gap-5">
      <Link href="/team" className="inline-flex min-h-10 items-center gap-1 self-start text-14 font-semibold">
        <Icon name="arrow_forward" size={18} mirror />
        {R.back}
      </Link>

      <header className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="m-0 font-mono text-24 font-bold">
            <bdi dir="ltr">{detail.reference}</bdi>
          </h1>
          <TeamStatusTag status={detail.status} />
          <span className="text-14 text-muted">
            {ind.waitingLabel}: <strong className="text-ink">{c.waiting[detail.waitingOn]}</strong>
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-14">
          <span>
            {R.assignedTo}: <strong>{detail.assigned?.name ?? R.notAssigned}</strong>
          </span>
          {detail.can.take ? (
            <Button size="sm" variant="secondary" loading={act.busy} onClick={() => void act.run("POST", `${base}/take`)}>
              {R.take}
            </Button>
          ) : null}
          {detail.can.assign && !terminal ? (
            <Button size="sm" variant="text" onClick={() => setDialog("assign")}>
              {R.assign}
            </Button>
          ) : null}
          <span className="inline-flex items-center gap-2" title={c.internal}>
            <span className="text-muted">{R.timer}:</span>
            <TimerTag timer={detail.timer} />
            <Tag tone="neutral" icon="lock">
              {c.internal}
            </Tag>
          </span>
        </div>
      </header>

      {act.error ? <Alert tone="err">{act.error}</Alert> : null}
      {!detail.consentActive && !terminal ? <Alert tone="warn">{R.consentNone}</Alert> : null}

      <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_360px]">
        <div className="flex min-w-0 flex-col gap-5">
          {card(
            R.applicant,
            <KeyValueList
              rows={[
                { key: R.applicant, value: detail.applicant.name ?? "—" },
                { key: R.idLabel, value: <bdi dir="ltr" className="font-mono">{detail.applicant.idMasked}</bdi> },
                { key: R.phoneLabel, value: <bdi dir="ltr" className="font-mono">{detail.applicant.phoneMasked}</bdi> },
                {
                  key: R.identity,
                  value: detail.applicant.identityCheck ? (
                    <span className="flex flex-col">
                      <span className="inline-flex items-center gap-1 font-semibold text-ok">
                        <Icon name="verified" size={18} />
                        {R.identityChecked(detail.applicant.identityCheck.by ?? "—")}
                      </span>
                      <span className="text-13 text-muted">{detail.applicant.identityCheck.note}</span>
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 text-warn">
                      <Icon name="gpp_maybe" size={18} />
                      {R.identitySelf}
                    </span>
                  ),
                },
              ]}
            />,
            detail.can.identityCheck ? (
              <Button size="sm" variant="secondary" onClick={() => setDialog("identity")}>
                {R.identityCheck}
              </Button>
            ) : null,
            "applicant-h",
          )}

          {card(
            R.declared,
            <>
              <KeyValueList
                rows={[
                  { key: R.lender, value: detail.institutionListed ? detail.institutionName : `${detail.institutionName} · ${R.lenderOther}` },
                  { key: R.contract, value: f.contractNumber ? <bdi dir="ltr" className="font-mono">{f.contractNumber}</bdi> : "—" },
                  { key: R.installment, value: f.monthlyInstallment === null ? "—" : formatMoney(f.monthlyInstallment, fmt) },
                  { key: R.arrears, value: f.arrearsDuration ? (ind.wizard.finance.arrearsOptions as Record<string, string>)[f.arrearsDuration] : "—" },
                  { key: R.city, value: f.propertyCity ?? "—" },
                  { key: R.preference, value: f.pathPreference ? (ind.wizard.situation.options as Record<string, string>)[f.pathPreference] : "—" },
                  ...(f.affordableMonthly !== null ? [{ key: R.affordable, value: formatMoney(f.affordableMonthly, fmt) }] : []),
                  ...(f.situationText ? [{ key: R.words, value: <span className="whitespace-pre-line">{f.situationText}</span> }] : []),
                ]}
              />
              {detail.duplicateOf ? <Alert tone="info">{R.duplicateOf(detail.duplicateOf)}</Alert> : null}
            </>,
            <Tag tone="neutral" icon="person">
              {R.sourceClient}
            </Tag>,
            "declared-h",
          )}

          {card(
            R.documents,
            detail.documents.length === 0 ? (
              <p className="m-0 text-14 text-muted">{R.noDocuments}</p>
            ) : (
              <ul className="m-0 flex list-none flex-col divide-y divide-divider p-0">
                {detail.documents.map((d) => (
                  <li key={d.id} className="flex flex-wrap items-center gap-3 py-2.5">
                    <Icon name={d.kind === "lender_letter" ? "mail" : "description"} size={22} className="text-muted" />
                    <div className="flex min-w-0 flex-1 flex-col">
                      {d.versionId ? (
                        <a href={`/api${base}/documents/${d.versionId}/file`} className="truncate text-14 font-semibold">
                          {d.name}
                        </a>
                      ) : (
                        <span className="text-14">{d.name}</span>
                      )}
                      <span className="text-12 text-muted">
                        {d.uploadedBy ?? "—"} · {d.uploadedAt ? <bdi dir="ltr">{formatDate(d.uploadedAt, fmt)}</bdi> : "—"}
                        {d.addedAfterSubmit ? ` · ${R.addedLater}` : ""}
                      </span>
                    </div>
                    <Tag tone={d.visibility === "team_only" ? "neutral" : "info"} icon={d.visibility === "team_only" ? "lock" : "visibility"}>
                      {d.visibility === "team_only" ? R.teamOnly : R.visibleToClient}
                    </Tag>
                  </li>
                ))}
              </ul>
            ),
            detail.can.coordinate || (detail.can.message && !terminal) ? (
              <Button size="sm" variant="secondary" icon="upload_file" onClick={() => setDialog("upload")}>
                {R.uploadDoc}
              </Button>
            ) : null,
            "docs-h",
          )}

          <section className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
            <Tabs
              label={R.tabsLabel}
              tabs={[
                { key: "coordination", label: R.tabs.coordination, count: detail.coordination.length, panelId: "tab-panel" },
                { key: "timeline", label: R.tabs.timeline, panelId: "tab-panel" },
                { key: "messages", label: R.tabs.messages, count: detail.messages.length, panelId: "tab-panel" },
              ]}
              active={tab}
              onChange={(k) => setTab(k as typeof tab)}
            />
            <div id="tab-panel" role="tabpanel" className="flex flex-col gap-3">
              {tab === "coordination" ? (
                <CoordinationPanel detail={detail} onAdd={() => { setCorrecting(null); setDialog("coord"); }} onCorrect={(e) => { setCorrecting(e); setDialog("coord"); }} />
              ) : tab === "timeline" ? (
                <ol className="m-0 flex list-none flex-col gap-3 p-0">
                  {detail.timeline.map((e, i) => (
                    <li key={`${e.at}-${i}`} className="flex flex-col gap-0.5 border-s-2 border-line ps-3">
                      <span className="flex flex-wrap items-center gap-2">
                        <strong className="text-14">{e.title}</strong>
                        {!e.visible ? (
                          <Tag tone="neutral" icon="lock">
                            {R.teamOnly}
                          </Tag>
                        ) : null}
                      </span>
                      {e.body ? <p className="m-0 text-14 whitespace-pre-line text-charcoal">{e.body}</p> : null}
                      <span className="text-12 text-muted">
                        {e.authorLabel ?? e.authorKind} · <bdi dir="ltr">{formatDateTime(e.at, fmt)}</bdi>
                      </span>
                    </li>
                  ))}
                </ol>
              ) : (
                <MessagesPanel detail={detail} />
              )}
            </div>
          </section>
        </div>

        <aside className="flex flex-col gap-5">
          {card(
            R.actions,
            <div className="flex flex-col gap-3">
              {actions.length === 0 ? <p className="m-0 text-14 text-muted">—</p> : null}
              {actions.map((a) => (
                <div key={a.key} className="flex flex-col gap-1.5">
                  <Button
                    fullWidth
                    variant={a.key === "not_eligible" ? "secondary" : "primary"}
                    softDisabled={!a.enabled}
                    aria-describedby={a.enabled ? undefined : `why-${a.key}`}
                    loading={act.busy}
                    onClick={() => runAction(a.key)}
                  >
                    {R.actionKeys[a.key] ?? a.label}
                  </Button>
                  {!a.enabled ? (
                    <ul id={`why-${a.key}`} className="m-0 flex list-none flex-col gap-1 p-0 text-13 text-muted">
                      {a.reasons.map((r) => (
                        <li key={r} className="flex items-start gap-1">
                          <Icon name="block" size={16} />
                          {r}
                        </li>
                      ))}
                    </ul>
                  ) : null}
                </div>
              ))}
              {detail.can.message && !terminal ? (
                <Button fullWidth variant="secondary" icon="campaign" onClick={() => setDialog("update")}>
                  {c.update.button}
                </Button>
              ) : null}
            </div>,
            undefined,
            "actions-h",
          )}

          {card(
            R.whatClientSees,
            <div className="flex flex-col gap-2 rounded-md bg-subtle p-3 text-14">
              <span>
                {R.clientStatus}: <strong>{ind.status[detail.status as RequestStatusKey] ?? detail.statusLabel}</strong>
              </span>
              <span>
                {R.clientWaiting}: <strong>{ind.waiting[detail.waitingOn]}</strong>
              </span>
              <span>
                {R.clientNext}: {detail.nextStepText ?? <span className="text-muted">{ind.nextStep[detail.status as RequestStatusKey]} {R.defaultNext}</span>}
              </span>
            </div>,
            undefined,
            "sees-h",
          )}

          {card(
            R.consent,
            <ConsentEvidence detail={detail} />,
            detail.consentActive ? (
              <Tag tone="ok" icon="verified_user">
                {R.consentActive}
              </Tag>
            ) : (
              <Tag tone="warn" icon="gpp_maybe">
                {R.consentNone}
              </Tag>
            ),
            "consent-h",
          )}
        </aside>
      </div>

      <AssignDialog open={dialog === "assign"} onClose={() => setDialog(null)} base={base} />
      <IdentityDialog open={dialog === "identity"} onClose={() => setDialog(null)} base={base} />
      <InfoDrawer open={dialog === "info"} onClose={() => setDialog(null)} base={base} />
      <CoordinationDrawer open={dialog === "coord"} onClose={() => setDialog(null)} base={base} detail={detail} correcting={correcting} />
      <NotEligibleDialog open={dialog === "notEligible"} onClose={() => setDialog(null)} base={base} />
      <UpdateDrawer open={dialog === "update"} onClose={() => setDialog(null)} base={base} />
      <UploadDialog open={dialog === "upload"} onClose={() => setDialog(null)} base={base} />
    </div>
  );
}

function ConsentEvidence({ detail }: { detail: TeamRequestDetail }) {
  const c = useTeamCopy();
  const R = c.review;
  const { numerals } = useI18n();
  if (detail.consents.length === 0) return <p className="m-0 text-14 text-muted">—</p>;
  return (
    <ul className="m-0 flex list-none flex-col gap-3 p-0">
      {detail.consents.map((k) => (
        <li key={k.id} className={cn("flex flex-col gap-1 rounded-md border p-3 text-13", k.withdrawnAt ? "border-line bg-subtle" : "border-ok-line bg-ok-bg")}>
          <span>
            {R.consentRecipient}: <strong>{k.recipientName}</strong>
          </span>
          <span>
            {R.consentVersion}: <bdi dir="ltr" className="font-mono">{k.textVersion}</bdi>
          </span>
          <span>
            {R.consentAt}: <bdi dir="ltr">{formatDateTime(k.recordedAt, { numerals })}</bdi>
          </span>
          {k.withdrawnAt ? (
            <span className="font-semibold text-warn">
              {R.consentWithdrawn} · <bdi dir="ltr">{formatDateTime(k.withdrawnAt, { numerals })}</bdi> · {R.consentReasons[k.withdrawnReason ?? ""] ?? k.withdrawnReason}
            </span>
          ) : null}
          <details>
            <summary className="cursor-pointer font-semibold">{c.coordination.summary}</summary>
            <p className="m-0 mt-1 leading-6">{k.textSnapshot}</p>
          </details>
        </li>
      ))}
    </ul>
  );
}

function CoordinationPanel({ detail, onAdd, onCorrect }: { detail: TeamRequestDetail; onAdd: () => void; onCorrect: (e: CoordinationItem) => void }) {
  const c = useTeamCopy();
  const C = c.coordination;
  const { numerals } = useI18n();
  const base = `/api/team/requests/${encodeURIComponent(detail.reference)}`;
  return (
    <>
      <Alert tone="info" compact>
        {C.banner}
      </Alert>
      {detail.can.coordinate ? (
        <Button size="sm" icon="add" className="self-start" onClick={onAdd}>
          {C.add}
        </Button>
      ) : !detail.consentActive ? (
        <p className="m-0 text-13 text-warn">{C.consentBlocked}</p>
      ) : null}
      {detail.coordination.length === 0 ? <p className="m-0 text-14 text-muted">{C.empty}</p> : null}
      <ol className="m-0 flex list-none flex-col gap-3 p-0">
        {detail.coordination.map((e) => (
          <li key={e.id} className="flex flex-col gap-1.5 rounded-md border border-line p-3">
            <div className="flex flex-wrap items-center gap-2 text-13">
              <Tag tone="neutral" icon="call">
                {C.channels[e.channel] ?? e.channel}
              </Tag>
              <Tag tone="info">{C.kinds[e.kind] ?? e.kind}</Tag>
              {e.correctsEntryId ? <Tag tone="warn" icon="edit">{C.corrects}</Tag> : null}
              <bdi dir="ltr" className="text-muted">
                {formatDateTime(e.occurredAt, { numerals })}
              </bdi>
            </div>
            <strong className="text-14">{e.counterpart}</strong>
            <p className="m-0 text-14 whitespace-pre-line">{e.summary}</p>
            {e.evidence.length > 0 ? (
              <ul className="m-0 flex list-none flex-wrap gap-2 p-0 text-13">
                {e.evidence.map((d) => (
                  <li key={d.id}>
                    {d.versionId ? <a href={`${base}/documents/${d.versionId}/file`}>{d.name}</a> : d.name}
                  </li>
                ))}
              </ul>
            ) : null}
            {e.visibleToApplicant ? (
              <p className="m-0 rounded-sm bg-info-bg p-2 text-13">
                <Icon name="visibility" size={14} /> {e.applicantText}
              </p>
            ) : (
              <span className="text-12 text-muted">
                <Icon name="lock" size={14} /> {c.review.teamOnly}
              </span>
            )}
            <div className="flex items-center justify-between gap-2 text-12 text-muted">
              <span>{C.recordedBy(e.recordedBy)}</span>
              {detail.can.coordinate ? (
                <Button size="sm" variant="text" onClick={() => onCorrect(e)}>
                  {C.correct}
                </Button>
              ) : null}
            </div>
          </li>
        ))}
      </ol>
    </>
  );
}

function MessagesPanel({ detail }: { detail: TeamRequestDetail }) {
  const c = useTeamCopy();
  const M = c.messages;
  const { numerals } = useI18n();
  const [text, setText] = useState("");
  const act = useTeamAction();
  const base = `/team/requests/${encodeURIComponent(detail.reference)}`;
  const send = async (kind: "messages" | "notes") => {
    if (await act.run("POST", `${base}/${kind}`, { body: text })) setText("");
  };
  return (
    <>
      {detail.messages.length === 0 ? <p className="m-0 text-14 text-muted">{M.empty}</p> : null}
      <ol className="m-0 flex list-none flex-col gap-2 p-0">
        {detail.messages.map((m) => (
          <li
            key={m.id}
            className={cn(
              "flex flex-col gap-1 rounded-md p-3 text-14",
              m.isInternal ? "border border-dashed border-line-strong bg-subtle" : m.authorKind === "team" ? "bg-info-bg" : "border border-line bg-white",
            )}
          >
            <span className="flex flex-wrap items-center gap-2 text-12 text-muted">
              <strong className="text-ink">{m.isInternal ? `${M.internalTag} · ${m.authorLabel}` : m.authorKind === "team" ? M.fromTeam : M.fromClient}</strong>
              <bdi dir="ltr">{formatDateTime(m.at, { numerals })}</bdi>
            </span>
            <p className="m-0 whitespace-pre-line">{m.body}</p>
          </li>
        ))}
      </ol>
      {detail.can.message || detail.can.note ? (
        <div className="flex flex-col gap-2 border-t border-divider pt-3">
          <Textarea label={M.toClient} rows={3} maxLength={2000} value={text} onChange={(e) => setText(e.target.value)} help={M.noteHelp} />
          {act.error ? <Alert tone="err">{act.error}</Alert> : null}
          <div className="flex flex-wrap gap-2">
            {detail.can.message ? (
              <Button size="md" loading={act.busy} disabled={!text.trim()} onClick={() => void send("messages")}>
                {M.sendMessage}
              </Button>
            ) : null}
            {detail.can.note ? (
              <Button size="md" variant="secondary" icon="lock" loading={act.busy} disabled={!text.trim()} onClick={() => void send("notes")}>
                {M.addNote}
              </Button>
            ) : null}
          </div>
        </div>
      ) : null}
    </>
  );
}

/* ───────── dialogs and drawers ───────── */

function FormFooter({ onClose, busy, label, onSubmit }: { onClose: () => void; busy: boolean; label: string; onSubmit: () => void }) {
  const c = useTeamCopy();
  return (
    <div className="flex flex-wrap justify-end gap-2">
      <Button variant="secondary" onClick={onClose}>
        {c.cancel}
      </Button>
      <Button loading={busy} onClick={onSubmit}>
        {label}
      </Button>
    </div>
  );
}

function AssignDialog({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const [members, setMembers] = useState<TeamMember[]>([]);
  const [userId, setUserId] = useState("");
  const act = useTeamAction();
  useEffect(() => {
    if (!open) return;
    let alive = true;
    apiSend<TeamMember[]>("GET", "/team/members")
      .then((m) => {
        if (alive) setMembers(m);
      })
      .catch(() => undefined);
    return () => {
      alive = false;
    };
  }, [open]);
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={c.review.assignTitle}
      footer={<FormFooter onClose={onClose} busy={act.busy} label={c.save} onSubmit={async () => (await act.run("POST", `${base}/assign`, { userId })) && onClose()} />}
    >
      <Select
        label={c.review.assignTo}
        placeholder={c.review.assignPick}
        value={userId}
        onChange={(e) => setUserId(e.target.value)}
        options={members.map((m) => ({ value: m.userId, label: `${m.name}${m.role ? ` · ${m.role}` : ""}` }))}
        error={act.fieldErrors.userId}
      />
      {act.error && !act.fieldErrors.userId ? <Alert tone="err">{act.error}</Alert> : null}
    </Dialog>
  );
}

function IdentityDialog({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const [note, setNote] = useState("");
  const act = useTeamAction();
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={c.review.identityTitle}
      footer={<FormFooter onClose={onClose} busy={act.busy} label={c.save} onSubmit={async () => (await act.run("POST", `${base}/identity-check`, { note })) && onClose()} />}
    >
      <Textarea label={c.review.identityNote} help={c.review.identityHelp} rows={3} maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} error={act.fieldErrors.note} />
      {act.error && !act.fieldErrors.note ? <Alert tone="err">{act.error}</Alert> : null}
    </Dialog>
  );
}

function InfoDrawer({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const I = c.info;
  const [items, setItems] = useState("");
  const [message, setMessage] = useState("");
  const act = useTeamAction();
  const list = items.split("\n").map((s) => s.trim()).filter(Boolean);
  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={I.title}
      footer={<FormFooter onClose={onClose} busy={act.busy} label={I.submit} onSubmit={async () => (await act.run("POST", `${base}/request-info`, { items: list, message })) && onClose()} />}
    >
      <div className="flex flex-col gap-4 p-5">
        <Textarea label={I.items} help={I.itemsHelp} rows={4} value={items} onChange={(e) => setItems(e.target.value)} />
        <Textarea label={I.message} help={I.messageHelp} rows={4} maxLength={1000} value={message} onChange={(e) => setMessage(e.target.value)} error={act.fieldErrors.message} />
        <div className="flex flex-col gap-1 rounded-md bg-subtle p-3 text-14">
          <strong>{I.preview}</strong>
          {list.length > 0 ? (
            <ul className="m-0 ps-5">
              {list.map((i) => (
                <li key={i}>{i}</li>
              ))}
            </ul>
          ) : null}
          <p className="m-0 whitespace-pre-line">{message || "—"}</p>
        </div>
        {act.error && !act.fieldErrors.message ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Drawer>
  );
}

const toLocalInput = (d: Date) => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

function CoordinationDrawer({ open, onClose, base, detail, correcting }: { open: boolean; onClose: () => void; base: string; detail: TeamRequestDetail; correcting: CoordinationItem | null }) {
  const c = useTeamCopy();
  const C = c.coordination;
  const [channel, setChannel] = useState("phone");
  const [kind, setKind] = useState("general");
  const [occurredAt, setOccurredAt] = useState(() => toLocalInput(new Date()));
  const [counterpart, setCounterpart] = useState("");
  const [summary, setSummary] = useState("");
  const [evidence, setEvidence] = useState<string[]>([]);
  const [visible, setVisible] = useState(false);
  const [applicantText, setApplicantText] = useState("");
  const act = useTeamAction();

  const submit = async () => {
    const ok = await act.run("POST", `${base}/coordination`, {
      channel,
      kind,
      occurredAt: occurredAt ? new Date(occurredAt).toISOString() : null,
      counterpart,
      summary,
      evidenceDocumentIds: evidence,
      visibleToApplicant: visible,
      applicantText: visible ? applicantText : null,
      correctsEntryId: correcting?.id ?? null,
    });
    if (ok) {
      setSummary("");
      setApplicantText("");
      setEvidence([]);
      setVisible(false);
      onClose();
    }
  };

  return (
    <Drawer open={open} onClose={onClose} title={correcting ? C.correctTitle : C.addTitle} footer={<FormFooter onClose={onClose} busy={act.busy} label={c.save} onSubmit={() => void submit()} />}>
      <div className="flex flex-col gap-4 p-5">
        <Alert tone="info" compact>
          {C.banner}
        </Alert>
        {correcting ? (
          <p className="m-0 rounded-md bg-subtle p-3 text-13">
            {C.corrects}: {correcting.counterpart} — {correcting.summary}
          </p>
        ) : null}
        <div className="grid gap-3 sm:grid-cols-2">
          <Select label={C.channel} value={channel} onChange={(e) => setChannel(e.target.value)} options={Object.entries(C.channels).map(([value, label]) => ({ value, label }))} error={act.fieldErrors.channel} />
          <Select label={C.kind} value={kind} onChange={(e) => setKind(e.target.value)} options={Object.entries(C.kinds).map(([value, label]) => ({ value, label }))} />
        </div>
        <TextField label={C.occurredAt} type="datetime-local" ltr value={occurredAt} onChange={(e) => setOccurredAt(e.target.value)} error={act.fieldErrors.occurredAt} />
        <TextField label={C.counterpart} help={C.counterpartHelp} maxLength={200} value={counterpart} onChange={(e) => setCounterpart(e.target.value)} error={act.fieldErrors.counterpart} />
        <Textarea label={C.summary} rows={4} maxLength={2000} value={summary} onChange={(e) => setSummary(e.target.value)} error={act.fieldErrors.summary} />
        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-14 font-semibold">{C.evidence}</legend>
          <span className="text-13 text-muted">{C.evidenceHelp}</span>
          {detail.documents.map((d) => (
            <Checkbox
              key={d.id}
              label={d.name}
              checked={evidence.includes(d.id)}
              onChange={(e) => setEvidence((x) => (e.target.checked ? [...x, d.id] : x.filter((i) => i !== d.id)))}
            />
          ))}
        </fieldset>
        <Checkbox label={C.visible} description={C.visibleHelp} checked={visible} onChange={(e) => setVisible(e.target.checked)} />
        {visible ? <Textarea label={C.applicantText} rows={3} maxLength={1000} value={applicantText} onChange={(e) => setApplicantText(e.target.value)} error={act.fieldErrors.applicantText} /> : null}
        {act.error && Object.keys(act.fieldErrors).length === 0 ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Drawer>
  );
}

function NotEligibleDialog({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const N = c.notEligible;
  const [reason, setReason] = useState("");
  const act = useTeamAction();
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={N.title}
      footer={<FormFooter onClose={onClose} busy={act.busy} label={N.submit} onSubmit={async () => (await act.run("POST", `${base}/not-eligible`, { reason })) && onClose()} />}
    >
      <Textarea label={N.reason} help={N.help} rows={4} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} error={act.fieldErrors.reason} />
      {act.error && !act.fieldErrors.reason ? <Alert tone="err">{act.error}</Alert> : null}
    </Dialog>
  );
}

function UpdateDrawer({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const U = c.update;
  const [text, setText] = useState("");
  const [next, setNext] = useState("");
  const act = useTeamAction();
  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={U.title}
      footer={<FormFooter onClose={onClose} busy={act.busy} label={U.submit} onSubmit={async () => (await act.run("POST", `${base}/updates`, { text, nextStep: next || null })) && onClose()} />}
    >
      <div className="flex flex-col gap-4 p-5">
        <Textarea label={U.text} rows={4} maxLength={1000} value={text} onChange={(e) => setText(e.target.value)} error={act.fieldErrors.text} />
        <Textarea label={U.nextStep} help={U.nextHelp} rows={2} maxLength={600} value={next} onChange={(e) => setNext(e.target.value)} />
        {act.error && !act.fieldErrors.text ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Drawer>
  );
}

function UploadDialog({ open, onClose, base }: { open: boolean; onClose: () => void; base: string }) {
  const c = useTeamCopy();
  const R = c.review;
  const [kind, setKind] = useState("lender_letter");
  const [name, setName] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [visible, setVisible] = useState(false);
  const act = useTeamAction();
  const submit = async () => {
    if (!file) return;
    const form = new FormData();
    form.append("file", file);
    form.append("kind", kind);
    if (name.trim()) form.append("name", name.trim());
    form.append("visibleToApplicant", visible ? "true" : "false");
    if (await act.run("POST", `${base}/documents`, undefined, form)) {
      setFile(null);
      setName("");
      onClose();
    }
  };
  return (
    <Dialog open={open} onClose={onClose} title={R.uploadTitle} footer={<FormFooter onClose={onClose} busy={act.busy} label={c.save} onSubmit={() => void submit()} />}>
      <div className="flex flex-col gap-3">
        <Select label={R.docKind} value={kind} onChange={(e) => setKind(e.target.value)} options={Object.entries(R.docKinds).map(([value, label]) => ({ value, label }))} />
        <TextField label={R.docName} maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
        <label className="flex flex-col gap-1 text-14 font-semibold">
          {R.docFile}
          <input type="file" accept="application/pdf,image/jpeg,image/png" onChange={(e) => setFile(e.target.files?.[0] ?? null)} className="text-14 font-normal" />
        </label>
        <Checkbox label={R.docVisible} checked={visible} onChange={(e) => setVisible(e.target.checked)} />
        {act.error ? <Alert tone="err">{act.error}</Alert> : null}
      </div>
    </Dialog>
  );
}
