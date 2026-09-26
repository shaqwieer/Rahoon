"use client";

import { likelyPaths, type PathKey } from "@/components/individual/copy";
import { IndividualFrame, Panel, useRequestCopy } from "@/components/individual/ui";
import { Tag } from "@/components/ui/Status";
import type { MyRequestDetail } from "@/lib/api/requests";

const ALL: PathKey[] = ["p1", "p2", "p3", "p4"];

export function PathsView({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const studying = likelyPaths(detail.fields.pathPreference);
  return (
    <IndividualFrame title={c.pathsPage.title} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: `/my/requests/${encodeURIComponent(detail.reference)}` }}>
      <h1 className="m-0 text-24 leading-9 font-bold">{c.pathsPage.heading}</h1>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {ALL.map((p) => (
          <li key={p}>
            <Panel as="article" className={studying.includes(p) ? "border-2 border-ink" : undefined}>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h2 className="m-0 text-18 font-bold">{c.paths[p].title}</h2>
                {studying.includes(p) ? (
                  <Tag tone="sel" icon="manage_search">
                    {c.pathsPage.studying}
                  </Tag>
                ) : null}
              </div>
              <p className="m-0 text-16 leading-7">{c.paths[p].body}</p>
            </Panel>
          </li>
        ))}
      </ul>
      <p className="m-0 text-14 leading-6 text-muted">{c.paths.footnote}</p>
    </IndividualFrame>
  );
}
