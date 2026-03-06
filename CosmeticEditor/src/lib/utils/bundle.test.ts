/**
 * bundle.test.ts — Unit tests for .ccb bundle read/write utilities.
 *
 * Covers:
 *  1. Header encoding (writeHeader) produces correct little-endian bytes.
 *  2. Header decoding (readHeader) round-trips correctly.
 *  3. validateHeader rejects wrong magic and unsupported versions.
 *  4. Manifest serialize/deserialize round-trip preserves all PascalCase fields.
 *  5. assembleBundle + parseBundle full round-trip: create bundle → re-open → assert equality.
 *  6. parseBundle throws a descriptive error for corrupt magic.
 *  7. parseBundle throws for unsupported version > 1.
 */

import { describe, it, expect } from 'vitest';
import {
	BUNDLE_MAGIC,
	BUNDLE_VERSION,
	HEADER_SIZE,
	writeHeader,
	readHeader,
	validateHeader,
	serializeManifest,
	deserializeManifest,
	assembleBundle,
	parseBundle,
} from './bundle';
import type { BundleManifest } from '../types';
import { createHatEntry, SPRITE_SLOTS } from '../types';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Build a minimal 1×1 white PNG as Uint8Array (valid PNG bytes). */
function makeMinimalPng(): Uint8Array {
	// This is a valid 1×1 white PNG (67 bytes), hardcoded for test purposes.
	return new Uint8Array([
		0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, // PNG signature
		0x00, 0x00, 0x00, 0x0d, // IHDR length
		0x49, 0x48, 0x44, 0x52, // "IHDR"
		0x00, 0x00, 0x00, 0x01, // width = 1
		0x00, 0x00, 0x00, 0x01, // height = 1
		0x08, 0x02,             // 8-bit depth, RGB color type
		0x00, 0x00, 0x00,       // compression, filter, interlace
		0x90, 0x77, 0x53, 0xde, // IHDR CRC
		0x00, 0x00, 0x00, 0x0c, // IDAT length
		0x49, 0x44, 0x41, 0x54, // "IDAT"
		0x08, 0xd7, 0x63, 0xf8, 0xcf, 0xc0, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, // deflate stream
		0xe2, 0x21, 0xbc, 0x33, // IDAT CRC
		0x00, 0x00, 0x00, 0x00, // IEND length
		0x49, 0x45, 0x4e, 0x44, // "IEND"
		0xae, 0x42, 0x60, 0x82, // IEND CRC
	]);
}

// ---------------------------------------------------------------------------
// 1. writeHeader — correct little-endian bytes
// ---------------------------------------------------------------------------

describe('writeHeader', () => {
	it('produces a 16-byte buffer', () => {
		const buf = writeHeader(100, 200);
		expect(buf.byteLength).toBe(HEADER_SIZE);
	});

	it('encodes magic as 0x434F5253 at offset 0 (LE)', () => {
		const buf = writeHeader(0, 0);
		const view = new DataView(buf);
		expect(view.getUint32(0, true)).toBe(BUNDLE_MAGIC);
	});

	it('encodes version 1 at offset 4 (LE)', () => {
		const buf = writeHeader(0, 0);
		const view = new DataView(buf);
		expect(view.getUint16(4, true)).toBe(BUNDLE_VERSION);
	});

	it('encodes flags 0 at offset 6 (LE)', () => {
		const buf = writeHeader(0, 0);
		const view = new DataView(buf);
		expect(view.getUint16(6, true)).toBe(0);
	});

	it('encodes manifestLength at offset 8 (LE)', () => {
		const buf = writeHeader(12345, 0);
		const view = new DataView(buf);
		expect(view.getUint32(8, true)).toBe(12345);
	});

	it('encodes dataLength at offset 12 (LE)', () => {
		const buf = writeHeader(0, 98765);
		const view = new DataView(buf);
		expect(view.getUint32(12, true)).toBe(98765);
	});
});

// ---------------------------------------------------------------------------
// 2. readHeader round-trip
// ---------------------------------------------------------------------------

describe('readHeader', () => {
	it('round-trips magic, version, flags, manifestLength, dataLength', () => {
		const written = writeHeader(42, 1337);
		const parsed = readHeader(written);
		expect(parsed.magic).toBe(BUNDLE_MAGIC);
		expect(parsed.version).toBe(BUNDLE_VERSION);
		expect(parsed.flags).toBe(0);
		expect(parsed.manifestLength).toBe(42);
		expect(parsed.dataLength).toBe(1337);
	});

	it('throws if buffer is too short', () => {
		const tiny = new ArrayBuffer(8);
		expect(() => readHeader(tiny)).toThrow();
	});
});

