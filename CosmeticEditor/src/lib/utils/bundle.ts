/**
 * bundle.ts — Binary read/write utilities for the .ccb (Corsac Cosmetics Bundle) format.
 *
 * Binary layout (all fields little-endian):
 *   Offset  0: uint32  Magic          = 0x434F5253 ("CORS" in ASCII)
 *   Offset  4: uint16  Version        = 1
 *   Offset  6: uint16  Flags          = 0 (reserved)
 *   Offset  8: uint32  ManifestLength = byte length of the UTF-8 JSON manifest
 *   Offset 12: uint32  DataLength     = total bytes of all appended image data
 *   --- end of 16-byte header ---
 *   Offset 16: [ManifestLength bytes] UTF-8 JSON matching BundleManifest shape
 *   Offset 16+ManifestLength: [DataLength bytes] raw concatenated PNG bytes
 *
 * Sprite Offset values in the manifest are relative to the start of the data section
 * (i.e., byte position 16 + ManifestLength in the file).
 *
 * This module is pure browser code — no Node/server APIs used.
 */

import type { BundleManifest, HatEntry, HatManifest, VisorEntry, VisorManifest, SpriteData } from '$lib/types';
import { SPRITE_SLOTS, VISOR_SPRITE_SLOTS, createEmptySpriteData } from '$lib/types';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

export const BUNDLE_MAGIC = 0x434f5253; // 'CORS' in ASCII
export const BUNDLE_VERSION = 1;
export const HEADER_SIZE = 16;
export const MAX_BUNDLE_SIZE_BYTES = 50 * 1024 * 1024; // 50 MB warning threshold

// ---------------------------------------------------------------------------
// Header read/write (little-endian DataView operations)
// ---------------------------------------------------------------------------

export interface BundleHeader {
	magic: number;
	version: number;
	flags: number;
	manifestLength: number;
	dataLength: number;
}

/**
 * Write the 16-byte bundle header into a new ArrayBuffer.
 * All fields are written little-endian to match C# BinaryPrimitives.WriteUInt*LittleEndian.
 */
export function writeHeader(manifestLength: number, dataLength: number): ArrayBuffer {
	const buf = new ArrayBuffer(HEADER_SIZE);
	const view = new DataView(buf);
	// Offset  0: uint32 Magic
	view.setUint32(0, BUNDLE_MAGIC, true);
	// Offset  4: uint16 Version
	view.setUint16(4, BUNDLE_VERSION, true);
	// Offset  6: uint16 Flags (reserved = 0)
	view.setUint16(6, 0, true);
	// Offset  8: uint32 ManifestLength
	view.setUint32(8, manifestLength, true);
	// Offset 12: uint32 DataLength
	view.setUint32(12, dataLength, true);
	return buf;
}

/**
 * Read and parse the 16-byte header from an ArrayBuffer.
 * Throws a descriptive error if the buffer is too short.
 */
export function readHeader(buffer: ArrayBuffer): BundleHeader {
	if (buffer.byteLength < HEADER_SIZE) {
		throw new Error(`File too short: expected at least ${HEADER_SIZE} bytes, got ${buffer.byteLength}.`);
	}
	const view = new DataView(buffer, 0, HEADER_SIZE);
	return {
		magic: view.getUint32(0, true),
		version: view.getUint16(4, true),
		flags: view.getUint16(6, true),
		manifestLength: view.getUint32(8, true),
		dataLength: view.getUint32(12, true),
	};
}

/**
 * Validate header magic and version. Returns an error string or null on success.
 */
export function validateHeader(header: BundleHeader): string | null {
	if (header.magic !== BUNDLE_MAGIC) {
		const got = '0x' + header.magic.toString(16).toUpperCase().padStart(8, '0');
		return `Invalid magic number: expected 0x434F5253 ("CORS"), got ${got}. This is not a valid .ccb file.`;
	}
	if (header.version === 0 || header.version > BUNDLE_VERSION) {
		return `Unsupported bundle version: ${header.version}. This editor supports version 1 only.`;
	}
	return null;
}

// ---------------------------------------------------------------------------
// UTF-8 encode / decode
// ---------------------------------------------------------------------------

const encoder = new TextEncoder();
const decoder = new TextDecoder('utf-8');

export function encodeUtf8(str: string): Uint8Array {
	return encoder.encode(str);
}

export function decodeUtf8(bytes: ArrayBuffer | ArrayBufferView): string {
	return decoder.decode(bytes);
}

// ---------------------------------------------------------------------------
// Manifest serialization
// ---------------------------------------------------------------------------

/**
 * Serialize a BundleManifest to a UTF-8 JSON string.
 * Property names stay in PascalCase to match the C# JsonSerializer expectations.
 * Uses JSON.stringify with stable ordering of sprite fields.
 */
export function serializeManifest(manifest: BundleManifest): Uint8Array {
	const json = JSON.stringify(manifest);
	return encodeUtf8(json);
}

/**
 * Deserialize a UTF-8 JSON byte range into a BundleManifest.
 */
