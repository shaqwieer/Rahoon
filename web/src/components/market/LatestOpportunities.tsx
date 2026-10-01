import Link from "next/link";
import { M } from "@/components/market/copy";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";

/** Latest published opportunities on the home page (filled in by the listing step). */
export async function LatestOpportunities() {
  return (
    <div className="flex flex-col items-start gap-4 rounded-lg border border-dashed border-line-strong bg-white p-6 md:flex-row md:items-center md:justify-between">
      <p className="m-0 flex gap-2 text-15 leading-7 text-charcoal">
        <Icon name="home_work" size={22} className="mt-0.5 text-muted" />
        {M.home.latestEmpty}
      </p>
      <Link href="/buy/new" className={buttonClasses({ variant: "secondary", size: "lg", className: "min-h-11 flex-none" })}>
        {M.home.latestEmptyCta}
      </Link>
    </div>
  );
}
