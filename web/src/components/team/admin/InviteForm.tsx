"use client";

import Link from "next/link";
import { useState } from "react";
import { Card } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Checkbox, TextField } from "@/components/ui/Field";
import { apiSend, isApiError } from "@/lib/api/client";
import type { InvitationIssued } from "@/lib/team/admin";
import { IssuedLink } from "./IssuedLink";

type RoleOption = { id: string; nameAr: string; isSystem: boolean; descriptionAr: string | null };

/** Invite form: work e-mail, full name, Saudi mobile (for the sign-in code), optional title, one or more roles. */
export function InviteForm({ roles }: { roles: RoleOption[] }) {
  const [v, setV] = useState({ email: "", fullName: "", phone: "", title: "" });
  const [roleIds, setRoleIds] = useState<string[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [failure, setFailure] = useState<{ title: string; reasons: string[] } | null>(null);
  const [busy, setBusy] = useState(false);
  const [issued, setIssued] = useState<InvitationIssued | null>(null);

  const submit = async () => {
    const e: Record<string, string> = {};
    if (!/^\S+@\S+\.\S+$/.test(v.email.trim())) e.email = "أدخل بريدًا إلكترونيًا صحيحًا.";
    if (v.fullName.trim().length < 2) e.fullName = "أدخل الاسم الكامل.";
    if (!/^(\+?966|0)?5\d{8}$/.test(v.phone.replace(/\s/g, ""))) e.phone = "أدخل رقم جوال سعودي يبدأ بـ 05.";
    if (roleIds.length === 0) e.roleIds = "اختر دورًا واحدًا على الأقل.";
    setErrors(e);
    setFailure(null);
    if (Object.keys(e).length) return;
    setBusy(true);
    try {
      // Not idempotency-replayed: the response holds the one-time link. A double submit gets «توجد دعوة سارية».
      const res = await apiSend<InvitationIssued>("POST", "/team/admin/invitations", { ...v, title: v.title || null, roleIds });
      setIssued(res);
    } catch (err) {
      if (isApiError(err)) {
        setErrors(Object.fromEntries(Object.entries(err.errors ?? {}).map(([k, x]) => [k, x[0]])));
        setFailure({ title: err.title || "تعذّر إرسال الدعوة.", reasons: err.reasons ?? [] });
      } else setFailure({ title: "تعذّر الاتصال. أعد المحاولة.", reasons: [] });
    } finally {
      setBusy(false);
    }
  };

  if (issued)
    return (
      <Card title="أُنشئت الدعوة">
        <div className="flex flex-col gap-4">
          <IssuedLink issued={issued} />
          <p className="m-0 text-14">بعد أن يضع المدعو كلمة المرور، يدخل من صفحة دخول الفريق بالبريد وكلمة المرور ورمز يصل إلى جواله.</p>
          <div className="flex flex-wrap gap-2">
            <Link href="/team/members" className="font-semibold">العودة إلى الفريق</Link>
          </div>
        </div>
      </Card>
    );

  return (
    <Card>
      <form className="flex flex-col gap-4" noValidate onSubmit={(ev) => { ev.preventDefault(); void submit(); }}>
        {failure ? (
          <Alert tone="err" title={failure.title}>
            {failure.reasons.length ? <ul className="m-0 ps-5">{failure.reasons.map((r) => <li key={r}>{r}</li>)}</ul> : null}
          </Alert>
        ) : null}
        <TextField label="البريد الإلكتروني للعمل" requiredMark type="email" ltr autoComplete="off" value={v.email}
          onChange={(e) => setV({ ...v, email: e.target.value })} error={errors.email} />
        <TextField label="الاسم الكامل" requiredMark value={v.fullName} onChange={(e) => setV({ ...v, fullName: e.target.value })} error={errors.fullName} />
        <TextField label="رقم الجوال" requiredMark inputMode="tel" ltr value={v.phone} onChange={(e) => setV({ ...v, phone: e.target.value })}
          help="يصل إليه رمز التحقق عند كل دخول." error={errors.phone} />
        <TextField label="المسمى الوظيفي" optionalMark value={v.title} onChange={(e) => setV({ ...v, title: e.target.value })} error={errors.title} />
        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-14 font-semibold">الأدوار</legend>
          {roles.length === 0 ? <p className="m-0 text-14 text-muted">لا توجد أدوار ضمن صلاحياتك يمكنك منحها.</p> : null}
          {roles.map((r) => (
            <Checkbox key={r.id} label={r.nameAr + (r.isSystem ? "" : " (مخصص)")} description={r.descriptionAr ?? undefined} checked={roleIds.includes(r.id)}
              onChange={(e) => setRoleIds((ids) => (e.target.checked ? [...ids, r.id] : ids.filter((x) => x !== r.id)))} />
          ))}
          {errors.roleIds ? <p role="alert" className="m-0 text-13 text-err">{errors.roleIds}</p> : null}
        </fieldset>
        <Alert tone="info" compact>لا توجد خدمة بريد مفعّلة: ستحصل على رابط تسلّمه للموظف بنفسك. الرابط يُستخدم مرة واحدة وتنتهي مدته.</Alert>
        <div><Button type="submit" loading={busy}>إنشاء الدعوة</Button></div>
      </form>
    </Card>
  );
}
