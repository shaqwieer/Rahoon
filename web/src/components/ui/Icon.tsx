import type { CSSProperties } from "react";
import { cn } from "@/lib/cn";

export interface IconProps {
  /** Material Symbols Rounded ligature name, e.g. "search". */
  name: string;
  /** Pixel size (design uses 14/16/18/20/22/24). */
  size?: number;
  /** Directional icons (arrows, chevrons, progress) flip in LTR. Clock/check/currency must not. */
  mirror?: boolean;
  /** Filled variant (FILL axis). */
  filled?: boolean;
  /** When set the icon is meaningful and announced; otherwise it is decorative (aria-hidden). */
  label?: string;
  className?: string;
  style?: CSSProperties;
}

export function Icon({ name, size = 20, mirror, filled, label, className, style }: IconProps) {
  return (
    <span
      className={cn("ms", mirror && "ms-mirror", className)}
      style={{ fontSize: size, width: size, height: size, ...(filled ? { fontVariationSettings: "'FILL' 1" } : null), ...style }}
      {...(label ? { role: "img", "aria-label": label } : { "aria-hidden": true })}
    >
      {name}
    </span>
  );
}
