"use client";

import { Button } from "@/components/ui/Button";

export function PrintButton() {
  return (
    <Button icon="print" onClick={() => window.print()}>
      طباعة / حفظ PDF
    </Button>
  );
}
