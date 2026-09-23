"use client";

import { useRouter } from "next/navigation";
import { useToast } from "@/components/ui";
import type { InstallmentDto } from "@/lib/api/lender";
import { RecordPaymentForm } from "../RecordPaymentForm";

export function RecordPaymentPage({ reference, installments, today }: { reference: string; installments: InstallmentDto[]; today: string }) {
  const router = useRouter();
  const toast = useToast();
  const back = `/cases/${reference}/payments`;
  return (
    <RecordPaymentForm
      reference={reference}
      installments={installments}
      today={today}
      onCancel={() => router.push(back)}
      onDone={() => {
        toast.toast({ tone: "ok", message: "سُجّلت الدفعة — بانتظار المطابقة من موظف مالية آخر." });
        router.push(back);
        router.refresh();
      }}
    />
  );
}
