import { cn } from "@/lib/cn";

export interface AvatarProps {
  /** Two-letter initials, e.g. «س ق». */
  initials: string;
  size?: 32 | 36 | 44 | 48;
  /** Circle for people, rounded tile for organizations. */
  shape?: "circle" | "tile";
  tone?: "subtle" | "ink" | "rust";
  /** Accessible name when the avatar is the only identifier; otherwise decorative. */
  label?: string;
  className?: string;
}

const TONE = { subtle: "bg-subtle text-ink", ink: "bg-ink text-white", rust: "bg-rust-50 text-rust-700" };

export function Avatar({ initials, size = 32, shape = "circle", tone = "subtle", label, className }: AvatarProps) {
  return (
    <span
      className={cn(
        "inline-flex flex-none items-center justify-center font-bold whitespace-nowrap",
        shape === "circle" ? "rounded-full" : size >= 44 ? "rounded-[8px]" : "rounded-sm",
        TONE[tone],
        size >= 44 ? "text-14" : size === 36 ? "text-14" : "text-12",
        className,
      )}
      style={{ width: size, height: size }}
      {...(label ? { role: "img", "aria-label": label } : { "aria-hidden": true })}
    >
      {initials}
    </span>
  );
}
