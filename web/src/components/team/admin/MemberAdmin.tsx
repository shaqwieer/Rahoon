"use client";

import { useState } from "react";
import { ActionDialog, ActionError, useTeamAction } from "@/components/market/team/useTeamAction";
import { Card } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Checkbox, Select, Textarea } from "@/components/ui/Field";
import type { MemberDetail } from "@/lib/team/admin";

type Dlg = null | "suspend" | "reactivate" | "remove" | "reassign";

/**
 * Member administration. Every button mirrors a server rule (the API checks each one again): no self-change, no acting on
 * someone with more access, never leaving the team without an active platform owner, and open work must go somewhere
 * before a member loses access.
 */
export function MemberAdmin({ member: m, others }: { member: MemberDetail; others: { userId: string; name: string }[] }) {
  const { busy, error, setError, run } = useTeamAction();
  const [roleIds, setRoleIds] = useState<string[]>(m.roles.filter((r) => !r.archived).map((r) => r.id));
  const [reason, setReason] = useState("");
  const [dlg, setDlg] = useState<Dlg>(null);
  const [reassign, setReassign] = useState<"" | "unassigned" | "member">("");
  const [to, setTo] = useState("");
  const open = (d: Dlg) => { setError(null); setReason(""); setReassign(""); setTo(""); setDlg(d); };
  const base = `/team/admin/members/${m.id}`;
  const a = m.actions;
  const needsMove = m.workload.total > 0 && (dlg === "suspend" || dlg === "remove" || dlg === "reassign");
  const statusBody = () => ({ reason, reassign: needsMove ? reassign || null : null, reassignToUserId: reassign === "member" ? to || null : null });

  // Roles the member holds but the actor can't grant stay visible (disabled) so nothing is removed by accident.
  const assignable = new Map(m.assignableRoles.map((r) => [r.id, r]));
  const roleChoices = [...m.assignableRoles, ...m.roles.filter((r) => !assignable.has(r.id)).map((r) => ({ id: r.id, nameAr: r.nameAr, isSystem: r.isSystem }))];

  return (
    <>
      {!a.manage && a.blocked ? <Alert tone="info" compact>{a.blocked}</Alert> : null}
      {m.lastOwner ? <Alert tone="warn" compact>هذا آخر مالك نشط للمنصة: لا يمكن إيقافه أو إزالته أو سحب دور المالك منه قبل إضافة مالك آخر.</Alert> : null}
      <ActionError error={dlg ? null : error} />

      <Card title="الأدوار"
        actions={a.changeRoles ? (
          <Button size="sm" loading={busy === "roles"} disabled={roleIds.length === 0}
            onClick={() => void run("roles", "POST", `${base}/roles`, { roleIds })}>حفظ الأدوار</Button>
        ) : null}>
        {a.changeRoles ? (
          <div className="flex flex-col gap-2">
            {roleChoices.map((r) => (
              <Checkbox key={r.id} label={r.nameAr + (r.isSystem ? "" : " (مخصص)") + (assignable.has(r.id) ? "" : " — خارج صلاحياتك")}
                disabled={!assignable.has(r.id)} checked={roleIds.includes(r.id)}
                onChange={(e) => setRoleIds((ids) => (e.target.checked ? [...ids, r.id] : ids.filter((x) => x !== r.id)))} />
            ))}
            <p className="m-0 text-13 text-muted">يسري التغيير على جلسته الحالية من الطلب التالي، دون إعادة دخول.</p>
          </div>
        ) : (
          <ul className="m-0 flex list-none flex-wrap gap-2 p-0">
            {m.roles.map((r) => <li key={r.id} className="rounded-pill bg-subtle px-3 py-1 text-14">{r.nameAr}</li>)}
          </ul>
        )}
      </Card>

      {a.suspend || a.reactivate || a.remove || a.reassign ? (
        <div className="flex flex-wrap gap-2">
          {a.reassign ? <Button variant="secondary" onClick={() => open("reassign")}>إعادة إسناد أعماله</Button> : null}
          {a.reactivate ? <Button onClick={() => open("reactivate")}>إعادة تفعيل</Button> : null}
          {a.suspend ? <Button variant="secondary" onClick={() => open("suspend")}>إيقاف مؤقت</Button> : null}
          {a.remove ? <Button variant="sensitive" onClick={() => open("remove")}>إزالة من الفريق</Button> : null}
        </div>
      ) : null}

      <ActionDialog
        open={dlg !== null}
        onClose={() => setDlg(null)}
        title={dlg === "suspend" ? "إيقاف العضو" : dlg === "reactivate" ? "إعادة التفعيل" : dlg === "remove" ? "إزالة من الفريق" : "إعادة إسناد الأعمال"}
        confirm={dlg === "suspend" ? "إيقاف" : dlg === "reactivate" ? "تفعيل" : dlg === "remove" ? "إزالة" : "إعادة الإسناد"}
        tone={dlg === "remove" || dlg === "suspend" ? "sensitive" : "primary"}
        busy={busy === "status"}
        onConfirm={() => run("status", "POST", `${base}/${dlg}`, statusBody())}
      >
        <ActionError error={error} />
        {dlg === "suspend" ? <p className="m-0 text-14">تنتهي جلساته فورًا ولا يستطيع الدخول حتى تعيد تفعيله. يبقى سجله وأدواره.</p> : null}
        {dlg === "remove" ? <p className="m-0 text-14">تنتهي عضويته وجلساته. لا يُحذف شيء: يبقى سجله وكل ما قام به. لإعادته لاحقًا أرسل له دعوة جديدة.</p> : null}
        {needsMove ? (
          <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
            <legend className="mb-1 text-14 font-semibold">لديه {m.workload.total} من الأعمال المفتوحة. أين تذهب؟</legend>
            <label className="flex items-center gap-2 text-14"><input type="radio" name="reassign" checked={reassign === "unassigned"} onChange={() => setReassign("unassigned")} /> إلى قائمة غير المسندة</label>
            <label className="flex items-center gap-2 text-14"><input type="radio" name="reassign" checked={reassign === "member"} onChange={() => setReassign("member")} /> إلى عضو آخر</label>
            {reassign === "member" ? (
              <Select label="العضو" value={to} onChange={(e) => setTo(e.target.value)} error={error?.fields.reassignToUserId}
                options={[{ value: "", label: "اختر…" }, ...others.map((o) => ({ value: o.userId, label: o.name }))]} />
            ) : null}
            {error?.fields.reassign ? <p role="alert" className="m-0 text-13 text-err">{error.fields.reassign}</p> : null}
            <p className="m-0 text-12 text-muted">يُسجل في كل ملف من كان مسؤولًا ومن أصبح.</p>
          </fieldset>
        ) : null}
        <Textarea label="السبب" requiredMark value={reason} onChange={(e) => setReason(e.target.value)} rows={2} error={error?.fields.reason} help="يُحفظ في السجل." />
      </ActionDialog>
    </>
  );
}
