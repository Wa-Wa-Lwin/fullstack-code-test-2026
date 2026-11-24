import type { NextConfig } from "next";

// The browser calls this app's own /api/*, and Next.js forwards each call to the .NET API.
// The browser never calls the API directly, so the API needs no CORS setup, and in Docker
// the API can be reached by its service name. Rewrites are resolved when the app is built
// (or when `next dev` starts), so set API_BASE_URL before either.
const apiBaseUrl = process.env.API_BASE_URL ?? "http://localhost:5263";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  experimental: {
    // A saved-card checkout makes up to three 2C2P calls in a row (Payment Token, Do Payment,
    // Payment Inquiry), each with a 15s timeout. The proxy's default 30s timeout could cut
    // it off and leave the customer without an answer.
    proxyTimeout: 90_000,
  },
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${apiBaseUrl}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;
