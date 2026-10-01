"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { TextField } from "@/components/ui/Field";
import { apiSend, isApiError } from "@/lib/api/client";
import { safeNext } from "@/lib/api/types";

/** Asks for the name once (accounts created by mobile only), marks notifications read, and states how the mobile was confirmed. */
export function AccountClient({ needsName, unread, smsVerified }: { needsName: boolean; unread: number; smsVerified: boolean }) {
  const router = useRouter();
  const sp = useSearchParams();
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (unread > 0) {
      const t = window.setTimeout(() => void apiSend("POST", "/market/me/notifications/read").catch(() => undefined), 2500);
      return () => window.clearTimeout(t);
    }
  }, [unread]);

  const save = async (e: FormEvent) => {
    e.preventDefault();
    if (name.trim().length < 2) {
      setError("اكتب اسمك (حرفان على الأقل).");
      return;
    }
    setBusy(true);
    try {
      await apiSend("PUT", "/account/name", { name: name.trim() });
      const next = sp.get("next");
      if (next) router.push(safeNext(next, "/account"));
      else router.refresh();
    } catch (err) {
      setError(isApiError(err) ? err.fieldError("name") ?? err.title : "تعذّر الحفظ. حاول مرة أخرى.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      {!smsVerified ? (
        <Alert tone="warn" title="رقم الجوال غير مُتحقق منه">
          خدمة الرسائل النصية غير مفعّلة في هذه البيئة، فلم نرسل رمزًا إلى رقمك. لا تستخدم بيانات حقيقية هنا.
        </Alert>
      ) : null}
      {needsName ? (
        <form onSubmit={save} noValidate className="flex flex-col gap-3 rounded-lg border border-rust-200 bg-rust-50 p-4 md:flex-row md:items-end">
          <div className="flex-1">
            <TextField label="ما اسمك؟ ليعرف الفريق كيف يخاطبك" value={name} onChange={(e) => setName(e.target.value)} error={error ?? undefined} autoComplete="name" />
          </div>
          <Button type="submit" loading={busy}>حفظ الاسم</Button>
        </form>
      ) : null}
    </>
  );
}
