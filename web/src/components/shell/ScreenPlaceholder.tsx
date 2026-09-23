import type { Metadata } from "next";
import { EmptyState } from "@/components/ui/SystemState";
import type { Dictionary } from "@/lib/i18n";
import { getServerDictionary } from "@/lib/i18n/server";

type Pick = (t: Dictionary) => string;

import { PageHeader } from "./PageHeader";

export { PageHeader };

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
