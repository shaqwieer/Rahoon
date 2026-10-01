"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { TextField } from "@/components/ui/Field";
import { Logo } from "@/components/ui/Logo";
import { Skeleton } from "@/components/ui/Skeleton";
import { apiSend, isApiError } from "@/lib/api/client";
import { formatDateTime } from "@/lib/format";

interface Invitation {
  organization: string;
  fullName: string;
  email: string;
  phoneMasked: string;
  title: string | null;
  roles: string[];
  expiresAt: string;
  invitedByLabel: string;
  existingAccount: boolean;
}

type State =
  | { kind: "loading" }
  | { kind: "invalid"; message: string }
  | { kind: "ready"; inv: Invitation; token: string }
  | { kind: "joined"; email: string };

/** The invited person sets a password (or confirms their existing staff password). E-mail, mobile and roles are fixed. */
export function JoinForm() {
  const [state, setState] = useState<State>({ kind: "loading" });
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [failure, setFailure] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const token = window.location.hash.replace(/^#/, "").trim();
      let next: State;
      if (!token) next = { kind: "invalid", message: "رابط الدعوة ناقص. افتح الرابط كما وصلك كاملًا." };
      else {
        try {
          next = { kind: "ready", inv: await apiSend<Invitation>("POST", "/auth/invitations/lookup", { token }), token };
        } catch (err) {
          next = { kind: "invalid", message: isApiError(err) && err.title ? err.title : "تعذّر التحقق من الدعوة. أعد المحاولة." };
        }
      }
      if (!cancelled) setState(next);
    })();
    // A different link pasted into this tab only changes the fragment: start over with it.
    const onHash = () => window.location.reload();
    window.addEventListener("hashchange", onHash);
    return () => { cancelled = true; window.removeEventListener("hashchange", onHash); };
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (state.kind !== "ready") return;
    const token = state.token;
    const existing = state.inv.existingAccount;
    const fe: Record<string, string> = {};
    if (existing) {
      if (!password) fe.currentPassword = "أدخل كلمة مرور حسابك الحالية.";
    } else {
      if (password.length < 10) fe.password = "كلمة المرور 10 أحرف على الأقل.";
      else if (!/[0-9]/.test(password) || !/[^\d\s]/.test(password)) fe.password = "استخدم حروفًا وأرقامًا معًا.";
      if (confirm !== password) fe.confirm = "كلمتا المرور غير متطابقتين.";
    }
    setErrors(fe);
    setFailure(null);
    if (Object.keys(fe).length) return;
    setBusy(true);
    try {
      const res = await apiSend<{ email: string }>("POST", "/auth/invitations/accept", existing ? { token, currentPassword: password } : { token, password });
      // The link is spent: drop it from the address bar and history.
      window.history.replaceState(null, "", "/join");
      setState({ kind: "joined", email: res.email });
    } catch (err) {
      if (isApiError(err)) {
        setErrors(Object.fromEntries(Object.entries(err.errors ?? {}).map(([k, x]) => [k, x[0]])));
        if (err.code === "invitation_invalid") setState({ kind: "invalid", message: err.title });
        else setFailure(err.title || "تعذّر قبول الدعوة.");
      } else setFailure("تعذّر الاتصال. أعد المحاولة.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <Logo variant="horizontal" width={140} />
      {state.kind === "loading" ? (
        <div className="flex flex-col gap-3" aria-busy="true">
          <span role="status" className="sr-only">جارٍ التحقق من الدعوة…</span>
          <Skeleton height={32} width="75%" />
          <Skeleton height={96} />
        </div>
      ) : null}
      {state.kind === "invalid" ? (
        <>
          <h1 className="m-0 text-24 font-bold">لا يمكن استخدام هذه الدعوة</h1>
          <Alert tone="warn">{state.message}</Alert>
          <Link href="/login" className="font-semibold">صفحة دخول الفريق</Link>
        </>
      ) : null}
      {state.kind === "joined" ? (
        <>
          <h1 className="m-0 text-24 font-bold">انضممت إلى الفريق</h1>
          <Alert tone="ok">ادخل الآن بالبريد <bdi dir="ltr">{state.email}</bdi> وكلمة المرور، ثم الرمز الذي يصل إلى جوالك.</Alert>
          <Link href="/login" className="font-semibold">الذهاب إلى الدخول</Link>
        </>
      ) : null}
      {state.kind === "ready" ? (
        <form className="flex flex-col gap-4" noValidate onSubmit={submit}>
          <div className="flex flex-col gap-1">
            <h1 className="m-0 text-24 font-bold">دعوة للانضمام إلى {state.inv.organization}</h1>
            <p className="m-0 text-15 text-muted">مرحبًا {state.inv.fullName}. دعاك {state.inv.invitedByLabel}.</p>
          </div>
          <dl className="m-0 flex flex-col gap-2 rounded-md border border-line bg-white p-3 text-14">
            <div><dt className="text-muted">البريد</dt><dd className="m-0 font-semibold"><bdi dir="ltr">{state.inv.email}</bdi></dd></div>
            <div><dt className="text-muted">الجوال (لرمز الدخول)</dt><dd className="m-0"><bdi dir="ltr">{state.inv.phoneMasked}</bdi></dd></div>
            <div><dt className="text-muted">الأدوار</dt><dd className="m-0">{state.inv.roles.join("، ")}</dd></div>
            <div><dt className="text-muted">صالحة حتى</dt><dd className="m-0">{formatDateTime(state.inv.expiresAt)}</dd></div>
          </dl>
          <p className="m-0 text-13 text-muted">الأدوار يحددها من دعاك ولا تتغير من هنا. إن كان الجوال أو البريد غير صحيح فاطلب دعوة جديدة.</p>
          {failure ? <Alert tone="err">{failure}</Alert> : null}
          {state.inv.existingAccount ? (
            <TextField label="كلمة مرور حسابك الحالية" requiredMark type="password" autoComplete="current-password" value={password}
              onChange={(e) => setPassword(e.target.value)} error={errors.currentPassword} help="لديك حساب فريق سابق بهذا البريد؛ أكّد أنه لك." />
          ) : (
            <>
              <TextField label="كلمة المرور" requiredMark type="password" autoComplete="new-password" value={password}
                onChange={(e) => setPassword(e.target.value)} error={errors.password} help="10 أحرف على الأقل، حروف وأرقام." />
              <TextField label="تأكيد كلمة المرور" requiredMark type="password" autoComplete="new-password" value={confirm}
                onChange={(e) => setConfirm(e.target.value)} error={errors.confirm} />
            </>
          )}
          <Button type="submit" size="lg" loading={busy}>قبول الدعوة</Button>
        </form>
      ) : null}
    </div>
  );
}
