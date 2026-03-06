<script lang="ts">
	/**
	 * +page.svelte — Corsac Cosmetics Bundle Editor
	 *
	 * Main editor page. Handles:
	 *  - New bundle creation
	 *  - Opening an existing .ccb file (parse header + manifest + reconstruct image previews)
	 *  - Downloading a .ccb file (assemble header + manifest + data, trigger browser download)
	 *  - Hat list management: add, delete, duplicate, reorder
	 *  - Validation warnings (empty names, duplicates, large bundles)
	 */
	import type { HatEntry, SpriteSlot } from '$lib/types';
	import { createHatEntry } from '$lib/types';
	import { assembleBundle, parseBundle, triggerDownload, createPreviewUrl, MAX_BUNDLE_SIZE_BYTES } from '$lib/utils/bundle';
	import HatEditor from '$lib/components/HatEditor.svelte';
	import ManifestPreview from '$lib/components/ManifestPreview.svelte';

	// ---------------------------------------------------------------------------
	// State
	// ---------------------------------------------------------------------------

	let hats = $state<HatEntry[]>([]);
	let statusMessage = $state<string>('');
	let statusType = $state<'info' | 'success' | 'error' | 'warning'>('info');
	let lastManifestLength = $state<number | null>(null);
	let lastDataLength = $state<number | null>(null);
	let isProcessing = $state(false);

	let openFileInput: HTMLInputElement;
	let bundleFileName = $state('bundle.ccb');

	// ---------------------------------------------------------------------------
	// Status helpers
	// ---------------------------------------------------------------------------

	function setStatus(msg: string, type: typeof statusType = 'info') {
		statusMessage = msg;
		statusType = type;
	}

	function clearStatus() {
		statusMessage = '';
	}

	// ---------------------------------------------------------------------------
	// Hat list management
	// ---------------------------------------------------------------------------

	function newBundle() {
		// Revoke all existing object URLs
		for (const hat of hats) {
			for (const url of Object.values(hat.previewUrls)) {
				if (url) URL.revokeObjectURL(url);
			}
		}
		hats = [];
		lastManifestLength = null;
		lastDataLength = null;
		bundleFileName = 'bundle.ccb';
		setStatus('New bundle created.', 'info');
	}

	function addHat() {
		hats = [...hats, createHatEntry()];
		setStatus(`Added hat #${hats.length}.`, 'info');
	}

	function updateHat(id: string, updated: HatEntry) {
		hats = hats.map((h) => (h.id === id ? updated : h));
	}

	function deleteHat(id: string) {
		const hat = hats.find((h) => h.id === id);
		if (hat) {
			for (const url of Object.values(hat.previewUrls)) {
				if (url) URL.revokeObjectURL(url);
			}
		}
		hats = hats.filter((h) => h.id !== id);
		setStatus('Hat removed.', 'info');
	}

	function duplicateHat(id: string) {
		const idx = hats.findIndex((h) => h.id === id);
		if (idx === -1) return;
		const src = hats[idx];

		// Create new preview URLs for each slot (can't share object URLs safely)
		const newPreviewUrls: Partial<Record<SpriteSlot, string>> = {};
		for (const [slot, bytes] of Object.entries(src.imageBytes) as [SpriteSlot, Uint8Array][]) {
			newPreviewUrls[slot] = createPreviewUrl(bytes);
		}

		const copy: HatEntry = {
			...createHatEntry({
				...src.manifest,
				Name: src.manifest.Name + ' (Copy)',
			}),
			imageBytes: { ...src.imageBytes },
			previewUrls: newPreviewUrls,
			fileNames: { ...src.fileNames },
		};

		hats = [...hats.slice(0, idx + 1), copy, ...hats.slice(idx + 1)];
		setStatus(`Duplicated "${src.manifest.Name}".`, 'info');
	}

	function moveHatUp(id: string) {
		const idx = hats.findIndex((h) => h.id === id);
		if (idx <= 0) return;
		const arr = [...hats];
		[arr[idx - 1], arr[idx]] = [arr[idx], arr[idx - 1]];
		hats = arr;
	}

	function moveHatDown(id: string) {
		const idx = hats.findIndex((h) => h.id === id);
		if (idx === -1 || idx >= hats.length - 1) return;
		const arr = [...hats];
		[arr[idx], arr[idx + 1]] = [arr[idx + 1], arr[idx]];
		hats = arr;
	}

	// ---------------------------------------------------------------------------
	// Validation
	// ---------------------------------------------------------------------------

	function validate(): string[] {
		const warnings: string[] = [];
		if (hats.length === 0) warnings.push('Bundle has no hats.');
		const nameCounts = new Map<string, number>();
		for (const hat of hats) {
			const name = hat.manifest.Name.trim();
			if (!name) warnings.push('One or more hats have empty names.');
			nameCounts.set(name, (nameCounts.get(name) ?? 0) + 1);
		}
		for (const [name, count] of nameCounts) {
			if (count > 1 && name) {
				warnings.push(`Duplicate hat name: "${name}" appears ${count} times.`);
			}
		}
		return warnings;
	}

	// ---------------------------------------------------------------------------
	// Download .ccb
	// ---------------------------------------------------------------------------

	async function downloadBundle() {
		if (isProcessing) return;
		isProcessing = true;
		clearStatus();

		try {
			const validationWarnings = validate();
			if (validationWarnings.length > 0) {
				setStatus('Warnings: ' + validationWarnings.join(' | '), 'warning');
				// Don't block download, just warn
			}

			const result = assembleBundle(hats);

			lastManifestLength = result.manifestLength;
			lastDataLength = result.dataLength;

			// Surface any assembly warnings (in addition to validation warnings)
			const allWarnings = [...validate(), ...result.warnings];
			if (allWarnings.length > 0 && statusType !== 'error') {
				setStatus('Downloaded with warnings: ' + allWarnings.join(' | '), 'warning');
			} else {
				setStatus(
					`Bundle downloaded! ManifestLength=${result.manifestLength} bytes, DataLength=${result.dataLength} bytes.`,
					'success'
				);
			}

			triggerDownload(result.blob, bundleFileName);
		} catch (err) {
			setStatus('Download failed: ' + (err instanceof Error ? err.message : String(err)), 'error');
		} finally {
			isProcessing = false;
		}
	}

	// ---------------------------------------------------------------------------
	// Open .ccb
	// ---------------------------------------------------------------------------

	function openBundle() {
		openFileInput.click();
	}

	async function handleOpenFile(e: Event) {
		const file = (e.target as HTMLInputElement).files?.[0];
		if (!file) return;
		(e.target as HTMLInputElement).value = '';

		isProcessing = true;
		clearStatus();

		try {
			// Warn for very large files
			if (file.size > MAX_BUNDLE_SIZE_BYTES) {
				setStatus(
					`Warning: file is ${(file.size / 1024 / 1024).toFixed(1)} MB (> 50 MB). Loading…`,
					'warning'
				);
			}

			const buffer = await file.arrayBuffer();
			const parsed = parseBundle(buffer);

			// Revoke existing URLs
			for (const hat of hats) {
				for (const url of Object.values(hat.previewUrls)) {
					if (url) URL.revokeObjectURL(url);
				}
			}

			// Reconstruct HatEntry list from parsed data
			hats = parsed.hats.map(({ manifest, imageBytes }) => {
				const previewUrls: Partial<Record<SpriteSlot, string>> = {};
				const fileNames: Partial<Record<SpriteSlot, string>> = {};

				for (const [slot, bytes] of Object.entries(imageBytes) as [SpriteSlot, Uint8Array][]) {
					previewUrls[slot] = createPreviewUrl(bytes);
					fileNames[slot] = `${slot}.png`;
				}

				return {
					id: crypto.randomUUID(),
					manifest,
					imageBytes,
					previewUrls,
					fileNames,
				};
			});

			bundleFileName = file.name;
			lastManifestLength = null;
			lastDataLength = null;

			const warnings = parsed.warnings;
			if (warnings.length > 0) {
				setStatus(
					`Opened "${file.name}" with ${parsed.hats.length} hat(s). Warnings: ${warnings.join(' | ')}`,
					'warning'
				);
			} else {
				setStatus(`Opened "${file.name}" — ${parsed.hats.length} hat(s) loaded successfully.`, 'success');
			}
		} catch (err) {
			setStatus('Failed to open file: ' + (err instanceof Error ? err.message : String(err)), 'error');
		} finally {
			isProcessing = false;
		}
	}
