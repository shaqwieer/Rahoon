"use client";

import { useRouter } from "next/navigation";
import { useState, type ReactNode } from "react";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { apiSend, isApiError } from "@/lib/api/client";

/** Runs a team action (server-authorised), then refreshes the page; keeps the error and the field errors for the form. */
export function useTeamAction() {
  const router = useRouter();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<{ title: string; fields: Record<string, string>; reasons: string[] } | null>(null);
  const run = async (key: string, method: "POST" | "PUT", path: string, body?: unknown): Promise<boolean> => {
    setBusy(key);
    setError(null);
    try {
      await apiSend(method, path, body ?? {});
      router.refresh();
      return true;
    } catch (err) {
      if (isApiError(err))
        setError({ title: err.title || "تعذّر تنفيذ الإجراء.", fields: Object.fromEntries(Object.entries(err.errors ?? {}).map(([k, v]) => [k, v[0]])), reasons: err.reasons ?? [] });
      else setError({ title: "تعذّر الاتصال. أعد المحاولة.", fields: {}, reasons: [] });
      return false;
    } finally {
      setBusy(null);
    }
  };
  return { busy, error, setError, run };
}

export function ActionError({ error }: { error: { title: string; fields: Record<string, string>; reasons: string[] } | null }) {
  if (!error) return null;
  const list = [...error.reasons, ...Object.values(error.fields)];
  return (
    <Alert tone="err" title={error.title}>
      {list.length ? <ul className="m-0 ps-5">{list.map((x) => <li key={x}>{x}</li>)}</ul> : null}
    </Alert>
  );
}

/** A small form in a dialog: the confirm button runs `onConfirm`; the dialog closes only when it succeeds. */
export function ActionDialog({ open, onClose, title, children, confirm, onConfirm, busy, tone = "primary" }: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  confirm: string;
  onConfirm: () => Promise<boolean>;
  busy?: boolean;
  tone?: "primary" | "sensitive";
}) {
  return (
    <Dialog open={open} onClose={onClose} title={title}
      footer={
        <div className="flex gap-2">
          <Button variant={tone} loading={busy} onClick={async () => { if (await onConfirm()) onClose(); }}>{confirm}</Button>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
        </div>
      }>
      <div className="flex flex-col gap-4">{children}</div>
    </Dialog>
  );
}
