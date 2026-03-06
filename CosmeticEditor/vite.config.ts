import tailwindcss from '@tailwindcss/vite';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';
import { resolve } from 'path';

export default defineConfig({
	plugins: [tailwindcss(), sveltekit()],
	resolve: {
		alias: {
			$lib: resolve('./src/lib'),
		},
	},
	test: {
		// Run unit tests in a Node environment (no DOM needed for bundle utils).
		environment: 'node',
		include: ['src/**/*.test.ts'],
	},
});