export function deserializeManifest(buffer: ArrayBuffer, offset: number, length: number): BundleManifest {
	const slice = buffer.slice(offset, offset + length);
	const json = decodeUtf8(slice);
	return JSON.parse(json) as BundleManifest;
}

// ---------------------------------------------------------------------------
// Bundle assembly (Download .ccb)
// ---------------------------------------------------------------------------

export interface AssembledBundle {
	blob: Blob;
	manifestLength: number;
	dataLength: number;
	warnings: string[];
}

/**
 * Assemble a complete .ccb bundle from lists of HatEntry and VisorEntry objects.
 *
 * Algorithm:
 *  1. Collect all image bytes per hat per sprite slot, then per visor per sprite slot,
 *     in deterministic order (SPRITE_SLOTS for hats, VISOR_SPRITE_SLOTS for visors).
 *  2. Assign each sprite an Offset (relative to start-of-data) and Size.
 *     If no image, Size=0 and Offset=0.
 *  3. Build the BundleManifest JSON.
 *  4. Compute ManifestLength and DataLength.
 *  5. Write 16-byte header.
 *  6. Concatenate: header + manifestBytes + dataBytes → Blob.
 */
export function assembleBundle(hats: HatEntry[], visors: VisorEntry[] = []): AssembledBundle {
	const warnings: string[] = [];

	// --- Step 1 & 2: collect image bytes, compute offsets ---
	const dataParts: Uint8Array[] = [];
	let runningOffset = 0;

	const resolvedHats: HatManifest[] = hats.map((hat) => {
		const m = { ...hat.manifest };

		if (!m.Name || m.Name.trim() === '') {
			warnings.push(`A hat has an empty name — it will default to "Custom Hat".`);
			m.Name = 'Custom Hat';
		}

		for (const slot of SPRITE_SLOTS) {
			const bytes = hat.imageBytes[slot];
			if (bytes && bytes.byteLength > 0) {
				m[slot] = { Size: bytes.byteLength, Offset: runningOffset } as SpriteData;
				dataParts.push(bytes);
				runningOffset += bytes.byteLength;
			} else {
				m[slot] = createEmptySpriteData();
			}
		}
		return m;
	});

	// Check for duplicate hat names
	const hatNameCounts = new Map<string, number>();
	for (const h of resolvedHats) {
		hatNameCounts.set(h.Name, (hatNameCounts.get(h.Name) ?? 0) + 1);
	}
	for (const [name, count] of hatNameCounts) {
		if (count > 1) {
			warnings.push(`Duplicate hat name "${name}" found ${count} times. The C# loader may overwrite duplicates.`);
		}
	}

	const resolvedVisors: VisorManifest[] = visors.map((visor) => {
		const m = { ...visor.manifest };

		if (!m.Name || m.Name.trim() === '') {
			warnings.push(`A visor has an empty name — it will default to "Custom Visor".`);
			m.Name = 'Custom Visor';
		}

		for (const slot of VISOR_SPRITE_SLOTS) {
			const bytes = visor.imageBytes[slot];
			if (bytes && bytes.byteLength > 0) {
				m[slot] = { Size: bytes.byteLength, Offset: runningOffset } as SpriteData;
				dataParts.push(bytes);
				runningOffset += bytes.byteLength;
			} else {
				m[slot] = createEmptySpriteData();
			}
		}
		return m;
	});

	// Check for duplicate visor names
	const visorNameCounts = new Map<string, number>();
	for (const v of resolvedVisors) {
		visorNameCounts.set(v.Name, (visorNameCounts.get(v.Name) ?? 0) + 1);
	}
	for (const [name, count] of visorNameCounts) {
		if (count > 1) {
			warnings.push(`Duplicate visor name "${name}" found ${count} times. The C# loader may overwrite duplicates.`);
		}
	}

	const dataLength = runningOffset;

	// --- Step 3: build manifest ---
	const manifest: BundleManifest = {
		Version: BUNDLE_VERSION,
		Hats: resolvedHats,
		Visors: resolvedVisors,
	};

	// --- Step 4: serialize manifest ---
	const manifestBytes = serializeManifest(manifest);
	const manifestLength = manifestBytes.byteLength;

	// --- Step 5: write header ---
	const headerBuffer = writeHeader(manifestLength, dataLength);

	// --- Step 6: size warning ---
	const totalSize = HEADER_SIZE + manifestLength + dataLength;
	if (totalSize > MAX_BUNDLE_SIZE_BYTES) {
		warnings.push(`Bundle size is ${(totalSize / 1024 / 1024).toFixed(1)} MB, which exceeds the 50 MB warning threshold.`);
	}

	// --- Assemble Blob ---
	const parts: BlobPart[] = [headerBuffer as ArrayBuffer, manifestBytes.buffer as ArrayBuffer];
	for (const part of dataParts) {
		parts.push(part.buffer as ArrayBuffer);
	}
	const blob = new Blob(parts, { type: 'application/octet-stream' });

	return { blob, manifestLength, dataLength, warnings };
}

