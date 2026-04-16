/** @type {import('next').NextConfig} */
const nextConfig = {
  eslint: {
    ignoreDuringBuilds: true,
  },
  typescript: {
    ignoreBuildErrors: true,
  },
  images: {
    unoptimized: true,
  },
  // Avoid broken vendor chunk for @microsoft/signalr (MODULE_NOT_FOUND ./vendor-chunks/@microsoft.js)
  serverExternalPackages: ["@microsoft/signalr"],
  // Disable webpack filesystem cache in dev to avoid ENOENT on .pack.gz and missing middleware-manifest.json
  webpack: (config, { dev }) => {
    if (dev) config.cache = false;
    return config;
  },
}

export default nextConfig