</script>

<svelte:head>
	<title>Corsac Cosmetics Bundle Editor</title>
</svelte:head>

<div class="page">
	<!-- Toolbar -->
	<header class="toolbar">
		<div class="toolbar-brand">
			<span class="brand-icon" aria-hidden="true">🎩</span>
			<h1 class="brand-title">Corsac Cosmetics Bundle Editor</h1>
			<span class="brand-badge">.ccb</span>
		</div>

		<nav class="toolbar-actions" aria-label="Bundle actions">
			<button type="button" class="btn btn-secondary" onclick={newBundle} disabled={isProcessing}>
				📄 New Bundle
			</button>
			<button type="button" class="btn btn-secondary" onclick={openBundle} disabled={isProcessing}>
				📂 Open .ccb
			</button>
			<div class="filename-wrapper">
				<input
					type="text"
					class="filename-input"
					bind:value={bundleFileName}
					placeholder="bundle.ccb"
					aria-label="Output filename"
				/>
			</div>
			<button
				type="button"
				class="btn btn-primary"
				onclick={downloadBundle}
				disabled={isProcessing || hats.length === 0}
			>
				⬇ Download .ccb
			</button>
		</nav>

		<!-- Hidden file inputs -->
		<input
			bind:this={openFileInput}
			type="file"
			accept=".ccb"
			class="sr-only"
			onchange={handleOpenFile}
			aria-label="Open .ccb file"
		/>
	</header>

	<!-- Status bar -->
	{#if statusMessage}
		<div class="status-bar status-{statusType}" role="status" aria-live="polite">
			<span class="status-icon">
				{#if statusType === 'success'}✓{:else if statusType === 'error'}✗{:else if statusType === 'warning'}⚠{:else}ℹ{/if}
			</span>
			<span class="status-text">{statusMessage}</span>
			<button
				type="button"
				class="status-close"
				onclick={clearStatus}
				aria-label="Dismiss status message"
			>
				×
			</button>
		</div>
	{/if}

	<!-- Main content area -->
	<main class="main-content">
		<div class="editor-column">
			<!-- Hat list header -->
			<div class="section-bar">
				<h2 class="section-heading">
					Hats
					<span class="hat-count">{hats.length}</span>
				</h2>
				<button type="button" class="btn btn-add" onclick={addHat}>
					+ Add Hat
				</button>
			</div>

			<!-- Empty state -->
			{#if hats.length === 0}
				<div class="empty-state">
					<div class="empty-icon" aria-hidden="true">🎩</div>
					<p class="empty-title">No hats yet</p>
					<p class="empty-sub">
						Click <strong>+ Add Hat</strong> to start building your bundle, or open an existing
						<code>.ccb</code> file.
					</p>
					<button type="button" class="btn btn-primary" onclick={addHat}>
						+ Add Hat
					</button>
				</div>
			{:else}
				<div class="hat-list">
					{#each hats as hat, index (hat.id)}
						<HatEditor
							{hat}
							{index}
							total={hats.length}
							onupdate={updateHat}
							ondelete={deleteHat}
							onduplicate={duplicateHat}
							onmoveup={moveHatUp}
							onmovedown={moveHatDown}
						/>
					{/each}
				</div>

				<!-- Add another hat at the bottom -->
				<button type="button" class="btn btn-add-bottom" onclick={addHat}>
					+ Add Another Hat
				</button>
			{/if}
		</div>

		<!-- Sidebar: stats + manifest preview -->
		<aside class="sidebar">
			<ManifestPreview {hats} {lastManifestLength} {lastDataLength} />

			<!-- Format reference card -->
			<div class="format-card">
				<h3 class="format-title">Bundle Format Reference</h3>
				<table class="format-table">
					<thead>
						<tr>
							<th>Offset</th>
							<th>Field</th>
							<th>Type</th>
							<th>Value</th>
						</tr>
					</thead>
					<tbody>
						<tr><td>0</td><td>Magic</td><td>uint32 LE</td><td><code>0x434F5253</code></td></tr>
						<tr><td>4</td><td>Version</td><td>uint16 LE</td><td><code>1</code></td></tr>
						<tr><td>6</td><td>Flags</td><td>uint16 LE</td><td><code>0</code></td></tr>
						<tr><td>8</td><td>ManifestLength</td><td>uint32 LE</td><td>JSON byte length</td></tr>
						<tr><td>12</td><td>DataLength</td><td>uint32 LE</td><td>Total image bytes</td></tr>
						<tr
							><td>16</td><td>Manifest</td><td>UTF-8 JSON</td><td>BundleManifest</td></tr
						>
						<tr
							><td>16+M</td><td>Data</td><td>raw bytes</td><td>Concatenated PNGs</td></tr
						>
					</tbody>
				</table>
				<p class="format-note">
					Sprite <code>Offset</code> values are relative to the start of the Data section
					(byte <code>16 + ManifestLength</code>).
				</p>
			</div>
		</aside>
	</main>
</div>

<style>
	/* ── Layout ─────────────────────────────────────────────────────────────── */
	.page {
		min-height: 100vh;
		background-color: #0f172a;
		color: #f3f4f6;
		display: flex;
		flex-direction: column;
	}

	/* ── Toolbar ─────────────────────────────────────────────────────────────── */
	.toolbar {
		position: sticky;
		top: 0;
		z-index: 40;
		background-color: #111827;
		border-bottom: 1px solid #1e293b;
		padding: 0.625rem 1.25rem;
		display: flex;
		align-items: center;
		justify-content: space-between;
		flex-wrap: wrap;
		gap: 0.75rem;
	}

	.toolbar-brand {
		display: flex;
		align-items: center;
		gap: 0.5rem;
	}

	.brand-icon {
		font-size: 1.25rem;
	}

	.brand-title {
		font-size: 1rem;
		font-weight: 700;
		color: #f9fafb;
		margin: 0;
		white-space: nowrap;
	}

	.brand-badge {
		font-size: 0.65rem;
		background-color: #4c1d95;
		color: #c4b5fd;
		padding: 0.1rem 0.4rem;
		border-radius: 0.25rem;
		font-weight: 700;
		letter-spacing: 0.05em;
	}

	.toolbar-actions {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		flex-wrap: wrap;
	}

	.filename-wrapper {
		display: flex;
		align-items: center;
	}

	.filename-input {
		background-color: #1f2937;
		border: 1px solid #374151;
		border-radius: 0.375rem;
		color: #f3f4f6;
		font-size: 0.8rem;
		padding: 0.35rem 0.6rem;
		outline: none;
		width: 160px;
		transition: border-color 0.15s;
	}

	.filename-input:focus {
		border-color: #7c3aed;
	}

	/* ── Buttons ─────────────────────────────────────────────────────────────── */
	.btn {
		font-size: 0.8rem;
		font-weight: 600;
		padding: 0.4rem 0.875rem;
		border-radius: 0.375rem;
		cursor: pointer;
		border: none;
		transition:
			background-color 0.15s,
			opacity 0.15s;
		white-space: nowrap;
	}

	.btn:disabled {
		opacity: 0.45;
		cursor: not-allowed;
	}

	.btn-primary {
		background-color: #7c3aed;
		color: #fff;
	}

	.btn-primary:hover:not(:disabled) {
		background-color: #6d28d9;
	}

	.btn-secondary {
		background-color: #1f2937;
		color: #d1d5db;
		border: 1px solid #374151;
	}

	.btn-secondary:hover:not(:disabled) {
		background-color: #374151;
		color: #f3f4f6;
	}

	.btn-add {
		background-color: #064e3b;
		color: #6ee7b7;
		border: 1px solid #065f46;
	}

	.btn-add:hover {
		background-color: #065f46;
	}

	.btn-add-bottom {
		background-color: transparent;
		color: #6ee7b7;
		border: 1px dashed #065f46;
		width: 100%;
		padding: 0.6rem;
		text-align: center;
		border-radius: 0.5rem;
	}

	.btn-add-bottom:hover {
		background-color: #064e3b22;
	}

	/* ── Status bar ─────────────────────────────────────────────────────────── */
	.status-bar {
		display: flex;
		align-items: flex-start;
		gap: 0.5rem;
		padding: 0.6rem 1.25rem;
		font-size: 0.8rem;
		border-bottom: 1px solid transparent;
	}

	.status-info {
		background-color: #0c4a6e;
		color: #bae6fd;
		border-color: #0369a1;
	}

	.status-success {
		background-color: #14532d;
		color: #bbf7d0;
		border-color: #166534;
	}

	.status-error {
		background-color: #7f1d1d;
		color: #fecaca;
		border-color: #b91c1c;
	}

	.status-warning {
		background-color: #78350f;
		color: #fde68a;
		border-color: #92400e;
	}

	.status-icon {
		font-size: 0.9rem;
		flex-shrink: 0;
		margin-top: 0.05rem;
	}

	.status-text {
		flex: 1;
		line-height: 1.4;
	}

	.status-close {
		background: none;
		border: none;
		font-size: 1rem;
		cursor: pointer;
		color: inherit;
		opacity: 0.7;
		padding: 0;
		line-height: 1;
		flex-shrink: 0;
	}

	.status-close:hover {
		opacity: 1;
	}

	/* ── Main layout ─────────────────────────────────────────────────────────── */
	.main-content {
		display: grid;
		grid-template-columns: 1fr 320px;
		gap: 1.25rem;
		padding: 1.25rem;
		flex: 1;
		align-items: start;
	}

	@media (max-width: 900px) {
		.main-content {
			grid-template-columns: 1fr;
		}
	}

	/* ── Editor column ───────────────────────────────────────────────────────── */
	.editor-column {
		display: flex;
		flex-direction: column;
		gap: 0.875rem;
	}

	.section-bar {
		display: flex;
		align-items: center;
		justify-content: space-between;
		padding-bottom: 0.25rem;
	}

	.section-heading {
		font-size: 1rem;
		font-weight: 700;
		color: #e5e7eb;
		margin: 0;
		display: flex;
		align-items: center;
		gap: 0.5rem;
	}

	.hat-count {
		font-size: 0.75rem;
		background-color: #374151;
		color: #9ca3af;
		padding: 0.1rem 0.45rem;
		border-radius: 9999px;
		font-weight: 600;
	}

	.hat-list {
		display: flex;
		flex-direction: column;
		gap: 0.625rem;
	}

	/* ── Empty state ─────────────────────────────────────────────────────────── */
	.empty-state {
		background-color: #111827;
		border: 2px dashed #374151;
		border-radius: 0.75rem;
		padding: 3rem 2rem;
		text-align: center;
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 0.75rem;
	}

	.empty-icon {
		font-size: 3rem;
	}

	.empty-title {
		font-size: 1.125rem;
		font-weight: 700;
		color: #9ca3af;
		margin: 0;
	}

	.empty-sub {
		font-size: 0.85rem;
		color: #6b7280;
		margin: 0;
		max-width: 360px;
		line-height: 1.5;
	}

	.empty-sub code {
		background-color: #1f2937;
		padding: 0 0.25rem;
		border-radius: 0.2rem;
		font-size: 0.8rem;
	}

	/* ── Sidebar ─────────────────────────────────────────────────────────────── */
	.sidebar {
		display: flex;
		flex-direction: column;
		gap: 1rem;
		position: sticky;
		top: 4.5rem;
	}

	/* ── Format reference card ───────────────────────────────────────────────── */
	.format-card {
		background-color: #111827;
		border: 1px solid #1e293b;
		border-radius: 0.625rem;
		padding: 0.875rem;
	}

	.format-title {
		font-size: 0.75rem;
		font-weight: 700;
		color: #9ca3af;
		text-transform: uppercase;
		letter-spacing: 0.08em;
		margin: 0 0 0.625rem;
	}

	.format-table {
		width: 100%;
		border-collapse: collapse;
		font-size: 0.65rem;
	}

	.format-table th {
		text-align: left;
		color: #6b7280;
		font-weight: 600;
		padding: 0.2rem 0.4rem;
		border-bottom: 1px solid #1e293b;
	}

	.format-table td {
		padding: 0.2rem 0.4rem;
		color: #d1d5db;
		border-bottom: 1px solid #0f172a;
		vertical-align: top;
	}

	.format-table code {
		font-family: ui-monospace, monospace;
		background-color: #1e293b;
		padding: 0 0.2rem;
		border-radius: 0.15rem;
		color: #a5f3fc;
		font-size: 0.6rem;
	}

	.format-note {
		font-size: 0.65rem;
		color: #6b7280;
		margin: 0.5rem 0 0;
		line-height: 1.4;
	}

	.format-note code {
		font-family: ui-monospace, monospace;
		background-color: #1e293b;
		padding: 0 0.2rem;
		border-radius: 0.15rem;
		color: #a5f3fc;
	}

	/* ── Shared ──────────────────────────────────────────────────────────────── */
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
</style>
