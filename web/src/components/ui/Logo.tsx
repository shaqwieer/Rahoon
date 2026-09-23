import Image from "next/image";
import { cn } from "@/lib/cn";

export type LogoVariant = "horizontal" | "horizontal-dark" | "stacked" | "stacked-dark" | "symbol" | "app-icon";

/** Supplied artwork (public/brand, byte-identical to design-source/uploads). Never recolour or CSS-filter. */
const ART: Record<LogoVariant, { src: string; w: number; h: number; min: number }> = {
  horizontal: { src: "/brand/rahoon-horizontal-full.svg", w: 420, h: 140, min: 170 },
  "horizontal-dark": { src: "/brand/rahoon-horizontal-dark.svg", w: 420, h: 140, min: 170 },
  stacked: { src: "/brand/rahoon-stacked-full.svg", w: 400, h: 240, min: 100 },
  "stacked-dark": { src: "/brand/rahoon-stacked-dark.svg", w: 400, h: 240, min: 100 },
  symbol: { src: "/brand/rahoon-symbol-full.svg", w: 64, h: 64, min: 24 },
  "app-icon": { src: "/brand/rahoon-app-icon.svg", w: 1024, h: 1024, min: 48 },
};

export interface LogoProps {
  /** Use the `-dark` variants (or app-icon) on dark surfaces. */
  variant?: LogoVariant;
  /** Rendered CSS width in px; clamped to the README minimum (horizontal 170, stacked 100, symbol 24, tile 48). */
  width?: number;
  /** Accessible name. The Arabic mark stays as supplied in the English UI; only the alt text changes. Pass "" when decorative. */
  alt?: string;
  priority?: boolean;
  className?: string;
}

export function Logo({ variant = "horizontal", width, alt = "رهون", priority, className }: LogoProps) {
  const art = ART[variant];
  const w = Math.max(art.min, width ?? (variant === "symbol" ? 28 : variant === "app-icon" ? 72 : 172));
  const h = Math.round((w * art.h) / art.w);
  return (
    <Image
      src={art.src}
      alt={alt}
      width={w}
      height={h}
      unoptimized
      priority={priority}
      draggable={false}
      className={cn("block max-w-none flex-none", variant === "app-icon" && "rounded-lg", className)}
      style={{ width: w, height: h }}
    />
  );
}
