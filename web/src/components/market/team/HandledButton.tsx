"use client";

import { useState } from "react";
import { ActionDialog, ActionError, useTeamAction } from "@/components/market/team/useTeamAction";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Field";

export function HandledButton({ reference }: { reference: string }) {
  const { busy, error, run } = useTeamAction();
  const [open, setOpen] = useState(false);
  const [note, setNote] = useState("");
  return (
    <>
      <Button size="sm" variant="secondary" className="mt-2" onClick={() => setOpen(true)}>تمت المعالجة</Button>
      <ActionDialog open={open} onClose={() => setOpen(false)} title="تسجيل المعالجة" confirm="حفظ" busy={busy === "h"}
        onConfirm={() => run("h", "POST", `/team/market/contact-messages/${reference}/handled`, { note })}>
        <ActionError error={error} />
        <Textarea label="ماذا تم؟ (اختياري)" value={note} onChange={(e) => setNote(e.target.value)} rows={2} />
      </ActionDialog>
    </>
  );
}
