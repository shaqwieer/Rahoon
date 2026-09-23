import type { Metadata } from "next";
import type { ReactNode } from "react";
import { EmptyState } from "@/components/ui/SystemState";
import type { Dictionary } from "@/lib/i18n";
import { getServerDictionary } from "@/lib/i18n/server";

type Pick = (t: Dictionary) => string;

/** Page title row: the single h1 of an app page (design H2 32/44 promoted to h1), optional actions at the end. */
export function PageHeader({ title, description, actions }: { title: ReactNode; description?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end gap-3">
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <h1 className="m-0 text-24 leading-9 font-bold md:text-32 md:leading-[44px]">{title}</h1>
        {description ? <p className="m-0 text-14 text-muted">{description}</p> : null}
      </div>
      {actions}
    </div>
  );
}

export async function placeholderMetadata(pick: Pick): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: pick(t) };
}

/**
 * Temporary screen body while business screens are implemented: real shell, real h1, calm empty state.
 * No fake data — replace the page file when the screen is built.
 */
export async function ScreenPlaceholder({ title, icon = "construction", headerless }: { title: Pick; icon?: string; headerless?: boolean }) {
  const { t } = await getServerDictionary();
  return (
    <>
      {headerless ? <h1 className="sr-only">{title(t)}</h1> : <PageHeader title={title(t)} />}
      <EmptyState icon={icon} title={t.common.screenInProgressTitle} body={t.common.screenInProgressBody} />
    </>
  );
}