// ---------------------------------------------------------------------------
// Bundle parsing (Open .ccb)
// ---------------------------------------------------------------------------

export interface ParsedHat {
	manifest: HatManifest;
	/** Per-slot image bytes, sliced from the file buffer */
	imageBytes: Partial<Record<(typeof SPRITE_SLOTS)[number], Uint8Array>>;
}

export interface ParsedVisor {
	manifest: VisorManifest;
	/** Per-slot image bytes, sliced from the file buffer */
	imageBytes: Partial<Record<(typeof VISOR_SPRITE_SLOTS)[number], Uint8Array>>;
}

export interface ParsedBundle {
	manifest: BundleManifest;
	hats: ParsedHat[];
	visors: ParsedVisor[];
	warnings: string[];
}

/**
 * Parse a complete .ccb file from an ArrayBuffer.
 *
 * The data section starts at byte (HEADER_SIZE + manifestLength).
 * Each sprite's Offset in the manifest is relative to that data section start.
 */
export function parseBundle(buffer: ArrayBuffer): ParsedBundle {
	const warnings: string[] = [];

	// --- Read header ---
	const header = readHeader(buffer);
	const headerError = validateHeader(header);
	if (headerError) throw new Error(headerError);

	const minExpectedSize = HEADER_SIZE + header.manifestLength + header.dataLength;
	if (buffer.byteLength < minExpectedSize) {
		throw new Error(
			`File appears truncated: header claims ${minExpectedSize} bytes but file is ${buffer.byteLength} bytes.`
		);
	}

	// --- Parse manifest ---
	const manifest = deserializeManifest(buffer, HEADER_SIZE, header.manifestLength);
	if (!manifest.Hats || !Array.isArray(manifest.Hats)) {
		throw new Error('Manifest is missing the "Hats" array.');
	}
	if (!manifest.Version || manifest.Version < 1 || manifest.Version > BUNDLE_VERSION) {
		warnings.push(`Manifest Version field is ${manifest.Version}; expected 1.`);
	}

	// Data section starts at: HEADER_SIZE + manifestLength
	const dataStart = HEADER_SIZE + header.manifestLength;

	// --- Extract sprite bytes per hat ---
	const hats: ParsedHat[] = manifest.Hats.map((hatManifest) => {
		const imageBytes: Partial<Record<(typeof SPRITE_SLOTS)[number], Uint8Array>> = {};

		for (const slot of SPRITE_SLOTS) {
			const sprite: SpriteData = hatManifest[slot] ?? { Size: 0, Offset: 0 };
			if (sprite.Size > 0) {
				// Slice from: dataStart + sprite.Offset, length: sprite.Size
				const start = dataStart + sprite.Offset;
				const end = start + sprite.Size;
				if (end > buffer.byteLength) {
					warnings.push(
						`Hat "${hatManifest.Name}" sprite ${slot}: data range [${start}, ${end}) exceeds file size ${buffer.byteLength}. Skipping.`
					);
				} else {
					imageBytes[slot] = new Uint8Array(buffer.slice(start, end));
				}
			}
		}

		return { manifest: hatManifest, imageBytes };
	});

	// --- Extract sprite bytes per visor ---
	const visors: ParsedVisor[] = (manifest.Visors ?? []).map((visorManifest) => {
		const imageBytes: Partial<Record<(typeof VISOR_SPRITE_SLOTS)[number], Uint8Array>> = {};

		for (const slot of VISOR_SPRITE_SLOTS) {
			const sprite: SpriteData = visorManifest[slot] ?? { Size: 0, Offset: 0 };
			if (sprite.Size > 0) {
				const start = dataStart + sprite.Offset;
				const end = start + sprite.Size;
				if (end > buffer.byteLength) {
					warnings.push(
						`Visor "${visorManifest.Name}" sprite ${slot}: data range [${start}, ${end}) exceeds file size ${buffer.byteLength}. Skipping.`
					);
				} else {
					imageBytes[slot] = new Uint8Array(buffer.slice(start, end));
				}
			}
		}

		return { manifest: visorManifest, imageBytes };
	});

	return { manifest, hats, visors, warnings };
}

// ---------------------------------------------------------------------------
// File download helper
// ---------------------------------------------------------------------------

/**
 * Trigger a browser download of the given Blob as the specified filename.
 */
export function triggerDownload(blob: Blob, filename: string): void {
	const url = URL.createObjectURL(blob);
	const a = document.createElement('a');
	a.href = url;
	a.download = filename;
	a.style.display = 'none';
	document.body.appendChild(a);
	a.click();
	document.body.removeChild(a);
	// Revoke after a short delay to allow the download to start
	setTimeout(() => URL.revokeObjectURL(url), 5000);
}

// ---------------------------------------------------------------------------
// Preview URL helpers
// ---------------------------------------------------------------------------

/**
 * Create an object URL from raw image bytes. Caller is responsible for revoking it.
 */
export function createPreviewUrl(bytes: Uint8Array): string {
	const blob = new Blob([bytes.buffer as ArrayBuffer], { type: 'image/png' });
	return URL.createObjectURL(blob);
}
