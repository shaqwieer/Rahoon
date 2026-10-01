"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ActionDialog, ActionError, useTeamAction } from "@/components/market/team/useTeamAction";
import { Card } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { TextField, Textarea } from "@/components/ui/Field";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { Catalog, RoleRow, Scope } from "@/lib/team/admin";

/**
 * Custom role editor: name, description and grants by catalog area, each scopable grant with «المسند / الكل».
 * Grants beyond the editor's own (or wider than their scope) are disabled; the API refuses them anyway.
 * A system role, an archived role or a role beyond the editor's authority opens read-only, with the reason.
 */
export function RoleEditor({ catalog, role, grantable, blocked, canArchive }: {
  catalog: Catalog;
  role?: RoleRow;
  grantable: Record<string, Scope>;
  blocked?: string | null;
  canArchive?: boolean;
}) {
  const router = useRouter();
  const key = useIdempotencyKey();
  const readOnly = Boolean(blocked);
  const [nameAr, setNameAr] = useState(role?.nameAr ?? "");
  const [nameEn, setNameEn] = useState(role?.nameEn && role.nameEn !== role.nameAr ? role.nameEn : "");
  const [description, setDescription] = useState(role?.descriptionAr ?? "");
  const [grants, setGrants] = useState<Record<string, Scope>>(Object.fromEntries((role?.grants ?? []).map((g) => [g.key, g.scope])));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [failure, setFailure] = useState<{ title: string; reasons: string[] } | null>(null);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const archive = useTeamAction();
  const [archiveOpen, setArchiveOpen] = useState(false);
  const [reason, setReason] = useState("");

  const toggle = (k: string, on: boolean, scopable: boolean) => {
    setSaved(false);
    setGrants((g) => {
      const next = { ...g };
      // A new scopable grant starts narrow (assigned work); widen it explicitly.
      if (on) next[k] = scopable ? "assigned" : "all";
      else delete next[k];
      return next;
    });
  };

  const submit = async () => {
    const e: Record<string, string> = {};
    if (nameAr.trim().length < 2) e.nameAr = "أدخل اسم الدور بالعربية.";
    if (Object.keys(grants).length === 0) e.grants = "اختر صلاحية واحدة على الأقل.";
    setErrors(e);
    setFailure(null);
    if (Object.keys(e).length) return;
    setBusy(true);
    const body = {
      nameAr, nameEn: nameEn || null, descriptionAr: description || null, version: role?.version,
      grants: Object.entries(grants).map(([k, scope]) => ({ key: k, scope })),
    };
    try {
      const res = await apiSend<{ id: string }>(role ? "PUT" : "POST", role ? `/team/admin/roles/${role.id}` : "/team/admin/roles", body, { idempotencyKey: key.get() });
      key.reset();
      if (role) { setSaved(true); router.refresh(); } else router.push(`/team/roles/${res.id}`);
    } catch (err) {
      key.reset();
      if (isApiError(err)) {
        setErrors(Object.fromEntries(Object.entries(err.errors ?? {}).map(([k, x]) => [k, x[0]])));
        setFailure({ title: err.title || "تعذّر الحفظ.", reasons: err.reasons ?? [] });
      } else setFailure({ title: "تعذّر الاتصال. أعد المحاولة.", reasons: [] });
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="flex flex-col gap-5" noValidate onSubmit={(ev) => { ev.preventDefault(); void submit(); }}>
      {blocked ? <Alert tone="info" compact>{blocked}</Alert> : null}
      {failure ? (
        <Alert tone="err" title={failure.title}>
          {failure.reasons.length ? <ul className="m-0 ps-5">{failure.reasons.map((r) => <li key={r}>{r}</li>)}</ul> : null}
        </Alert>
      ) : null}
      {saved ? <Alert tone="ok" compact>حُفظ الدور. يسري على أعضائه من طلبهم التالي.</Alert> : null}
      <Card>
        <div className="grid gap-4 md:grid-cols-2">
          <TextField label="اسم الدور" requiredMark value={nameAr} disabled={readOnly} onChange={(e) => { setSaved(false); setNameAr(e.target.value); }} error={errors.nameAr} />
          <TextField label="الاسم بالإنجليزية" optionalMark ltr value={nameEn} disabled={readOnly} onChange={(e) => setNameEn(e.target.value)} error={errors.nameEn} />
          <Textarea label="الوصف" optionalMark containerClassName="md:col-span-2" rows={2} value={description} disabled={readOnly} onChange={(e) => setDescription(e.target.value)} />
        </div>
      </Card>
      {errors.grants ? <p role="alert" className="m-0 text-14 text-err">{errors.grants}</p> : null}
      {catalog.areas.map((area) => (
        <Card key={area.key} title={area.nameAr}>
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {area.permissions.map((p) => {
              const mine = grantable[p.key];
              const on = p.key in grants;
              const canToggle = !readOnly && !p.reserved && Boolean(mine);
              const why = p.reserved ? `لمرحلة لاحقة (${p.reservedFor})` : !mine ? "لا تملك هذه الصلاحية" : null;
              return (
                <li key={p.key} className="flex flex-col gap-2 rounded-md border border-divider p-3 sm:flex-row sm:items-center sm:justify-between">
                  <label className="flex items-start gap-3 text-14">
                    <input type="checkbox" className="mt-1 size-5" checked={on} disabled={!canToggle}
                      onChange={(e) => toggle(p.key, e.target.checked, p.scopable)} />
                    <span className="flex flex-col">
                      <span className="font-semibold">{p.nameAr}</span>
                      {why ? <span className="text-12 text-muted">{why}</span> : null}
                    </span>
                  </label>
                  {p.scopable && on ? (
                    <div role="radiogroup" aria-label={`نطاق: ${p.nameAr}`} className="flex gap-1">
                      {(["assigned", "all"] as const).map((s) => {
                        const allowed = !readOnly && (s === "assigned" || mine === "all");
                        return (
                          <button key={s} type="button" role="radio" aria-checked={grants[p.key] === s} disabled={!allowed}
                            onClick={() => { setSaved(false); setGrants((g) => ({ ...g, [p.key]: s })); }}
                            className={`min-h-9 rounded-sm border px-3 text-13 ${grants[p.key] === s ? "border-rust bg-rust-50 font-bold text-rust-700" : "border-line bg-white"} disabled:opacity-50`}>
                            {s === "assigned" ? "المسند إليه" : "كل الفريق"}
                          </button>
                        );
                      })}
                    </div>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </Card>
      ))}
      {!readOnly ? (
        <div className="flex flex-wrap gap-2">
          <Button type="submit" loading={busy}>{role ? "حفظ الدور" : "إنشاء الدور"}</Button>
          {role && canArchive ? <Button type="button" variant="sensitive" onClick={() => { archive.setError(null); setReason(""); setArchiveOpen(true); }}>أرشفة الدور</Button> : null}
        </div>
      ) : null}
      {role ? (
        <ActionDialog open={archiveOpen} onClose={() => setArchiveOpen(false)} title="أرشفة الدور" confirm="أرشفة" tone="sensitive" busy={archive.busy === "archive"}
          onConfirm={async () => { const ok = await archive.run("archive", "POST", `/team/admin/roles/${role.id}/archive`, { reason }); if (ok) router.push("/team/roles"); return ok; }}>
          <ActionError error={archive.error} />
          <p className="m-0 text-14">لا يُسند الدور المؤرشف لأحد بعد الآن. يبقى في السجل.</p>
          <Textarea label="السبب" optionalMark value={reason} onChange={(e) => setReason(e.target.value)} rows={2} />
        </ActionDialog>
      ) : null}
    </form>
  );
}
