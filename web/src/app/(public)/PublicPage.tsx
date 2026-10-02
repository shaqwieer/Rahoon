import type { ReactNode } from "react";
import { PublicFooter, PublicHeader } from "@/components/shell/Public";
import { cn } from "@/lib/cn";

/**
 * Header + titled main column + footer for the simple public pages. `centered` is for short task pages (sign-in): the title,
 * the lead and the form share one narrow centred column.
 */
export function PublicPage({ eyebrow, title, lead, children, wide, centered }: { eyebrow?: string; title: string; lead?: ReactNode; children: ReactNode; wide?: boolean; centered?: boolean }) {
  const column = centered ? "mx-auto max-w-[480px] text-center" : wide ? "mx-auto max-w-[1440px]" : "mx-auto max-w-[960px]";
  return (
    <>
      <PublicHeader />
      <main id="main" tabIndex={-1} className="flex-1 outline-none">
        <section className={cn("border-b border-line bg-white px-4 md:px-10 xl:px-20", centered ? "py-8 md:py-10" : "py-10 md:py-14")}>
          <div className={column}>
            {eyebrow ? <span className="text-14 font-semibold text-rust">{eyebrow}</span> : null}
            <h1 className={cn("m-0 mt-1 font-bold text-pretty", centered ? "text-28 leading-10 md:text-32 md:leading-[46px]" : "text-30 leading-[44px] md:text-40 md:leading-[56px]")}>{title}</h1>
            {lead ? <p className={cn("m-0 mt-3 text-16 leading-7 text-charcoal", centered ? "mx-auto" : "max-w-[46em] md:text-18 md:leading-8")}>{lead}</p> : null}
          </div>
        </section>
        <div className={cn("mx-auto w-full px-4", centered ? "max-w-[480px] py-8 md:py-10" : wide ? "max-w-[1440px] py-10 md:px-10 md:py-14 xl:px-20" : "max-w-[960px] py-10 md:px-10 md:py-14")}>
          {children}
        </div>
      </main>
      <PublicFooter />
    </>
  );
}
