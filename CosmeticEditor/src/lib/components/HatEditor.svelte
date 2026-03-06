<script lang="ts">
	/**
	 * HatEditor.svelte
	 * Renders the full editing UI for a single hat entry, including:
	 *  - Name input with validation
	 *  - Boolean toggles: MatchPlayerColor, BlocksVisors, InFront, NoBounce
	 *  - All 9 sprite slot uploaders arranged in a grid
	 *  - Preview panel showing the main/preview sprites
	 *  - Reorder, duplicate, and delete controls
	 */
	import type { HatEntry, SpriteSlot } from '$lib/types';
	import { SPRITE_SLOTS } from '$lib/types';
	import { createPreviewUrl } from '$lib/utils/bundle';
	import SpriteUploader from './SpriteUploader.svelte';

	interface Props {
		hat: HatEntry;
		index: number;
		total: number;
		onupdate: (id: string, hat: HatEntry) => void;
		ondelete: (id: string) => void;
		onduplicate: (id: string) => void;
		onmoveup: (id: string) => void;
		onmovedown: (id: string) => void;
	}

	let { hat, index, total, onupdate, ondelete, onduplicate, onmoveup, onmovedown }: Props = $props();

	let isCollapsed = $state(false);

	function updateManifest(key: string, value: unknown) {
		const updated: HatEntry = {
			...hat,
			manifest: { ...hat.manifest, [key]: value },
		};
		onupdate(hat.id, updated);
	}

	function handleSpriteUpload(slot: SpriteSlot, bytes: Uint8Array, fileName: string) {
		// Revoke existing URL to prevent memory leaks
		const existingUrl = hat.previewUrls[slot];
		if (existingUrl) URL.revokeObjectURL(existingUrl);

		const url = createPreviewUrl(bytes);
		const updated: HatEntry = {
			...hat,
			imageBytes: { ...hat.imageBytes, [slot]: bytes },
			previewUrls: { ...hat.previewUrls, [slot]: url },
			fileNames: { ...hat.fileNames, [slot]: fileName },
		};
		onupdate(hat.id, updated);
	}

	function handleSpriteClear(slot: SpriteSlot) {
		const existingUrl = hat.previewUrls[slot];
		if (existingUrl) URL.revokeObjectURL(existingUrl);

		const imageBytes = { ...hat.imageBytes };
		const previewUrls = { ...hat.previewUrls };
		const fileNames = { ...hat.fileNames };
		delete imageBytes[slot];
		delete previewUrls[slot];
		delete fileNames[slot];

		onupdate(hat.id, { ...hat, imageBytes, previewUrls, fileNames });
	}

	const toggles = [
		{ key: 'MatchPlayerColor', label: 'Match Player Color', hint: 'Recolors hat to match the player color' },
		{ key: 'BlocksVisors', label: 'Blocks Visors', hint: 'Visor is hidden when this hat is worn' },
		{ key: 'InFront', label: 'In Front', hint: 'Hat renders in front of the player body' },
		{ key: 'NoBounce', label: 'No Bounce', hint: 'Disables the hat bounce animation' },
	] as const;

	// Group sprite slots into rows for the grid layout
	const spriteRows: { label: string; slots: SpriteSlot[] }[] = [
		{ label: 'Standard Sprites', slots: ['PreviewSprite', 'MainSprite', 'BackSprite', 'ClimbSprite', 'FloorSprite'] },
		{
			label: 'Left-Facing Sprites',
			slots: ['LeftMainSprite', 'LeftBackSprite', 'LeftClimbSprite', 'LeftFloorSprite'],
		},
	];

	const hasName = $derived(hat.manifest.Name.trim() !== '');
</script>

