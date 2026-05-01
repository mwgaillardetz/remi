import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';
import electron from 'vite-plugin-electron';

const root = path.resolve(__dirname, 'src/renderer');
const distMain = path.resolve(__dirname, 'dist/main');
const distRenderer = path.resolve(__dirname, 'dist');

export default defineConfig({
  plugins: [
    react(),
    electron([
      {
        entry: path.resolve(__dirname, 'src/main/main.ts'),
        onstart(options) {
          options.reload();
        },
        vite: {
          build: {
            outDir: distMain,
            emptyOutDir: false,
            rollupOptions: {
              external: ['electron'],
            },
          },
        },
      },
      {
        entry: path.resolve(__dirname, 'src/renderer/preload.ts'),
        onstart(options) {
          options.reload();
        },
        vite: {
          build: {
            outDir: distMain,
            emptyOutDir: false,
            rollupOptions: {
              external: ['electron'],
            },
          },
        },
      },
    ]),
  ],
  base: './',
  root,
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 3369,
  },
  build: {
    outDir: distRenderer,
    emptyOutDir: true,
  },
});
