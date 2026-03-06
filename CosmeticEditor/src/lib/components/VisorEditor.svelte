<script lang="ts">
	/**
	 * VisorEditor.svelte
	 * Renders the full editing UI for a single visor entry, including:
	 *  - Name input with validation
	 *  - Boolean toggles: MatchPlayerColor, BehindHats
	 *  - All 5 sprite slot uploaders
	 *  - Preview panel showing the idle/preview sprites
	 *  - Reorder, duplicate, and delete controls
	 */
	import type { VisorEntry, VisorSpriteSlot } from '$lib/types';
	import { VISOR_SPRITE_SLOTS, VISOR_SPRITE_SLOT_LABELS } from '$lib/types';
	import { createPreviewUrl } from '$lib/utils/bundle';
	import SpriteUploader from './SpriteUploader.svelte';

	interface Props {
		visor: VisorEntry;
		index: number;
		total: number;
		onupdate: (id: string, visor: VisorEntry) => void;
		ondelete: (id: string) => void;
		onduplicate: (id: string) => void;
		onmoveup: (id: string) => void;
		onmovedown: (id: string) => void;
	}

	let { visor, index, total, onupdate, ondelete, onduplicate, onmoveup, onmovedown }: Props = $props();

	let isCollapsed = $state(false);

	function updateManifest(key: string, value: unknown) {
		const updated: VisorEntry = {
			...visor,
			manifest: { ...visor.manifest, [key]: value },
		};
		onupdate(visor.id, updated);
	}

	function handleSpriteUpload(slot: VisorSpriteSlot, bytes: Uint8Array, fileName: string) {
		const existingUrl = visor.previewUrls[slot];
		if (existingUrl) URL.revokeObjectURL(existingUrl);

		const url = createPreviewUrl(bytes);
		const updated: VisorEntry = {
			...visor,
			imageBytes: { ...visor.imageBytes, [slot]: bytes },
			previewUrls: { ...visor.previewUrls, [slot]: url },
			fileNames: { ...visor.fileNames, [slot]: fileName },
		};
		onupdate(visor.id, updated);
	}

	function handleSpriteClear(slot: VisorSpriteSlot) {
		const existingUrl = visor.previewUrls[slot];
		if (existingUrl) URL.revokeObjectURL(existingUrl);

		const imageBytes = { ...visor.imageBytes };
		const previewUrls = { ...visor.previewUrls };
		const fileNames = { ...visor.fileNames };
		delete imageBytes[slot];
		delete previewUrls[slot];
		delete fileNames[slot];

		onupdate(visor.id, { ...visor, imageBytes, previewUrls, fileNames });
	}

	const toggles = [
		{ key: 'MatchPlayerColor', label: 'Match Player Color', hint: 'Recolors visor to match the player color' },
		{ key: 'BehindHats', label: 'Behind Hats', hint: 'Visor renders behind hats instead of in front' },
	] as const;

	const hasName = $derived(visor.manifest.Name.trim() !== '');
</script>