<div class="hat-editor" class:has-error={!hasName}>
	<!-- Hat header bar -->
	<div class="hat-header">
		<div class="hat-header-left">
			<button
				type="button"
				class="collapse-btn"
				onclick={() => (isCollapsed = !isCollapsed)}
				aria-expanded={!isCollapsed}
				aria-label={isCollapsed ? 'Expand hat' : 'Collapse hat'}
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					viewBox="0 0 20 20"
					fill="currentColor"
					class="chevron"
					class:rotated={isCollapsed}
					aria-hidden="true"
				>
					<path
						fill-rule="evenodd"
						d="M5.22 8.22a.75.75 0 0 1 1.06 0L10 11.94l3.72-3.72a.75.75 0 1 1 1.06 1.06l-4.25 4.25a.75.75 0 0 1-1.06 0L5.22 9.28a.75.75 0 0 1 0-1.06Z"
						clip-rule="evenodd"
					/>
				</svg>
			</button>
			<span class="hat-index">#{index + 1}</span>
			<span class="hat-name-display" class:unnamed={!hasName}>
				{hat.manifest.Name.trim() || 'Unnamed Hat'}
			</span>
			{#if !hasName}
				<span class="validation-badge">Name required</span>
			{/if}
		</div>
		<div class="hat-header-right">
			<!-- Reorder buttons -->
			<button
				type="button"
				class="icon-btn"
				onclick={() => onmoveup(hat.id)}
				disabled={index === 0}
				title="Move up"
				aria-label="Move hat up"
			>
				↑
			</button>
			<button
				type="button"
				class="icon-btn"
				onclick={() => onmovedown(hat.id)}
				disabled={index === total - 1}
				title="Move down"
				aria-label="Move hat down"
			>
				↓
			</button>
			<button
				type="button"
				class="icon-btn"
				onclick={() => onduplicate(hat.id)}
				title="Duplicate hat"
				aria-label="Duplicate hat"
			>
				⧉
			</button>
			<button
				type="button"
				class="icon-btn delete-btn"
				onclick={() => ondelete(hat.id)}
				title="Delete hat"
				aria-label="Delete hat"
			>
				✕
			</button>
		</div>
	</div>

	<!-- Collapsible body -->
	{#if !isCollapsed}
		<div class="hat-body">
			<!-- Name + Toggles row -->
			<div class="fields-row">
				<div class="field-group name-group">
					<label for="hat-name-{hat.id}" class="field-label">Hat Name</label>
					<input
						id="hat-name-{hat.id}"
						type="text"
						class="text-input"
						class:input-error={!hasName}
						value={hat.manifest.Name}
						oninput={(e) => updateManifest('Name', (e.target as HTMLInputElement).value)}
						placeholder="Enter hat name…"
						aria-required="true"
						aria-invalid={!hasName}
					/>
					{#if !hasName}
						<span class="field-error">Hat name cannot be empty</span>
					{/if}
				</div>

				<div class="toggles-group">
					{#each toggles as toggle}
						<label class="toggle-label" title={toggle.hint}>
							<input
								type="checkbox"
								checked={hat.manifest[toggle.key]}
								onchange={(e) =>
									updateManifest(toggle.key, (e.target as HTMLInputElement).checked)}
							/>
							<span class="toggle-text">{toggle.label}</span>
						</label>
					{/each}
				</div>
			</div>

			<!-- Sprite slots -->
			{#each spriteRows as row}
				<div class="sprite-row-section">
					<h4 class="sprite-row-label">{row.label}</h4>
					<div class="sprite-grid">
						{#each row.slots as slot}
							<SpriteUploader
								{slot}
								previewUrl={hat.previewUrls[slot]}
								fileName={hat.fileNames[slot]}
								fileSize={hat.imageBytes[slot]?.byteLength}
								onupload={handleSpriteUpload}
								onclear={handleSpriteClear}
							/>
						{/each}
					</div>
				</div>
			{/each}

			<!-- Quick preview panel: show Preview and Main sprites side by side -->
			{#if hat.previewUrls['PreviewSprite'] || hat.previewUrls['MainSprite']}
				<div class="preview-panel">
					<span class="preview-panel-label">Hat Preview</span>
					<div class="preview-images">
						{#if hat.previewUrls['PreviewSprite']}
							<div class="preview-item">
								<img
									src={hat.previewUrls['PreviewSprite']}
									alt="Preview sprite"
									class="preview-large"
								/>
								<span>Preview</span>
							</div>
						{/if}
						{#if hat.previewUrls['MainSprite']}
							<div class="preview-item">
								<img
									src={hat.previewUrls['MainSprite']}
									alt="Main sprite"
									class="preview-large"
								/>
								<span>Main</span>
							</div>
						{/if}
					</div>
				</div>
			{/if}
		</div>
	{/if}
</div>

<style>
	.hat-editor {
		background-color: #1f2937;
		border: 1px solid #374151;
		border-radius: 0.625rem;
		overflow: hidden;
		transition: border-color 0.15s;
	}

	.hat-editor.has-error {
		border-color: #ef4444;
	}

	/* Header */
	.hat-header {
		display: flex;
		align-items: center;
		justify-content: space-between;
		padding: 0.6rem 0.75rem;
		background-color: #111827;
		border-bottom: 1px solid #374151;
		gap: 0.5rem;
	}

	.hat-header-left {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		min-width: 0;
	}

	.hat-header-right {
		display: flex;
		align-items: center;
		gap: 0.25rem;
		flex-shrink: 0;
	}

	.collapse-btn {
		background: none;
		border: none;
		padding: 0;
		cursor: pointer;
		color: #9ca3af;
		display: flex;
		align-items: center;
	}

	.chevron {
		width: 16px;
		height: 16px;
		transition: transform 0.15s;
	}

	.chevron.rotated {
		transform: rotate(-90deg);
	}

	.hat-index {
		font-size: 0.7rem;
		font-weight: 700;
		color: #6b7280;
		background-color: #374151;
		padding: 0.1rem 0.35rem;
		border-radius: 0.25rem;
		flex-shrink: 0;
	}

	.hat-name-display {
		font-size: 0.9rem;
		font-weight: 600;
		color: #f3f4f6;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.hat-name-display.unnamed {
		color: #9ca3af;
		font-style: italic;
	}

	.validation-badge {
		font-size: 0.65rem;
		background-color: #7f1d1d;
		color: #fca5a5;
		padding: 0.1rem 0.4rem;
		border-radius: 9999px;
		flex-shrink: 0;
	}

	.icon-btn {
		background: none;
		border: 1px solid #374151;
		border-radius: 0.375rem;
		color: #9ca3af;
		font-size: 0.8rem;
		width: 1.75rem;
		height: 1.75rem;
		cursor: pointer;
		display: flex;
		align-items: center;
		justify-content: center;
		transition:
			color 0.15s,
			border-color 0.15s,
			background-color 0.15s;
	}

	.icon-btn:hover:not(:disabled) {
		color: #e5e7eb;
		border-color: #6b7280;
		background-color: #374151;
	}

	.icon-btn:disabled {
		opacity: 0.3;
		cursor: not-allowed;
	}

	.delete-btn:hover:not(:disabled) {
		color: #f87171;
		border-color: #ef4444;
		background-color: #1f0a0a;
	}

	/* Body */
	.hat-body {
		padding: 0.875rem;
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	/* Name + toggles */
	.fields-row {
		display: flex;
		gap: 1.25rem;
		flex-wrap: wrap;
		align-items: flex-start;
	}

	.name-group {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
		min-width: 200px;
		flex: 1;
	}

	.field-label {
		font-size: 0.75rem;
		font-weight: 600;
		color: #9ca3af;
		text-transform: uppercase;
		letter-spacing: 0.05em;
	}

	.text-input {
		background-color: #111827;
		border: 1px solid #374151;
		border-radius: 0.375rem;
		color: #f3f4f6;
		font-size: 0.875rem;
		padding: 0.4rem 0.6rem;
		outline: none;
		transition: border-color 0.15s;
		width: 100%;
	}

	.text-input:focus {
		border-color: #7c3aed;
		box-shadow: 0 0 0 2px rgba(124, 58, 237, 0.2);
	}

	.text-input.input-error {
		border-color: #ef4444;
	}

	.field-error {
		font-size: 0.7rem;
		color: #f87171;
	}

	.toggles-group {
		display: flex;
		flex-wrap: wrap;
		gap: 0.5rem 1rem;
		align-items: center;
		padding-top: 1.25rem;
	}

	.toggle-label {
		display: flex;
		align-items: center;
		gap: 0.35rem;
		cursor: pointer;
		font-size: 0.8rem;
		color: #d1d5db;
		white-space: nowrap;
	}

	.toggle-label input[type='checkbox'] {
		accent-color: #7c3aed;
		width: 14px;
		height: 14px;
		cursor: pointer;
	}

	.toggle-text {
		user-select: none;
	}

	/* Sprite sections */
	.sprite-row-section {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.sprite-row-label {
		font-size: 0.7rem;
		font-weight: 700;
		color: #6b7280;
		text-transform: uppercase;
		letter-spacing: 0.08em;
		margin: 0;
	}

	.sprite-grid {
		display: flex;
		flex-wrap: wrap;
		gap: 0.75rem;
	}

	/* Preview panel */
	.preview-panel {
		background-color: #111827;
		border: 1px solid #374151;
		border-radius: 0.5rem;
		padding: 0.75rem;
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.preview-panel-label {
		font-size: 0.7rem;
		font-weight: 700;
		color: #6b7280;
		text-transform: uppercase;
		letter-spacing: 0.08em;
	}

	.preview-images {
		display: flex;
		gap: 1.5rem;
	}

	.preview-item {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 0.25rem;
		font-size: 0.65rem;
		color: #9ca3af;
	}

	.preview-large {
		width: 64px;
		height: 64px;
		object-fit: contain;
		image-rendering: pixelated;
		background-color: #1f2937;
		border: 1px solid #374151;
		border-radius: 0.375rem;
	}
</style>
