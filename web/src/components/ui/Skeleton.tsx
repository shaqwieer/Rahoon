import type { CSSProperties } from "react";
import { cn } from "@/lib/cn";

/** Neutral loading placeholder block (C11 skeleton). Pulses unless reduced motion is preferred. */
export function Skeleton({ width = "100%", height = 14, radius = 4, className, style }: { width?: number | string; height?: number; radius?: number; className?: string; style?: CSSProperties }) {
  return <span aria-hidden="true" className={cn("block animate-rh-pulse bg-subtle", className)} style={{ width, height, borderRadius: radius, ...style }} />;
}
