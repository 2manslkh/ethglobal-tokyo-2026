import sharp from 'sharp';

export const MAX_DESIGN_BYTES = 5 * 1024 * 1024;
export const MAX_DESIGN_EDGE = 1024;

export async function inspectDesignImage(bytes) {
    if (!Buffer.isBuffer(bytes) || bytes.length < 8 || bytes.length > MAX_DESIGN_BYTES ||
        !bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))) throw new Error('Invalid PNG upload');
    const image = sharp(bytes, { failOn: 'error', limitInputPixels: MAX_DESIGN_EDGE * MAX_DESIGN_EDGE });
    const metadata = await image.metadata();
    if (metadata.format !== 'png' || !metadata.width || !metadata.height ||
        metadata.width > MAX_DESIGN_EDGE || metadata.height > MAX_DESIGN_EDGE || metadata.pages > 1) throw new Error('Invalid PNG dimensions');
    const artwork = await image.png().toBuffer();
    const thumbnail = await sharp(artwork).resize({ width: 256, height: 256, fit: 'inside', withoutEnlargement: true }).png().toBuffer();
    return { width: metadata.width, height: metadata.height, artwork, thumbnail };
}
