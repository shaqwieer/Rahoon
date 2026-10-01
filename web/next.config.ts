import type { NextConfig } from "next";

/** ASP.NET Core API origin. The browser never calls it directly: /api/* is rewritten so cookies stay first-party. */
const API_ORIGIN = process.env.API_ORIGIN ?? "http://localhost:5080";

const securityHeaders = [
  { key: "X-Frame-Options", value: "DENY" },
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "Referrer-Policy", value: "same-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(self)" },
];

const nextConfig: NextConfig = {
  poweredByHeader: false,
  reactStrictMode: true,
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${API_ORIGIN}/api/:path*` }];
  },
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          ...securityHeaders,
          // Every app page is private (09 Handoff «العام والخاص»).
          { key: "X-Robots-Tag", value: "noindex, nofollow" },
        ],
      },
      // Public pages are indexable (not the account, the team workspace or the withdrawn legacy portals).
      // Later rules override earlier ones for the same key.
      ...["/", "/sell", "/opportunities", "/opportunities/:ref", "/how-it-works", "/calculators", "/contact", "/privacy", "/terms"].map((source) => ({
        source,
        headers: [{ key: "X-Robots-Tag", value: "index, follow" }],
      })),
    ];
  },
};

export default nextConfig;
