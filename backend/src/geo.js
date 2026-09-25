const alphabet = '0123456789bcdefghjkmnpqrstuvwxyz';

export function geohash(latitude, longitude, length = 5) {
    let lat = [-90, 90];
    let lon = [-180, 180];
    let bits = 0;
    let value = 0;
    let result = '';
    while (result.length < length) {
        const range = bits % 2 === 0 ? lon : lat;
        const point = bits % 2 === 0 ? longitude : latitude;
        const midpoint = (range[0] + range[1]) / 2;
        value = (value << 1) | Number(point >= midpoint);
        range[point >= midpoint ? 0 : 1] = midpoint;
        bits++;
        if (bits % 5 === 0) { result += alphabet[value]; value = 0; }
    }
    return result;
}

function bounds(hash) {
    const lat = [-90, 90];
    const lon = [-180, 180];
    let bit = 0;
    for (const letter of hash) {
        const value = alphabet.indexOf(letter);
        for (let shift = 4; shift >= 0; shift--) {
            const range = bit++ % 2 === 0 ? lon : lat;
            range[(value >> shift) & 1 ? 0 : 1] = (range[0] + range[1]) / 2;
        }
    }
    return { lat, lon };
}

export function neighboringCells(latitude, longitude) {
    const cell = bounds(geohash(latitude, longitude));
    const height = cell.lat[1] - cell.lat[0];
    const width = cell.lon[1] - cell.lon[0];
    return [...new Set([-1, 0, 1].flatMap(y => [-1, 0, 1].map(x =>
        geohash(Math.max(-89.999999, Math.min(89.999999, latitude + y * height)),
            ((longitude + x * width + 540) % 360) - 180))))];
}

export function distanceMeters(a, b) {
    const radians = Math.PI / 180;
    const dLat = (b.latitude - a.latitude) * radians;
    const dLon = (b.longitude - a.longitude) * radians;
    const sinLat = Math.sin(dLat / 2);
    const sinLon = Math.sin(dLon / 2);
    const sphere = sinLat * sinLat + Math.cos(a.latitude * radians) * Math.cos(b.latitude * radians) * sinLon * sinLon;
    return 6371000 * 2 * Math.asin(Math.min(1, Math.sqrt(sphere)));
}
