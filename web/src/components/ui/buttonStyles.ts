import { cn } from "@/lib/cn";

/** Button look shared by <Button> (client) and plain links styled as buttons in Server Components. */
export type ButtonVariant = "primary" | "secondary" | "text" | "sensitive" | "strong" | "inverse";
export type ButtonSize = "sm" | "md" | "lg" | "xl";

const SIZE: Record<ButtonSize, string> = {
  sm: "min-h-9 px-3 text-13 rounded-sm",
  md: "min-h-10 px-4 text-14 rounded-sm",
  lg: "min-h-12 px-4 text-16 rounded-sm",
  xl: "min-h-[54px] px-6 text-18 rounded-[8px]",
};
const TEXT_SIZE: Record<ButtonSize, string> = {
  sm: "min-h-9 px-1.5 text-13 rounded-xs",
  md: "min-h-10 px-2 text-14 rounded-xs",
  lg: "min-h-12 px-2 text-16 rounded-xs",
  xl: "min-h-[54px] px-2 text-18 rounded-xs",
};

const VARIANT: Record<ButtonVariant, { on: string; off: string }> = {
  primary: { on: "bg-rust text-white hover:bg-rust-700 active:bg-rust-700 border border-transparent", off: "bg-track text-soft border border-transparent" },
  secondary: { on: "bg-white text-ink border border-line-strong hover:bg-subtle", off: "bg-warm text-soft border border-line" },
  text: {
    on: "bg-transparent text-rust underline underline-offset-[3px] hover:bg-rust-50 hover:text-rust-700",
    off: "bg-transparent text-soft",
  },
  sensitive: { on: "bg-white text-err border border-err hover:bg-err-bg", off: "bg-warm text-soft border border-line" },
  strong: { on: "bg-ink text-white border border-transparent hover:bg-charcoal", off: "bg-track text-soft border border-transparent" },
  inverse: { on: "bg-white text-ink border border-transparent hover:bg-subtle", off: "bg-inv-raised text-inv-2 border border-transparent" },
};

export function buttonClasses({
  variant = "primary",
  size = "md",
  disabled,
  fullWidth,
  className,
}: {
  variant?: ButtonVariant;
  size?: ButtonSize;
  disabled?: boolean;
  fullWidth?: boolean;
  className?: string;
}) {
  return cn(
    "relative inline-grid grid-flow-col items-center justify-center gap-1.5 font-semibold no-underline transition-colors duration-[var(--dur-fast)] select-none",
    variant === "text" ? TEXT_SIZE[size] : SIZE[size],
    disabled ? cn(VARIANT[variant].off, "cursor-not-allowed") : VARIANT[variant].on,
    fullWidth && "w-full",
    className,
  );
}