// ---------------------------------------------------------------------------
// 3. validateHeader
// ---------------------------------------------------------------------------

describe('validateHeader', () => {
	it('returns null for a valid header', () => {
		const buf = writeHeader(0, 0);
		const header = readHeader(buf);
		expect(validateHeader(header)).toBeNull();
	});

	it('returns error string for wrong magic', () => {
		const buf = writeHeader(0, 0);
		const view = new DataView(buf);
		view.setUint32(0, 0xdeadbeef, true); // corrupt magic
		const header = readHeader(buf);
		const err = validateHeader(header);
		expect(err).not.toBeNull();
		expect(err).toContain('0xDEADBEEF');
	});

	it('returns error string for version 0', () => {
		const buf = writeHeader(0, 0);
		const view = new DataView(buf);
		view.setUint16(4, 0, true);
		const header = readHeader(buf);
		const err = validateHeader(header);
		expect(err).not.toBeNull();
		expect(err).toContain('version');
	});

	it('returns error string for version > 1', () => {
		const buf = writeHeader(0, 0);
		const view = new DataView(buf);
		view.setUint16(4, 99, true);
		const header = readHeader(buf);
		const err = validateHeader(header);
		expect(err).not.toBeNull();
	});
});

// ---------------------------------------------------------------------------
// 4. Manifest serialize / deserialize round-trip
// ---------------------------------------------------------------------------

describe('manifest serialization', () => {
	const sampleManifest: BundleManifest = {
		Version: 1,
		Hats: [
			{
				Name: 'Test Hat',
				MatchPlayerColor: true,
				BlocksVisors: false,
				InFront: true,
				NoBounce: false,
				PreviewSprite: { Size: 100, Offset: 0 },
				MainSprite: { Size: 200, Offset: 100 },
				BackSprite: { Size: 0, Offset: 0 },
				ClimbSprite: { Size: 0, Offset: 0 },
				FloorSprite: { Size: 0, Offset: 0 },
				LeftMainSprite: { Size: 0, Offset: 0 },
				LeftBackSprite: { Size: 0, Offset: 0 },
				LeftClimbSprite: { Size: 0, Offset: 0 },
				LeftFloorSprite: { Size: 0, Offset: 0 },
			},
		],
	};

	it('serializes to UTF-8 bytes', () => {
		const bytes = serializeManifest(sampleManifest);
		expect(bytes).toBeInstanceOf(Uint8Array);
		expect(bytes.byteLength).toBeGreaterThan(0);
	});

	it('uses PascalCase property names in JSON output', () => {
		const bytes = serializeManifest(sampleManifest);
		const json = new TextDecoder().decode(bytes);
		expect(json).toContain('"Version"');
		expect(json).toContain('"Hats"');
		expect(json).toContain('"Name"');
		expect(json).toContain('"MatchPlayerColor"');
		expect(json).toContain('"PreviewSprite"');
		expect(json).toContain('"MainSprite"');
		expect(json).toContain('"LeftFloorSprite"');
	});

	it('deserializes back to identical manifest', () => {
		const bytes = serializeManifest(sampleManifest);
		const buf = bytes.buffer;
		const parsed = deserializeManifest(buf, 0, bytes.byteLength);
		expect(parsed.Version).toBe(sampleManifest.Version);
		expect(parsed.Hats.length).toBe(1);
		expect(parsed.Hats[0].Name).toBe('Test Hat');
		expect(parsed.Hats[0].MatchPlayerColor).toBe(true);
		expect(parsed.Hats[0].MainSprite.Size).toBe(200);
		expect(parsed.Hats[0].MainSprite.Offset).toBe(100);
	});
});

// ---------------------------------------------------------------------------
// 5. Full round-trip: assembleBundle → parseBundle
// ---------------------------------------------------------------------------

