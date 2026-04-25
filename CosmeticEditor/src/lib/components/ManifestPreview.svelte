<script lang="ts">
	/**
	 * ManifestPreview.svelte
	 * Shows a debug/info panel with computed bundle statistics:
	 * - Number of hats, total sprites
	 * - ManifestLength and DataLength from the last download
	 * - A pretty-printed manifest JSON preview
	 */
	import type { BundleVersion, BundleEditorGroup, HatEntry, VisorEntry, NameplateEntry } from '$lib/types';
	import { SPRITE_SLOTS, VISOR_SPRITE_SLOTS, NAMEPLATE_SPRITE_SLOTS, createBundleManifest } from '$lib/types';
	import { HEADER_SIZE, describeBundleVersion, serializeManifest } from '$lib/utils/bundle';

	interface Props {
		bundleVersion: BundleVersion;
		groups: BundleEditorGroup[];
		hats: HatEntry[];
		visors: VisorEntry[];
		nameplates: NameplateEntry[];
		lastManifestLength: number | null;
		lastDataLength: number | null;
	}

	let { bundleVersion, groups, hats, visors, nameplates, lastManifestLength, lastDataLength }: Props = $props();

	let showJson = $state(false);

	const stats = $derived.by(() => {
		let spriteCount = 0;
		let totalDataBytes = 0;
		for (const hat of hats) {
			for (const slot of SPRITE_SLOTS) {
				const bytes = hat.imageBytes[slot];
				if (bytes && bytes.byteLength > 0) {
					spriteCount++;
					totalDataBytes += bytes.byteLength;
				}
			}
		}
		for (const visor of visors) {
			for (const slot of VISOR_SPRITE_SLOTS) {
				const bytes = visor.imageBytes[slot];
				if (bytes && bytes.byteLength > 0) {
					spriteCount++;
					totalDataBytes += bytes.byteLength;
				}
			}
		}
		for (const nameplate of nameplates) {
			for (const slot of NAMEPLATE_SPRITE_SLOTS) {
				const bytes = nameplate.imageBytes[slot];
				if (bytes && bytes.byteLength > 0) {
					spriteCount++;
					totalDataBytes += bytes.byteLength;
				}
			}
		}
		return { spriteCount, totalDataBytes };
	});

	function formatBytes(bytes: number): string {
		if (bytes === 0) return '0 B';
		if (bytes < 1024) return `${bytes} B`;
		if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
		return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
	}

	/** Live estimated manifest size (before download is triggered) */
	const estimatedManifestLength = $derived.by(() => {
		if (hats.length === 0 && visors.length === 0 && nameplates.length === 0) return 0;
		const manifest = createBundleManifest(
			bundleVersion,
			hats.map((hat) => ({ ...hat.manifest })),
			visors.map((visor) => ({ ...visor.manifest })),
			nameplates.map((np) => ({ ...np.manifest })),
			bundleVersion === 2
				? groups.map((group) => ({
					Name: group.name,
					Hats: hats.filter((hat) => hat.groupId === group.id).map((hat) => ({ ...hat.manifest })),
					Visors: visors.filter((visor) => visor.groupId === group.id).map((visor) => ({ ...visor.manifest })),
					Nameplates: nameplates.filter((nameplate) => nameplate.groupId === group.id).map((nameplate) => ({ ...nameplate.manifest })),
				}))
				: []
		);
		try {
			return serializeManifest(manifest).byteLength;
		} catch {
			return 0;
		}
	});

	const totalBundleSize = $derived(HEADER_SIZE + estimatedManifestLength + stats.totalDataBytes);
</script>