<div class="visor-editor" class:has-error={!hasName}>
	<!-- Header -->
	<div class="visor-header">
		<div class="visor-header-left">
			<button
				type="button"
				class="collapse-btn"
				onclick={() => (isCollapsed = !isCollapsed)}
				aria-expanded={!isCollapsed}
				aria-label={isCollapsed ? 'Expand visor' : 'Collapse visor'}
			>
				<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor"
					class="chevron" class:rotated={isCollapsed} aria-hidden="true">
					<path fill-rule="evenodd" d="M5.22 8.22a.75.75 0 0 1 1.06 0L10 11.94l3.72-3.72a.75.75 0 1 1 1.06 1.06l-4.25 4.25a.75.75 0 0 1-1.06 0L5.22 9.28a.75.75 0 0 1 0-1.06Z" clip-rule="evenodd" />
				</svg>
			</button>
			<span class="item-index">#{index + 1}</span>
			{#if isCollapsed}
				<span class="item-name" class:unnamed={!hasName}>
					{visor.manifest.Name.trim() || 'Unnamed Visor'}
				</span>
				{#if !hasName}<span class="validation-badge">Name required</span>{/if}
			{:else}
				<input
					type="text"
					class="header-name-input"
					class:input-error={!hasName}
					value={visor.manifest.Name}
					oninput={(e) => updateManifest('Name', (e.target as HTMLInputElement).value)}
					placeholder="Visor nameâ€¦"
					aria-label="Visor name"
				/>
			{/if}
		</div>
		<div class="visor-header-right">
			<button type="button" class="icon-btn" onclick={() => onmoveup(visor.id)} disabled={index === 0} title="Move up">â†‘</button>
			<button type="button" class="icon-btn" onclick={() => onmovedown(visor.id)} disabled={index === total - 1} title="Move down">â†“</button>
			<button type="button" class="icon-btn" onclick={() => onduplicate(visor.id)} title="Duplicate">â§‰</button>
			<button type="button" class="icon-btn delete-btn" onclick={() => ondelete(visor.id)} title="Delete">âœ•</button>
		</div>
	</div>

	{#if !isCollapsed}
		<div class="visor-body">
			<div class="toggles-row">
				{#each toggles as toggle}
					<label class="toggle-label" title={toggle.hint}>
						<input type="checkbox" checked={visor.manifest[toggle.key]}
							onchange={(e) => updateManifest(toggle.key, (e.target as HTMLInputElement).checked)} />
						<span>{toggle.label}</span>
					</label>
				{/each}
			</div>

			<div class="sprite-section">
				<div class="sprite-grid">
					{#each VISOR_SPRITE_SLOTS as slot}
						<SpriteUploader
							{slot}
							previewUrl={visor.previewUrls[slot]}
							fileName={visor.fileNames[slot]}
							fileSize={visor.imageBytes[slot]?.byteLength}
							onupload={(s, bytes, name) => handleSpriteUpload(s as VisorSpriteSlot, bytes, name)}
							onclear={(s) => handleSpriteClear(s as VisorSpriteSlot)}
						/>
					{/each}
				</div>
			</div>
		</div>
	{/if}
</div>

<style>
	.visor-editor {
		background-color: #1f2937;
		border: 1px solid #374151;
		border-left: 3px solid #0d9488;
		border-radius: 0.5rem;
		overflow: hidden;
	}

	.visor-editor.has-error {
		border-left-color: #ef4444;
		border-color: #ef4444;
	}

	.visor-header {
		display: flex;
		align-items: center;
		justify-content: space-between;
		padding: 0.35rem 0.6rem;
		gap: 0.4rem;
	}

	.visor-header-left {
		display: flex;
		align-items: center;
		gap: 0.375rem;
		min-width: 0;
		flex: 1;
	}

	.visor-header-right {
		display: flex;
		align-items: center;
		gap: 0.15rem;
		flex-shrink: 0;
	}

	.collapse-btn {
		background: none;
		border: none;
		padding: 0;
		cursor: pointer;
		color: #4b5563;
		display: flex;
		align-items: center;
		flex-shrink: 0;
	}

	.collapse-btn:hover { color: #9ca3af; }

	.chevron {
		width: 14px;
		height: 14px;
		transition: transform 0.15s;
	}

	.chevron.rotated { transform: rotate(-90deg); }

	.item-index {
		font-size: 0.6rem;
		font-weight: 700;
		color: #4b5563;
		background-color: #111827;
		padding: 0.1rem 0.3rem;
		border-radius: 0.2rem;
		flex-shrink: 0;
	}

	.item-name {
		font-size: 0.85rem;
		font-weight: 600;
		color: #f3f4f6;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.item-name.unnamed { color: #6b7280; font-style: italic; }

	.header-name-input {
		flex: 1;
		min-width: 0;
		background-color: #111827;
		border: 1px solid #374151;
		border-radius: 0.3rem;
		color: #f3f4f6;
		font-size: 0.85rem;
		font-weight: 600;
		padding: 0.2rem 0.45rem;
		outline: none;
		transition: border-color 0.15s;
	}

	.header-name-input:focus { border-color: #0d9488; }
	.header-name-input.input-error { border-color: #ef4444; }

	.validation-badge {
		font-size: 0.6rem;
		background-color: #7f1d1d;
		color: #fca5a5;
		padding: 0.1rem 0.35rem;
		border-radius: 9999px;
		flex-shrink: 0;
		white-space: nowrap;
	}

	.icon-btn {
		background: none;
		border: 1px solid transparent;
		border-radius: 0.3rem;
		color: #4b5563;
		font-size: 0.75rem;
		width: 1.4rem;
		height: 1.4rem;
		cursor: pointer;
		display: flex;
		align-items: center;
		justify-content: center;
		transition: color 0.15s, border-color 0.15s, background-color 0.15s;
	}

	.icon-btn:hover:not(:disabled) { color: #e5e7eb; border-color: #4b5563; background-color: #374151; }
	.icon-btn:disabled { opacity: 0.2; cursor: not-allowed; }
	.delete-btn:hover:not(:disabled) { color: #f87171; border-color: #991b1b; background-color: #1c0a0a; }

	.visor-body {
		padding: 0.5rem 0.75rem;
		border-top: 1px solid #374151;
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.toggles-row {
		display: flex;
		flex-wrap: wrap;
		gap: 0.25rem 0.875rem;
	}

	.toggle-label {
		display: flex;
		align-items: center;
		gap: 0.3rem;
		cursor: pointer;
		font-size: 0.72rem;
		color: #9ca3af;
		white-space: nowrap;
	}

	.toggle-label input[type='checkbox'] {
		accent-color: #0d9488;
		width: 11px;
		height: 11px;
		cursor: pointer;
	}

	.sprite-section {
		display: flex;
		flex-direction: column;
		gap: 0.3rem;
	}

	.sprite-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, 60px);
		gap: 0.4rem;
	}
</style>
