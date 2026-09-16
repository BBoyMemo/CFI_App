/**
 * A v4 UUID, in every context this app runs in.
 *
 * `crypto.randomUUID()` exists only in a secure context - HTTPS, or localhost. Open the
 * app from a phone on the office wifi over plain http and it is simply undefined, which
 * would throw on every single request, since the correlation id is set on all of them.
 *
 * `crypto.getRandomValues` has no such restriction, so it carries the fallback: same
 * randomness, same format, assembled by hand. The last resort covers nothing we expect
 * to meet, but a missing id must never be the reason a fault report fails to send.
 */
export function newUuid() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  if (typeof crypto !== 'undefined' && typeof crypto.getRandomValues === 'function') {
    const bytes = crypto.getRandomValues(new Uint8Array(16));

    bytes[6] = (bytes[6] & 0x0f) | 0x40; // version 4
    bytes[8] = (bytes[8] & 0x3f) | 0x80; // variant 1

    const hex = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('');

    return [
      hex.slice(0, 8), hex.slice(8, 12), hex.slice(12, 16), hex.slice(16, 20), hex.slice(20),
    ].join('-');
  }

  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replaceAll(/[xy]/g, (character) => {
    const random = Math.trunc(Math.random() * 16);
    const value = character === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}
