"use client";

import { IndividualFrame, useRequestCopy } from "@/components/individual/ui";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";

export function Submitted({ reference }: { reference: string }) {
  const c = useRequestCopy();
  const S = c.submitted;
  return (
    <IndividualFrame title={c.wizard.title} back={{ href: "/my" }}>
      <div className="flex flex-col gap-4 pt-2">
        <span className="flex size-14 items-center justify-center rounded-full bg-ok-bg text-ok">
          <Icon name="task_alt" size={32} />
        </span>
        <h1 className="m-0 text-26 leading-[38px] font-bold">{S.title}</h1>
        <p className="m-0 flex flex-col gap-1 rounded-[12px] border border-line bg-white p-4">
          <span className="text-14 text-muted">{S.reference}</span>
          <bdi dir="ltr" className="self-start font-mono text-20 font-bold">
            {reference}
          </bdi>
        </p>
        <section aria-labelledby="next-h" className="flex flex-col gap-2">
          <h2 id="next-h" className="m-0 text-18 font-bold">
            {S.whatNext}
          </h2>
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {S.bullets.map((b) => (
              <li key={b} className="flex items-start gap-2 text-16 leading-7">
                <Icon name="check" size={20} className="mt-1 text-ok" />
                {b}
              </li>
            ))}
          </ul>
        </section>
        <Button href={`/my/requests/${encodeURIComponent(reference)}`} size="xl" fullWidth className="mt-2">
          {S.track}
        </Button>
        <Button href="/my" variant="secondary" size="xl" fullWidth>
          {c.myRequests}
        </Button>
      </div>
    </IndividualFrame>
  );
}
