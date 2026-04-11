import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    vue(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg', 'favicon-64.png', 'apple-touch-icon.png'],
      manifest: {
        name: 'Drinks & Desserts',
        short_name: 'D&D',
        description: 'Track your drinks, desserts, and cigar experiences',
        theme_color: '#0c0a09',
        background_color: '#0c0a09',
        display: 'standalone',
        scope: '/',
        start_url: '/',
        icons: [
          {
            src: 'pwa-192x192.png',
            sizes: '192x192',
            type: 'image/png',
          },
          {
            src: 'pwa-512x512.png',
            sizes: '512x512',
            type: 'image/png',
          },
          {
            src: 'pwa-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,svg,png,woff,woff2}'],
        navigateFallback: 'index.html',
        navigateFallbackDenylist: [/^\/api\//, /^\/uploads\//],
        runtimeCaching: [
          {
            // User-uploaded photos served via /uploads/ proxy
            urlPattern: /^.*\/uploads\/.+\.(jpg|jpeg|png|gif|webp|heic|heif|svg)$/i,
            handler: 'CacheFirst',
            options: {
              cacheName: 'upload-images',
              expiration: {
                maxEntries: 500,
                maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
              },
              cacheableResponse: {
                statuses: [0, 200],
              },
            },
          },
          {
            // Azure Blob Storage images
            urlPattern: /^https:\/\/.*\.blob\.core\.windows\.net\/.+$/i,
            handler: 'CacheFirst',
            options: {
              cacheName: 'blob-images',
              expiration: {
                maxEntries: 500,
                maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
              },
              cacheableResponse: {
                statuses: [0, 200],
              },
            },
          },
          {
            // API GET responses (item lists, details) — show stale while revalidating
            urlPattern: /^.*\/api\/(?!captures\/upload).+$/i,
            handler: 'NetworkFirst',
            method: 'GET',
            options: {
              cacheName: 'api-responses',
              expiration: {
                maxEntries: 100,
                maxAgeSeconds: 5 * 60, // 5 minutes
              },
              cacheableResponse: {
                statuses: [0, 200],
              },
              networkTimeoutSeconds: 5,
            },
          },
        ],
      },
    }),
  ],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5062',
        changeOrigin: true,
      },
    },
  },
})
