import type { Tone } from "@/components/ui/tones";

/** Shared by the agreement page (client) and the printable full text (server) — keep this module directive-free. */
export const AGREEMENT_STATUS: Record<string, { label: string; tone: Tone }> = {
  PendingActivation: { label: "بانتظار التفعيل", tone: "warn" },
  Active: { label: "نشط", tone: "ok" },
  BreachReview: { label: "مراجعة إخلال", tone: "warn" },
  Completed: { label: "مكتمل السداد", tone: "ok" },
  Terminated: { label: "منتهٍ", tone: "neutral" },
};

/** Renders «التوقيع الإلكتروني المرخّص: …» with the lead and the assumption flag in bold (design copy). */
export function SigningNote({ note }: { note: string }) {
  const i = note.indexOf(":");
  const head = i > 0 ? note.slice(0, i + 1) : "";
  const body = i > 0 ? note.slice(i + 1) : note;
  const flag = "افتراض يتطلب تأكيداً قانونياً";
  const at = body.indexOf(flag);
  return (
    <span>
      {head ? <strong>{head}</strong> : null}
      {at >= 0 ? (
        <>
          {body.slice(0, at)}
          <strong>{flag}</strong>
          {body.slice(at + flag.length)}
        </>
      ) : (
        body
      )}
    </span>
  );
}
