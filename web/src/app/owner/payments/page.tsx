import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata, riyadhToday } from "@/components/owner/server";
import type { OwnerPayments } from "@/components/owner/types";
import { Card, InfoNote } from "@/components/owner/ui";
import { Amount, ServerText } from "@/components/owner/values";
import { DateText, EmptyState } from "@/components/ui";
import { HowToPay, InstallmentList } from "./PaymentsClient";

export const generateMetadata = () => ownerMetadata((c) => c.payments.title);

/** D10 — next installment, «رهون لا تستلم أي مبالغ», installment rows (received / being confirmed / due / late) and payment notices. */
export default async function OwnerPaymentsPage() {
  const { c, shell } = await getOwnerContext();
  const p = await ownerGet<OwnerPayments>("/payments");
  const P = c.payments;

  if (!p.active) {
    return (
      <OwnerPage shell={shell} title={P.title} active="payments">
        <EmptyState icon="payments" title={P.inactiveTitle} body={<ServerText text={p.message} />} />
      </OwnerPage>
    );
  }

  return (
    <OwnerPage shell={shell} title={P.title} active="payments">
      {p.next ? (
        <Card as="article" accent aria-labelledby="next-inst" className="gap-2 p-4">
          <h2 id="next-inst" className="m-0 text-14 font-bold text-muted">
            {P.next}
          </h2>
          <Amount value={p.next.amount} className="text-28 leading-10 font-bold" unitClassName="text-15 font-medium" />
          <span className="text-16">
            {P.on} <DateText value={p.next.dueDate} /> · {P.nextLine(p.next.no, p.total)}
          </span>
          <HowToPay title={p.howToPay.title} steps={p.howToPay.steps} note={p.note} />
        </Card>
      ) : (
        <EmptyState icon="task_alt" title={P.allPaidTitle} body={P.allPaidBody} />
      )}
      <InfoNote>
        <ServerText text={p.note} />
      </InfoNote>
      {!p.next ? <HowToPay title={p.howToPay.title} steps={p.howToPay.steps} note={p.note} /> : null}
      <InstallmentList items={p.items} today={riyadhToday()} />
    </OwnerPage>
  );
}
