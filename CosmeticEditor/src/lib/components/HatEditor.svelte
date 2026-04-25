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
	import type { BundleEditorGroup, HatEntry, SpriteSlot } from '$lib/types';
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
		groups: BundleEditorGroup[];
		ongroupchange: (id: string, groupId: string) => void;
	}

	let { hat, index, total, onupdate, ondelete, onduplicate, onmoveup, onmovedown, groups, ongroupchange }: Props = $props();

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

	function handleGroupChange(event: Event) {
		ongroupchange(hat.id, (event.target as HTMLSelectElement).value);
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
	<!-- Header -->
	<div class="hat-header">
		<div class="hat-header-left">
			<button
				type="button"
				class="collapse-btn"
				onclick={() => (isCollapsed = !isCollapsed)}
				aria-expanded={!isCollapsed}
				aria-label={isCollapsed ? 'Expand hat' : 'Collapse hat'}
			>
				<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor"
					class="chevron" class:rotated={isCollapsed} aria-hidden="true">
					<path fill-rule="evenodd" d="M5.22 8.22a.75.75 0 0 1 1.06 0L10 11.94l3.72-3.72a.75.75 0 1 1 1.06 1.06l-4.25 4.25a.75.75 0 0 1-1.06 0L5.22 9.28a.75.75 0 0 1 0-1.06Z" clip-rule="evenodd" />
				</svg>
			</button>
			<span class="item-index">#{index + 1}</span>
			{#if isCollapsed}
				<span class="item-name" class:unnamed={!hasName}>
					{hat.manifest.Name.trim() || 'Unnamed Hat'}
				</span>
				{#if !hasName}<span class="validation-badge">Name required</span>{/if}
			{:else}
				<input
					type="text"
					class="header-name-input"
					class:input-error={!hasName}
					value={hat.manifest.Name}
					oninput={(e) => updateManifest('Name', (e.target as HTMLInputElement).value)}
					placeholder="Hat name…"
					aria-label="Hat name"
				/>
			{/if}
		</div>
		<div class="hat-header-right">
			{#if groups.length > 1}
				<label class="group-select-wrapper" title="Move to group">
					<span class="sr-only">Group</span>
					<select class="group-select" value={hat.groupId} onchange={handleGroupChange} aria-label="Hat group">
						{#each groups as group (group.id)}
							<option value={group.id}>{group.name}</option>
						{/each}
					</select>
				</label>
			{/if}
			<button type="button" class="icon-btn" onclick={() => onmoveup(hat.id)} disabled={index === 0} title="Move up">↑</button>
			<button type="button" class="icon-btn" onclick={() => onmovedown(hat.id)} disabled={index === total - 1} title="Move down">↓</button>
			<button type="button" class="icon-btn" onclick={() => onduplicate(hat.id)} title="Duplicate">⧉</button>
			<button type="button" class="icon-btn delete-btn" onclick={() => ondelete(hat.id)} title="Delete">✕</button>
		</div>
	</div>

	{#if !isCollapsed}
		<div class="hat-body">
			<div class="toggles-row">
				{#each toggles as toggle (toggle.key)}
					<label class="toggle-label" title={toggle.hint}>
						<input type="checkbox" checked={hat.manifest[toggle.key]}
							onchange={(e) => updateManifest(toggle.key, (e.target as HTMLInputElement).checked)} />
						<span>{toggle.label}</span>
					</label>
				{/each}
			</div>

			{#each spriteRows as row (row.label)}
				<div class="sprite-section">
					<span class="sprite-section-label">{row.label}</span>
					<div class="sprite-grid">
						{#each row.slots as slot (slot)}
							<SpriteUploader
								{slot}
								previewUrl={hat.previewUrls[slot]}
								fileName={hat.fileNames[slot]}
								fileSize={hat.imageBytes[slot]?.byteLength}
							onupload={(s, bytes, name) => handleSpriteUpload(s as SpriteSlot, bytes, name)}
							onclear={(s) => handleSpriteClear(s as SpriteSlot)}
							/>
						{/each}
					</div>
				</div>
			{/each}
		</div>
	{/if}
</div>

<style>
	.hat-editor {
		background-color: #1f2937;
		border: 1px solid #374151;
		border-left: 3px solid #6d28d9;
		border-radius: 0.5rem;
		overflow: hidden;
	}

	.hat-editor.has-error {
		border-left-color: #ef4444;
		border-color: #ef4444;
	}

	.hat-header {
		display: flex;
		align-items: center;
		justify-content: space-between;
		padding: 0.35rem 0.6rem;
		gap: 0.4rem;
	}

	.hat-header-left {
		display: flex;
		align-items: center;
		gap: 0.375rem;
		min-width: 0;
		flex: 1;
	}

	.hat-header-right {
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

	.header-name-input:focus { border-color: #7c3aed; }
	.header-name-input.input-error { border-color: #ef4444; }

	.group-select-wrapper {
		display: flex;
		align-items: center;
	}

	.group-select {
		background-color: #111827;
		border: 1px solid #374151;
		border-radius: 0.3rem;
		color: #d1d5db;
		font-size: 0.7rem;
		padding: 0.15rem 0.35rem;
		max-width: 8rem;
	}

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

	.hat-body {
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
		accent-color: #7c3aed;
		width: 11px;
		height: 11px;
		cursor: pointer;
	}

	.sprite-section {
		display: flex;
		flex-direction: column;
		gap: 0.3rem;
	}

	.sprite-section-label {
		font-size: 0.6rem;
		font-weight: 700;
		color: #374151;
		text-transform: uppercase;
		letter-spacing: 0.07em;
	}

	.sprite-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, 60px);
		gap: 0.4rem;
	}
</style>
