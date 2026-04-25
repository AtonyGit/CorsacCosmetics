<script lang="ts">
	/**
	 * NameplateEditor.svelte
	 * Renders the full editing UI for a single nameplate entry, including:
	 *  - Name input with validation
	 *  - PreviewSprite and NameplateSprite uploaders
	 *  - Reorder, duplicate, and delete controls
	 */
	import type { BundleEditorGroup, NameplateEntry, NameplateSpriteSlot } from '$lib/types';
	import { NAMEPLATE_SPRITE_SLOTS } from '$lib/types';
	import { createPreviewUrl } from '$lib/utils/bundle';
	import SpriteUploader from './SpriteUploader.svelte';

	interface Props {
		nameplate: NameplateEntry;
		index: number;
		total: number;
		onupdate: (id: string, nameplate: NameplateEntry) => void;
		ondelete: (id: string) => void;
		onduplicate: (id: string) => void;
		onmoveup: (id: string) => void;
		onmovedown: (id: string) => void;
		groups: BundleEditorGroup[];
		ongroupchange: (id: string, groupId: string) => void;
	}

	let { nameplate, index, total, onupdate, ondelete, onduplicate, onmoveup, onmovedown, groups, ongroupchange }: Props = $props();

	let isCollapsed = $state(false);

	function updateManifest(key: string, value: unknown) {
		const updated: NameplateEntry = {
			...nameplate,
			manifest: { ...nameplate.manifest, [key]: value },
		};
		onupdate(nameplate.id, updated);
	}

	function handleSpriteUpload(slot: NameplateSpriteSlot, bytes: Uint8Array, fileName: string) {
		const existingUrl = nameplate.previewUrls[slot];
		if (existingUrl) URL.revokeObjectURL(existingUrl);

		const url = createPreviewUrl(bytes);
		const updated: NameplateEntry = {
			...nameplate,
			imageBytes: { ...nameplate.imageBytes, [slot]: bytes },
			previewUrls: { ...nameplate.previewUrls, [slot]: url },
			fileNames: { ...nameplate.fileNames, [slot]: fileName },
		};
		onupdate(nameplate.id, updated);
	}

	function handleSpriteClear(slot: NameplateSpriteSlot) {
		const existingUrl = nameplate.previewUrls[slot];
		if (existingUrl) URL.revokeObjectURL(existingUrl);

		const imageBytes = { ...nameplate.imageBytes };
		const previewUrls = { ...nameplate.previewUrls };
		const fileNames = { ...nameplate.fileNames };
		delete imageBytes[slot];
		delete previewUrls[slot];
		delete fileNames[slot];

		onupdate(nameplate.id, { ...nameplate, imageBytes, previewUrls, fileNames });
	}

	function handleGroupChange(event: Event) {
		ongroupchange(nameplate.id, (event.target as HTMLSelectElement).value);
	}

	const hasName = $derived(nameplate.manifest.Name.trim() !== '');
</script>

<div class="nameplate-editor" class:has-error={!hasName}>
	<!-- Header -->
	<div class="nameplate-header">
		<div class="nameplate-header-left">
			<button
				type="button"
				class="collapse-btn"
				onclick={() => (isCollapsed = !isCollapsed)}
				aria-expanded={!isCollapsed}
				aria-label={isCollapsed ? 'Expand nameplate' : 'Collapse nameplate'}
			>
				<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor"
					class="chevron" class:rotated={isCollapsed} aria-hidden="true">
					<path fill-rule="evenodd" d="M5.22 8.22a.75.75 0 0 1 1.06 0L10 11.94l3.72-3.72a.75.75 0 1 1 1.06 1.06l-4.25 4.25a.75.75 0 0 1-1.06 0L5.22 9.28a.75.75 0 0 1 0-1.06Z" clip-rule="evenodd" />
				</svg>
			</button>
			<span class="item-index">#{index + 1}</span>
			{#if isCollapsed}
				<span class="item-name" class:unnamed={!hasName}>
					{nameplate.manifest.Name.trim() || 'Unnamed Nameplate'}
				</span>
				{#if !hasName}<span class="validation-badge">Name required</span>{/if}
			{:else}
				<input
					type="text"
					class="header-name-input"
					class:input-error={!hasName}
					value={nameplate.manifest.Name}
					oninput={(e) => updateManifest('Name', (e.target as HTMLInputElement).value)}
					placeholder="Nameplate name…"
					aria-label="Nameplate name"
				/>
			{/if}
		</div>
		<div class="nameplate-header-right">
			{#if groups.length > 1}
				<label class="group-select-wrapper" title="Move to group">
					<span class="sr-only">Group</span>
					<select class="group-select" value={nameplate.groupId} onchange={handleGroupChange} aria-label="Nameplate group">
						{#each groups as group (group.id)}
							<option value={group.id}>{group.name}</option>
						{/each}
					</select>
				</label>
			{/if}
			<button type="button" class="icon-btn" onclick={() => onmoveup(nameplate.id)} disabled={index === 0} title="Move up">↑</button>
			<button type="button" class="icon-btn" onclick={() => onmovedown(nameplate.id)} disabled={index === total - 1} title="Move down">↓</button>
			<button type="button" class="icon-btn" onclick={() => onduplicate(nameplate.id)} title="Duplicate">⧉</button>
			<button type="button" class="icon-btn delete-btn" onclick={() => ondelete(nameplate.id)} title="Delete">✕</button>
		</div>
	</div>

	{#if !isCollapsed}
		<div class="nameplate-body">
			<div class="sprite-section">
				<div class="sprite-grid">
					{#each NAMEPLATE_SPRITE_SLOTS as slot (slot)}
						<SpriteUploader
							{slot}
							previewUrl={nameplate.previewUrls[slot]}
							fileName={nameplate.fileNames[slot]}
							fileSize={nameplate.imageBytes[slot]?.byteLength}
							onupload={(s, bytes, name) => handleSpriteUpload(s as NameplateSpriteSlot, bytes, name)}
							onclear={(s) => handleSpriteClear(s as NameplateSpriteSlot)}
						/>
					{/each}
				</div>
			</div>
		</div>
	{/if}
</div>

<style>
	.nameplate-editor {
		background-color: #1f2937;
		border: 1px solid #374151;
		border-left: 3px solid #7c3aed;
		border-radius: 0.5rem;
		overflow: hidden;
	}

	.nameplate-editor.has-error {
		border-left-color: #ef4444;
		border-color: #ef4444;
	}

	.nameplate-header {
		display: flex;
		align-items: center;
		justify-content: space-between;
		padding: 0.35rem 0.6rem;
		gap: 0.4rem;
	}

	.nameplate-header-left {
		display: flex;
		align-items: center;
		gap: 0.375rem;
		min-width: 0;
		flex: 1;
	}

	.nameplate-header-right {
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

	.nameplate-body {
		padding: 0.5rem 0.75rem;
		border-top: 1px solid #374151;
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
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