<div class="manifest-preview">
	<div class="section-header">
		<h3 class="section-title">Bundle Statistics</h3>
		<button
			type="button"
			class="toggle-json-btn"
			onclick={() => (showJson = !showJson)}
			aria-expanded={showJson}
		>
			{showJson ? 'Hide' : 'Show'} Manifest JSON
		</button>
	</div>

	<div class="stats-grid">
		<div class="stat-item">
			<span class="stat-label">Hats</span>
			<span class="stat-value">{hats.length}</span>
		</div>
		<div class="stat-item">
			<span class="stat-label">Visors</span>
			<span class="stat-value">{visors.length}</span>
		</div>
		<div class="stat-item">
			<span class="stat-label">Nameplates</span>
			<span class="stat-value">{nameplates.length}</span>
		</div>
		<div class="stat-item highlight">
			<span class="stat-label">Bundle Version</span>
			<span class="stat-value">{describeBundleVersion(bundleVersion)}</span>
		</div>
		<div class="stat-item">
			<span class="stat-label">Sprites</span>
			<span class="stat-value">{stats.spriteCount}</span>
		</div>
		<div class="stat-item">
			<span class="stat-label">Header</span>
			<span class="stat-value">{HEADER_SIZE} B</span>
		</div>
		<div class="stat-item">
			<span class="stat-label">Manifest (est.)</span>
			<span class="stat-value">{formatBytes(estimatedManifestLength)}</span>
		</div>
		<div class="stat-item">
			<span class="stat-label">Data</span>
			<span class="stat-value">{formatBytes(stats.totalDataBytes)}</span>
		</div>
		<div class="stat-item highlight">
			<span class="stat-label">Total (est.)</span>
			<span class="stat-value">{formatBytes(totalBundleSize)}</span>
		</div>
	</div>

	{#if lastManifestLength !== null || lastDataLength !== null}
		<div class="last-download">
			<span class="last-label">Last download —</span>
			{#if lastManifestLength !== null}
				<span class="last-item">ManifestLength: <code>{lastManifestLength}</code></span>
			{/if}
			{#if lastDataLength !== null}
				<span class="last-item">DataLength: <code>{lastDataLength}</code></span>
			{/if}
		</div>
	{/if}

	{#if showJson}
		<div class="json-section">
			<pre class="json-block">{JSON.stringify(
					{
						...createBundleManifest(
							bundleVersion,
							hats.map((h) => h.manifest),
							visors.map((v) => v.manifest),
							nameplates.map((n) => n.manifest),
							bundleVersion === 2
								? groups.map((group) => ({
									Name: group.name,
									Hats: hats.filter((hat) => hat.groupId === group.id).map((hat) => hat.manifest),
									Visors: visors.filter((visor) => visor.groupId === group.id).map((visor) => visor.manifest),
									Nameplates: nameplates.filter((nameplate) => nameplate.groupId === group.id).map((nameplate) => nameplate.manifest),
								}))
								: []
						),
					},
					null,
					2
				)}</pre>
		</div>
	{/if}
</div>

<style>
	.manifest-preview {
		background-color: #111827;
		border: 1px solid #374151;
		border-radius: 0.625rem;
		padding: 0.875rem;
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	.section-header {
		display: flex;
		align-items: center;
		justify-content: space-between;
	}

	.section-title {
		font-size: 0.8rem;
		font-weight: 700;
		color: #9ca3af;
		text-transform: uppercase;
		letter-spacing: 0.08em;
		margin: 0;
	}

	.toggle-json-btn {
		font-size: 0.7rem;
		background: none;
		border: 1px solid #374151;
		border-radius: 0.375rem;
		color: #9ca3af;
		padding: 0.2rem 0.6rem;
		cursor: pointer;
		transition:
			color 0.15s,
			border-color 0.15s;
	}

	.toggle-json-btn:hover {
		color: #e5e7eb;
		border-color: #6b7280;
	}

	.stats-grid {
		display: grid;
		grid-template-columns: repeat(3, 1fr);
		gap: 0.5rem;
	}

	.stat-item {
		background-color: #1f2937;
		border-radius: 0.375rem;
		padding: 0.4rem 0.6rem;
		display: flex;
		flex-direction: column;
		gap: 0.1rem;
	}

	.stat-item.highlight {
		background-color: #1e1b4b;
		border: 1px solid #4c1d95;
	}

	.stat-label {
		font-size: 0.6rem;
		color: #6b7280;
		text-transform: uppercase;
		letter-spacing: 0.05em;
	}

	.stat-value {
		font-size: 0.85rem;
		font-weight: 600;
		color: #e5e7eb;
		font-variant-numeric: tabular-nums;
	}

	.last-download {
		display: flex;
		flex-wrap: wrap;
		gap: 0.5rem;
		align-items: center;
		font-size: 0.7rem;
		color: #9ca3af;
		background-color: #0f172a;
		border: 1px solid #1e293b;
		border-radius: 0.375rem;
		padding: 0.4rem 0.6rem;
	}

	.last-label {
		color: #6b7280;
	}

	.last-item {
		color: #9ca3af;
	}

	.last-item code {
		font-family: ui-monospace, monospace;
		background-color: #1e293b;
		padding: 0 0.3rem;
		border-radius: 0.2rem;
		color: #a5f3fc;
	}

	.json-section {
		border-top: 1px solid #374151;
		padding-top: 0.5rem;
	}

	.json-block {
		background-color: #0f172a;
		border: 1px solid #1e293b;
		border-radius: 0.375rem;
		padding: 0.75rem;
		font-family: ui-monospace, 'Cascadia Code', monospace;
		font-size: 0.7rem;
		color: #a3e635;
		overflow-x: auto;
		white-space: pre;
		margin: 0;
		max-height: 400px;
		overflow-y: auto;
	}
</style>
