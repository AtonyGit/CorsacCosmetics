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
	import type { HatEntry, SpriteSlot, VisorEntry, VisorSpriteSlot, NameplateEntry, NameplateSpriteSlot } from '$lib/types';
	import { createHatEntry, createVisorEntry, createNameplateEntry } from '$lib/types';
	import { assembleBundle, parseBundle, triggerDownload, createPreviewUrl, MAX_BUNDLE_SIZE_BYTES } from '$lib/utils/bundle';
	import HatEditor from '$lib/components/HatEditor.svelte';
	import VisorEditor from '$lib/components/VisorEditor.svelte';
	import NameplateEditor from '$lib/components/NameplateEditor.svelte';
	import ManifestPreview from '$lib/components/ManifestPreview.svelte';
	import { SvelteMap } from 'svelte/reactivity';

	// ---------------------------------------------------------------------------
	// State
	// ---------------------------------------------------------------------------

	let hats = $state<HatEntry[]>([]);
	let visors = $state<VisorEntry[]>([]);
	let nameplates = $state<NameplateEntry[]>([]);
	let statusMessage = $state<string>('');
	let statusType = $state<'info' | 'success' | 'error' | 'warning'>('info');
	let lastManifestLength = $state<number | null>(null);
	let lastDataLength = $state<number | null>(null);
	let isProcessing = $state(false);

	let openFileInput: HTMLInputElement;
	let bundleFileName = $state('bundle.ccb');
	let activeTab = $state<'hats' | 'visors' | 'nameplates'>('hats');

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
		for (const visor of visors) {
			for (const url of Object.values(visor.previewUrls)) {
				if (url) URL.revokeObjectURL(url);
			}
		}
		for (const nameplate of nameplates) {
			for (const url of Object.values(nameplate.previewUrls)) {
				if (url) URL.revokeObjectURL(url);
			}
		}
		hats = [];
		visors = [];
		nameplates = [];
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
	// Visor list management
	// ---------------------------------------------------------------------------

	function addVisor() {
		visors = [...visors, createVisorEntry()];
		setStatus(`Added visor #${visors.length}.`, 'info');
	}

	function updateVisor(id: string, updated: VisorEntry) {
		visors = visors.map((v) => (v.id === id ? updated : v));
	}

	function deleteVisor(id: string) {
		const visor = visors.find((v) => v.id === id);
		if (visor) {
			for (const url of Object.values(visor.previewUrls)) {
				if (url) URL.revokeObjectURL(url);
			}
		}
		visors = visors.filter((v) => v.id !== id);
		setStatus('Visor removed.', 'info');
	}

	function duplicateVisor(id: string) {
		const idx = visors.findIndex((v) => v.id === id);
		if (idx === -1) return;
		const src = visors[idx];

		const newPreviewUrls: Partial<Record<VisorSpriteSlot, string>> = {};
		for (const [slot, bytes] of Object.entries(src.imageBytes) as [VisorSpriteSlot, Uint8Array][]) {
			newPreviewUrls[slot] = createPreviewUrl(bytes);
		}

		const copy: VisorEntry = {
			...createVisorEntry({
				...src.manifest,
				Name: src.manifest.Name + ' (Copy)',
			}),
			imageBytes: { ...src.imageBytes },
			previewUrls: newPreviewUrls,
			fileNames: { ...src.fileNames },
		};

		visors = [...visors.slice(0, idx + 1), copy, ...visors.slice(idx + 1)];
		setStatus(`Duplicated "${src.manifest.Name}".`, 'info');
	}

	function moveVisorUp(id: string) {
		const idx = visors.findIndex((v) => v.id === id);
		if (idx <= 0) return;
		const arr = [...visors];
		[arr[idx - 1], arr[idx]] = [arr[idx], arr[idx - 1]];
		visors = arr;
	}

	function moveVisorDown(id: string) {
		const idx = visors.findIndex((v) => v.id === id);
		if (idx === -1 || idx >= visors.length - 1) return;
		const arr = [...visors];
		[arr[idx], arr[idx + 1]] = [arr[idx + 1], arr[idx]];
		visors = arr;
	}

	// ---------------------------------------------------------------------------
	// Nameplate list management
	// ---------------------------------------------------------------------------

	function addNameplate() {
		nameplates = [...nameplates, createNameplateEntry()];
		setStatus(`Added nameplate #${nameplates.length}.`, 'info');
	}

	function updateNameplate(id: string, updated: NameplateEntry) {
		nameplates = nameplates.map((n) => (n.id === id ? updated : n));
	}

	function deleteNameplate(id: string) {
		const nameplate = nameplates.find((n) => n.id === id);
		if (nameplate) {
			for (const url of Object.values(nameplate.previewUrls)) {
				if (url) URL.revokeObjectURL(url);
			}
		}
		nameplates = nameplates.filter((n) => n.id !== id);
		setStatus('Nameplate removed.', 'info');
	}

	function duplicateNameplate(id: string) {
		const idx = nameplates.findIndex((n) => n.id === id);
		if (idx === -1) return;
		const src = nameplates[idx];

		const newPreviewUrls: Partial<Record<NameplateSpriteSlot, string>> = {};
		for (const [slot, bytes] of Object.entries(src.imageBytes) as [NameplateSpriteSlot, Uint8Array][]) {
			newPreviewUrls[slot] = createPreviewUrl(bytes);
		}

		const copy: NameplateEntry = {
			...createNameplateEntry({
				...src.manifest,
				Name: src.manifest.Name + ' (Copy)',
			}),
			imageBytes: { ...src.imageBytes },
			previewUrls: newPreviewUrls,
			fileNames: { ...src.fileNames },
		};

		nameplates = [...nameplates.slice(0, idx + 1), copy, ...nameplates.slice(idx + 1)];
		setStatus(`Duplicated "${src.manifest.Name}".`, 'info');
	}

	function moveNameplateUp(id: string) {
		const idx = nameplates.findIndex((n) => n.id === id);
		if (idx <= 0) return;
		const arr = [...nameplates];
		[arr[idx - 1], arr[idx]] = [arr[idx], arr[idx - 1]];
		nameplates = arr;
	}

	function moveNameplateDown(id: string) {
		const idx = nameplates.findIndex((n) => n.id === id);
		if (idx === -1 || idx >= nameplates.length - 1) return;
		const arr = [...nameplates];
		[arr[idx], arr[idx + 1]] = [arr[idx + 1], arr[idx]];
		nameplates = arr;
	}

	function validate(): string[] {
		const warnings: string[] = [];
		if (hats.length === 0 && visors.length === 0) warnings.push('Bundle has no hats or visors.');
		const hatNameCounts = new SvelteMap<string, number>();
		for (const hat of hats) {
			const name = hat.manifest.Name.trim();
			if (!name) warnings.push('One or more hats have empty names.');
			hatNameCounts.set(name, (hatNameCounts.get(name) ?? 0) + 1);
		}
		for (const [name, count] of hatNameCounts) {
			if (count > 1 && name) {
				warnings.push(`Duplicate hat name: "${name}" appears ${count} times.`);
			}
		}
		const visorNameCounts = new SvelteMap<string, number>();
		for (const visor of visors) {
			const name = visor.manifest.Name.trim();
			if (!name) warnings.push('One or more visors have empty names.');
			visorNameCounts.set(name, (visorNameCounts.get(name) ?? 0) + 1);
		}
		for (const [name, count] of visorNameCounts) {
			if (count > 1 && name) {
				warnings.push(`Duplicate visor name: "${name}" appears ${count} times.`);
			}
		}
		const nameplateNameCounts = new SvelteMap<string, number>();
		for (const nameplate of nameplates) {
			const name = nameplate.manifest.Name.trim();
			if (!name) warnings.push('One or more nameplates have empty names.');
			nameplateNameCounts.set(name, (nameplateNameCounts.get(name) ?? 0) + 1);
		}
		for (const [name, count] of nameplateNameCounts) {
			if (count > 1 && name) {
				warnings.push(`Duplicate nameplate name: "${name}" appears ${count} times.`);
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

			const result = assembleBundle(hats, visors, nameplates);

			lastManifestLength = result.manifestLength;
			lastDataLength = result.dataLength;

			// Surface any assembly warnings (in addition to validation warnings)
			const allWarnings = [...validate(), ...result.warnings];
			if (allWarnings.length > 0 && statusType !== 'error') {
				setStatus('Downloaded with warnings: ' + allWarnings.join(' | '), 'warning');
			} else {
				setStatus(
					`Bundle downloaded! ${hats.length} hat(s), ${visors.length} visor(s), ${nameplates.length} nameplate(s). ManifestLength=${result.manifestLength} bytes, DataLength=${result.dataLength} bytes.`,
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
			for (const visor of visors) {
				for (const url of Object.values(visor.previewUrls)) {
					if (url) URL.revokeObjectURL(url);
				}
			}

			// Revoke existing nameplate URLs
			for (const nameplate of nameplates) {
				for (const url of Object.values(nameplate.previewUrls)) {
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

			// Reconstruct VisorEntry list from parsed data
			visors = parsed.visors.map(({ manifest, imageBytes }) => {
				const previewUrls: Partial<Record<VisorSpriteSlot, string>> = {};
				const fileNames: Partial<Record<VisorSpriteSlot, string>> = {};

				for (const [slot, bytes] of Object.entries(imageBytes) as [VisorSpriteSlot, Uint8Array][]) {
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

			// Reconstruct NameplateEntry list from parsed data
			nameplates = parsed.nameplates.map(({ manifest, imageBytes }) => {
				const previewUrls: Partial<Record<NameplateSpriteSlot, string>> = {};
				const fileNames: Partial<Record<NameplateSpriteSlot, string>> = {};

				for (const [slot, bytes] of Object.entries(imageBytes) as [NameplateSpriteSlot, Uint8Array][]) {
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
					`Opened "${file.name}" with ${parsed.hats.length} hat(s), ${parsed.visors.length} visor(s), ${parsed.nameplates.length} nameplate(s). Warnings: ${warnings.join(' | ')}`,
					'warning'
				);
			} else {
				setStatus(`Opened "${file.name}" — ${parsed.hats.length} hat(s), ${parsed.visors.length} visor(s), ${parsed.nameplates.length} nameplate(s) loaded successfully.`, 'success');
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
				disabled={isProcessing || (hats.length === 0 && visors.length === 0 && nameplates.length === 0)}
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
			<!-- Tab bar -->
			<div class="tab-bar">
				<button
					type="button"
					class="tab-btn"
					class:active={activeTab === 'hats'}
					onclick={() => (activeTab = 'hats')}
				>
					🎩 Hats <span class="tab-count">{hats.length}</span>
				</button>
				<button
					type="button"
					class="tab-btn"
					class:active={activeTab === 'visors'}
					onclick={() => (activeTab = 'visors')}
				>
					🥽 Visors <span class="tab-count">{visors.length}</span>
				</button>
				<button
					type="button"
					class="tab-btn"
					class:active={activeTab === 'nameplates'}
					onclick={() => (activeTab = 'nameplates')}
				>
					🪧 Nameplates <span class="tab-count">{nameplates.length}</span>
				</button>
				<div class="tab-spacer"></div>
				{#if activeTab === 'hats'}
					<button type="button" class="btn btn-add" onclick={addHat}>+ Add Hat</button>
				{:else if activeTab === 'visors'}
					<button type="button" class="btn btn-add-visor" onclick={addVisor}>+ Add Visor</button>
				{:else}
					<button type="button" class="btn btn-add-nameplate" onclick={addNameplate}>+ Add Nameplate</button>
				{/if}
			</div>

			<!-- Hats panel -->
			{#if activeTab === 'hats'}
				{#if hats.length === 0}
					<div class="empty-state">
						<div class="empty-icon" aria-hidden="true">🎩</div>
						<p class="empty-title">No hats yet</p>
						<p class="empty-sub">
							Click <strong>+ Add Hat</strong> to start building your bundle, or open an
							existing <code>.ccb</code> file.
						</p>
						<button type="button" class="btn btn-primary" onclick={addHat}>+ Add Hat</button>
					</div>
				{:else}
					<div class="item-list">
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
					<button type="button" class="btn btn-add-bottom" onclick={addHat}>
						+ Add Another Hat
					</button>
				{/if}
			{/if}

			<!-- Visors panel -->
			{#if activeTab === 'visors'}
				{#if visors.length === 0}
					<div class="empty-state">
						<div class="empty-icon" aria-hidden="true">🥽</div>
						<p class="empty-title">No visors yet</p>
						<p class="empty-sub">
							Click <strong>+ Add Visor</strong> to include visors in your bundle.
						</p>
						<button type="button" class="btn btn-primary" onclick={addVisor}>+ Add Visor</button>
					</div>
				{:else}
					<div class="item-list">
						{#each visors as visor, index (visor.id)}
							<VisorEditor
								{visor}
								{index}
								total={visors.length}
								onupdate={updateVisor}
								ondelete={deleteVisor}
								onduplicate={duplicateVisor}
								onmoveup={moveVisorUp}
								onmovedown={moveVisorDown}
							/>
						{/each}
					</div>
					<button type="button" class="btn btn-add-visor-bottom" onclick={addVisor}>
						+ Add Another Visor
					</button>
				{/if}
			{/if}

			<!-- Nameplates panel -->
			{#if activeTab === 'nameplates'}
				{#if nameplates.length === 0}
					<div class="empty-state">
						<div class="empty-icon" aria-hidden="true">🪧</div>
						<p class="empty-title">No nameplates yet</p>
						<p class="empty-sub">
							Click <strong>+ Add Nameplate</strong> to include nameplates in your bundle.
						</p>
						<button type="button" class="btn btn-primary" onclick={addNameplate}>+ Add Nameplate</button>
					</div>
				{:else}
					<div class="item-list">
						{#each nameplates as nameplate, index (nameplate.id)}
							<NameplateEditor
								{nameplate}
								{index}
								total={nameplates.length}
								onupdate={updateNameplate}
								ondelete={deleteNameplate}
								onduplicate={duplicateNameplate}
								onmoveup={moveNameplateUp}
								onmovedown={moveNameplateDown}
							/>
						{/each}
					</div>
					<button type="button" class="btn btn-add-nameplate-bottom" onclick={addNameplate}>
						+ Add Another Nameplate
					</button>
				{/if}
			{/if}
		</div>

		<!-- Sidebar: stats + manifest preview -->
		<aside class="sidebar">
			<ManifestPreview {hats} {visors} {nameplates} {lastManifestLength} {lastDataLength} />

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
		gap: 0.75rem;
	}

	/* ── Tab bar ─────────────────────────────────────────────────────────────── */
	.tab-bar {
		display: flex;
		align-items: center;
		gap: 0.25rem;
		border-bottom: 1px solid #1e293b;
		padding-bottom: 0;
	}

	.tab-btn {
		background: none;
		border: none;
		border-bottom: 2px solid transparent;
		margin-bottom: -1px;
		padding: 0.45rem 0.875rem;
		font-size: 0.8rem;
		font-weight: 600;
		color: #6b7280;
		cursor: pointer;
		display: flex;
		align-items: center;
		gap: 0.375rem;
		transition: color 0.15s, border-color 0.15s;
		white-space: nowrap;
	}

	.tab-btn:hover {
		color: #d1d5db;
	}

	.tab-btn.active {
		color: #f9fafb;
		border-bottom-color: #7c3aed;
	}

	.tab-count {
		font-size: 0.65rem;
		background-color: #374151;
		color: #9ca3af;
		padding: 0.05rem 0.4rem;
		border-radius: 9999px;
		font-weight: 700;
	}

	.tab-btn.active .tab-count {
		background-color: #4c1d95;
		color: #c4b5fd;
	}

	.tab-spacer {
		flex: 1;
	}

	.item-list {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
		gap: 0.5rem;
		align-items: start;
	}

	.btn-add-visor {
		background-color: #134e4a;
		color: #5eead4;
		border: 1px solid #0f766e;
	}

	.btn-add-visor:hover {
		background-color: #0f766e;
	}

	.btn-add-visor-bottom {
		background-color: transparent;
		color: #5eead4;
		border: 1px dashed #0f766e;
		width: 100%;
		padding: 0.6rem;
		text-align: center;
		border-radius: 0.5rem;
	}

	.btn-add-visor-bottom:hover {
		background-color: #134e4a22;
	}

	.btn-add-nameplate {
		background-color: #3b1d6e;
		color: #c4b5fd;
		border: 1px solid #5b21b6;
	}

	.btn-add-nameplate:hover {
		background-color: #4c1d95;
	}

	.btn-add-nameplate-bottom {
		background-color: transparent;
		color: #c4b5fd;
		border: 1px dashed #5b21b6;
		width: 100%;
		padding: 0.6rem;
		text-align: center;
		border-radius: 0.5rem;
	}

	.btn-add-nameplate-bottom:hover {
		background-color: #3b1d6e22;
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
