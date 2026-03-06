<script lang="ts">
	/**
	 * SpriteUploader.svelte
	 * Handles a single sprite slot: drag-and-drop or file input upload,
	 * thumbnail preview, file name display, and clear/replace controls.
	 */
	import type { SpriteSlot } from '$lib/types';
	import { SPRITE_SLOT_LABELS } from '$lib/types';
	import { createPreviewUrl } from '$lib/utils/bundle';

	interface Props {
		slot: SpriteSlot;
		previewUrl?: string | undefined;
		fileName?: string | undefined;
		fileSize?: number | undefined;
		onupload: (slot: SpriteSlot, bytes: Uint8Array, fileName: string) => void;
		onclear: (slot: SpriteSlot) => void;
	}

	let { slot, previewUrl = undefined, fileName = undefined, fileSize = undefined, onupload, onclear }: Props = $props();

	let isDragOver = $state(false);
	let inputEl: HTMLInputElement;

	const label = SPRITE_SLOT_LABELS[slot];

	function formatBytes(bytes: number): string {
		if (bytes < 1024) return `${bytes} B`;
		if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
		return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
	}

	async function handleFile(file: File) {
		if (!file.type.startsWith('image/') && !file.name.endsWith('.png')) {
			alert(`Only PNG images are supported for sprite slots.\nGot: ${file.type || 'unknown type'}`);
			return;
		}
		const buffer = await file.arrayBuffer();
		const bytes = new Uint8Array(buffer);
		onupload(slot, bytes, file.name);
	}

	function handleInputChange(e: Event) {
		const files = (e.target as HTMLInputElement).files;
		if (files && files[0]) handleFile(files[0]);
		// Reset input so same file can be re-uploaded
		(e.target as HTMLInputElement).value = '';
	}

	function handleDrop(e: DragEvent) {
		e.preventDefault();
		isDragOver = false;
		const file = e.dataTransfer?.files[0];
		if (file) handleFile(file);
	}

	function handleDragOver(e: DragEvent) {
		e.preventDefault();
		isDragOver = true;
	}

	function handleDragLeave() {
		isDragOver = false;
	}
</script>

<div class="sprite-slot">
	<div class="slot-header">
		<span class="slot-label">{label}</span>
		{#if fileName}
			<span class="file-info">{fileName}{fileSize !== undefined ? ` (${formatBytes(fileSize)})` : ''}</span>
		{/if}
	</div>

	<!-- Drop Zone / Preview Area -->
	<!-- svelte-ignore a11y_interactive_supports_focus -->
	<div
		class="drop-zone"
		class:has-image={!!previewUrl}
		class:drag-over={isDragOver}
		role="button"
		aria-label={previewUrl ? `Replace ${label} sprite` : `Upload ${label} sprite`}
		ondrop={handleDrop}
		ondragover={handleDragOver}
		ondragleave={handleDragLeave}
		onclick={() => inputEl.click()}
		onkeydown={(e) => e.key === 'Enter' && inputEl.click()}
	>
		{#if previewUrl}
			<img src={previewUrl} alt="{label} sprite preview" class="preview-img" />
			<div class="overlay">
				<span>Click or drop to replace</span>
			</div>
		{:else}
			<div class="empty-zone">
				<svg
					xmlns="http://www.w3.org/2000/svg"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="1.5"
					class="upload-icon"
					aria-hidden="true"
				>
					<path
						stroke-linecap="round"
						stroke-linejoin="round"
						d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5m-13.5-9L12 3m0 0 4.5 4.5M12 3v13.5"
					/>
				</svg>
				<span class="empty-label">{label}</span>
				<span class="empty-hint">Click or drop PNG</span>
			</div>
		{/if}
	</div>

	<!-- Hidden file input -->
	<input
		bind:this={inputEl}
		type="file"
		accept="image/png,.png"
		class="sr-only"
		onchange={handleInputChange}
		aria-label="Upload {label} sprite"
	/>

	<!-- Clear button (only shown when image is loaded) -->
	{#if previewUrl}
		<button
			type="button"
			class="clear-btn"
			onclick={() => onclear(slot)}
			aria-label="Clear {label} sprite"
		>
			✕ Clear
		</button>
	{/if}
</div>

<style>
	.sprite-slot {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
	}

	.slot-header {
		display: flex;
		align-items: baseline;
		gap: 0.5rem;
		min-height: 1.25rem;
	}

	.slot-label {
		font-size: 0.75rem;
		font-weight: 600;
		color: #a78bfa;
		text-transform: uppercase;
		letter-spacing: 0.05em;
		white-space: nowrap;
	}

	.file-info {
		font-size: 0.65rem;
		color: #9ca3af;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
		max-width: 120px;
	}

	.drop-zone {
		position: relative;
		width: 80px;
		height: 80px;
		border: 2px dashed #4b5563;
		border-radius: 0.5rem;
		cursor: pointer;
		overflow: hidden;
		transition:
			border-color 0.15s,
			background-color 0.15s;
		background-color: #1f2937;
	}

	.drop-zone:hover,
	.drop-zone.drag-over {
		border-color: #7c3aed;
		background-color: #1e1b4b;
	}

	.drop-zone.has-image {
		border-style: solid;
		border-color: #374151;
	}

	.preview-img {
		width: 100%;
		height: 100%;
		object-fit: contain;
		image-rendering: pixelated;
		display: block;
	}

	.overlay {
		position: absolute;
		inset: 0;
		display: flex;
		align-items: center;
		justify-content: center;
		background-color: rgba(0, 0, 0, 0.6);
		opacity: 0;
		transition: opacity 0.15s;
		font-size: 0.6rem;
		color: #e5e7eb;
		text-align: center;
		padding: 0.25rem;
	}

	.drop-zone:hover .overlay {
		opacity: 1;
	}

	.empty-zone {
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		height: 100%;
		gap: 0.1rem;
		color: #6b7280;
	}

	.upload-icon {
		width: 20px;
		height: 20px;
	}

	.empty-label {
		font-size: 0.55rem;
		font-weight: 600;
		text-align: center;
		line-height: 1.2;
	}

	.empty-hint {
		font-size: 0.5rem;
		text-align: center;
		color: #4b5563;
	}

	.sr-only {
		position: absolute;
		width: 1px;
		height: 1px;
		padding: 0;
		margin: -1px;
		overflow: hidden;
		clip: rect(0, 0, 0, 0);
		white-space: nowrap;
		border-width: 0;
	}

	.clear-btn {
		font-size: 0.65rem;
		color: #f87171;
		background: none;
		border: none;
		padding: 0;
		cursor: pointer;
		text-align: left;
		width: fit-content;
	}

	.clear-btn:hover {
		color: #fca5a5;
		text-decoration: underline;
	}
</style>