describe('full bundle round-trip', () => {
	it('assembles and parses a single hat with two sprites', async () => {
		const png1 = makeMinimalPng();
		const png2 = makeMinimalPng();

		const hat = createHatEntry();
		hat.manifest.Name = 'Round Trip Hat';
		hat.manifest.MatchPlayerColor = true;
		hat.manifest.BlocksVisors = false;
		hat.manifest.InFront = true;
		hat.manifest.NoBounce = false;
		hat.imageBytes['PreviewSprite'] = png1;
		hat.imageBytes['MainSprite'] = png2;

		const { blob, manifestLength, dataLength } = assembleBundle([hat]);

		expect(manifestLength).toBeGreaterThan(0);
		expect(dataLength).toBe(png1.byteLength + png2.byteLength);

		// Convert blob to ArrayBuffer
		const buffer = await blob.arrayBuffer();

		// Total size = 16 (header) + manifestLength + dataLength
		expect(buffer.byteLength).toBe(HEADER_SIZE + manifestLength + dataLength);

		// Parse back
		const parsed = parseBundle(buffer);
		expect(parsed.hats.length).toBe(1);

		const h = parsed.hats[0];
		expect(h.manifest.Name).toBe('Round Trip Hat');
		expect(h.manifest.MatchPlayerColor).toBe(true);
		expect(h.manifest.InFront).toBe(true);
		expect(h.manifest.NoBounce).toBe(false);

		// Verify sprite byte lengths match
		expect(h.imageBytes['PreviewSprite']?.byteLength).toBe(png1.byteLength);
		expect(h.imageBytes['MainSprite']?.byteLength).toBe(png2.byteLength);
	});

	it('correctly handles a hat with no sprites (all Size=0, Offset=0)', async () => {
		const hat = createHatEntry();
		hat.manifest.Name = 'Empty Hat';

		const { blob, dataLength } = assembleBundle([hat]);
		expect(dataLength).toBe(0);

		const buffer = await blob.arrayBuffer();
		const parsed = parseBundle(buffer);
		expect(parsed.hats.length).toBe(1);
		expect(parsed.hats[0].manifest.Name).toBe('Empty Hat');
		expect(Object.keys(parsed.hats[0].imageBytes).length).toBe(0);
	});

	it('preserves correct offsets for multiple hats', async () => {
		const pngA = makeMinimalPng(); // goes into hat1 MainSprite
		const pngB = makeMinimalPng(); // same size, goes into hat2 PreviewSprite

		const hat1 = createHatEntry();
		hat1.manifest.Name = 'Hat Alpha';
		hat1.imageBytes['MainSprite'] = pngA;

		const hat2 = createHatEntry();
		hat2.manifest.Name = 'Hat Beta';
		hat2.imageBytes['PreviewSprite'] = pngB;

		const { blob } = assembleBundle([hat1, hat2]);
		const buffer = await blob.arrayBuffer();
		const parsed = parseBundle(buffer);

		expect(parsed.hats.length).toBe(2);
		// hat1.MainSprite should be at offset 0
		expect(parsed.hats[0].manifest.MainSprite.Offset).toBe(0);
		// hat2.PreviewSprite should be at offset = pngA.byteLength
		expect(parsed.hats[1].manifest.PreviewSprite.Offset).toBe(pngA.byteLength);

		// Both image blobs should be recoverable and correct size
		expect(parsed.hats[0].imageBytes['MainSprite']?.byteLength).toBe(pngA.byteLength);
		expect(parsed.hats[1].imageBytes['PreviewSprite']?.byteLength).toBe(pngB.byteLength);
	});
});

// ---------------------------------------------------------------------------
// 6. parseBundle rejects corrupt magic
// ---------------------------------------------------------------------------

describe('parseBundle error handling', () => {
	it('throws on wrong magic number', async () => {
		const hat = createHatEntry();
		const { blob } = assembleBundle([hat]);
		const buf = await blob.arrayBuffer();

		// Corrupt magic
		const view = new DataView(buf);
		view.setUint32(0, 0xdeadbeef, true);

		expect(() => parseBundle(buf)).toThrow(/magic/i);
	});

	it('throws on unsupported version > 1', async () => {
		const hat = createHatEntry();
		const { blob } = assembleBundle([hat]);
		const buf = await blob.arrayBuffer();

		const view = new DataView(buf);
		view.setUint16(4, 99, true); // version = 99

		expect(() => parseBundle(buf)).toThrow(/version/i);
	});

	it('throws on truncated file', () => {
		const tinyBuf = new ArrayBuffer(8); // too short for a full header
		expect(() => parseBundle(tinyBuf)).toThrow();
	});
});
