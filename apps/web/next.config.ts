import path from "node:path";

import type { NextConfig } from "next";

/**
 * The browser reaches the API through the same origin path /api/v1. The route
 * handler under src/app/api forwards it to the API address, which is read from
 * the environment at request time.
 */
const nextConfig: NextConfig = {
  reactStrictMode: true,
  transpilePackages: ["@blindmesh/api-client"],
  // The generated API client lives in packages/api-client, outside the web
  // application directory.
  turbopack: {
    root: path.join(process.cwd(), "../.."),
  },
};

export default nextConfig;
