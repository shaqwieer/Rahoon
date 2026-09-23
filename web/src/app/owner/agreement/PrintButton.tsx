"use client";

import { Button } from "@/components/ui";

/** «تنزيل نسخة الاتفاق»: the browser's print dialog (Save as PDF). The chrome and this button are hidden in print. */
export function PrintButton({ label, hint }: { label: string; hint: string }) {
  return (
    <div className="flex flex-col gap-1.5 print:hidden">
      <Button variant="secondary" size="lg" icon="download" className="min-h-[52px] rounded-[8px]" aria-describedby="print-hint" onClick={() => window.print()}>
        {label}
      </Button>
      <span id="print-hint" className="text-13 text-muted">
        {hint}
      </span>
    </div>
  );
}
