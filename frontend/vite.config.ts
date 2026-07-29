import { configDefaults, defineConfig } from 'vitest/config'
import { loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "")
  const backendOrigin = env.VITE_DEV_BACKEND_ORIGIN ?? "http://127.0.0.1:5265"

  return {
    plugins: [react()],
    server: {
      proxy: {
        "/api": {
          target: backendOrigin,
          changeOrigin: true,
        },
        "/hubs": {
          target: backendOrigin,
          changeOrigin: true,
          ws: true,
        },
      },
    },
    build: {
      rollupOptions: {
        output: {
          manualChunks: {
            react: ["react", "react-dom", "react-router-dom"],
            mui: ["@mui/material", "@mui/icons-material", "@emotion/react", "@emotion/styled"],
            dataGrid: ["@mui/x-data-grid"],
            signalr: ["@microsoft/signalr"],
          },
        },
      },
    },
    test: {
      environment: 'jsdom',
      setupFiles: './src/test/setup.ts',
      globals: true,
      include: ['src/**/*.{test,spec}.{ts,tsx}'],
      exclude: [...configDefaults.exclude, 'e2e/**', 'playwright.config.ts'],
    },
  }
})
