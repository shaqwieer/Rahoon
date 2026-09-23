"use client";

import { useRouter } from "next/navigation";
import { Icon, Menu } from "@/components/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";

/** «كل المحفظة · كل المناطق» scope filter: my cases vs whole portfolio, and region. */
export function PortfolioScope({ regions, region, scope }: { regions: string[]; region: string; scope: "all" | "mine" }) {
  const router = useRouter();
  const go = (next: { region?: string; scope?: string }) => {
    const qs = new URLSearchParams();
    const r = next.region ?? region;
    const s = next.scope ?? scope;
    if (r) qs.set("region", r);
    if (s === "mine") qs.set("scope", "mine");
    router.push(`/portfolio${qs.size ? `?${qs}` : ""}`);
  };
  return (
    <Menu
      label="نطاق المحفظة"
      align="end"
      triggerClassName={buttonClasses({ variant: "secondary" })}
      trigger={
        <>
          <Icon name="filter_list" size={18} />
          {scope === "mine" ? "حالاتي" : "كل المحفظة"} · {region || "كل المناطق"}
        </>
      }
      items={[
        { key: "all", label: "كل المحفظة", onSelect: () => go({ scope: "all" }), checked: scope === "all" },
        { key: "mine", label: "حالاتي فقط", onSelect: () => go({ scope: "mine" }), checked: scope === "mine" },
        { key: "all-regions", label: "كل المناطق", icon: "public", onSelect: () => go({ region: "" }), checked: !region },
        ...regions.map((r) => ({ key: `r-${r}`, label: r, icon: "location_on", onSelect: () => go({ region: r }), checked: region === r })),
      ]}
    />
  );
}
