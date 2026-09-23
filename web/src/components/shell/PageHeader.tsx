import type { ReactNode } from "react";

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
