/**
 * Where the API lives.
 *
 * Empty by default, which means "the same origin this page came from" - the shape both
 * production (nginx forwards /api to the API) and development (the Vite proxy does the
 * same) actually use. It is what lets a phone on the wifi open the app without anything
 * knowing the server's IP address, and it keeps every request same-origin, so CORS is
 * simply not part of the picture.
 *
 * Set VITE_API_BASE_URL only when the API genuinely is somewhere else - a separate host,
 * or a frontend served from a different domain than the API.
 */
const rawBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

export const API_BASE_URL = rawBaseUrl.replace(/\/+$/, '');
