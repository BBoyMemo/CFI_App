// Thin fetch wrapper. Every call goes to the same origin (/api), proxied to the API in dev.

const TOKEN_KEY = 'dt.session';

export class ApiError extends Error {
  constructor(status, body) {
    super(body?.title || `Request failed (${status})`);
    this.status = status;
    this.code = body?.code ?? null;
    this.fieldErrors = body?.errors ?? null;
  }
}

let onUnauthorized = () => {};

export function setUnauthorizedHandler(handler) {
  onUnauthorized = handler;
}

export function loadSession() {
  try {
    const raw = localStorage.getItem(TOKEN_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw);
    if (!session?.token || new Date(session.expiresAt) <= new Date()) {
      localStorage.removeItem(TOKEN_KEY);
      return null;
    }
    return session;
  } catch {
    return null;
  }
}

export function saveSession(session) {
  try {
    if (session) localStorage.setItem(TOKEN_KEY, JSON.stringify(session));
    else localStorage.removeItem(TOKEN_KEY);
  } catch {
    // Storage unavailable (private mode): the session lives only in memory for this tab.
  }
}

function authHeader() {
  const session = loadSession();
  return session ? { Authorization: `Bearer ${session.token}` } : {};
}

async function request(method, path, { json, form, skipAuthHandling } = {}) {
  const headers = { ...authHeader() };
  let body;
  if (json !== undefined) {
    headers['Content-Type'] = 'application/json';
    body = JSON.stringify(json);
  } else if (form) {
    body = form;
  }

  let response;
  try {
    response = await fetch(`/api${path}`, { method, headers, body });
  } catch {
    throw new ApiError(0, { code: 'network' });
  }

  if (response.status === 401 && !skipAuthHandling) {
    onUnauthorized();
  }
  if (response.status === 204) return null;

  const isJson = response.headers.get('content-type')?.includes('json');
  const data = isJson ? await response.json().catch(() => null) : null;
  if (!response.ok) {
    throw new ApiError(response.status, data ?? (response.status === 429 ? { code: 'tooManyRequests' } : null));
  }
  return data;
}

// Photos need the bearer token, so they are fetched as blobs rather than linked with <img src>.
export async function fetchPhotoUrl(path) {
  const response = await fetch(`/api${path}`, { headers: authHeader() });
  if (response.status === 401) onUnauthorized();
  if (!response.ok) throw new ApiError(response.status, null);
  return URL.createObjectURL(await response.blob());
}

export const api = {
  login: (name, password) =>
    request('POST', '/auth/login', { json: { name, password }, skipAuthHandling: true }),
  me: () => request('GET', '/auth/me'),
  changePassword: (currentPassword, newPassword) =>
    request('POST', '/auth/change-password', { json: { currentPassword, newPassword } }),

  users: () => request('GET', '/users'),
  createUser: (user) => request('POST', '/users', { json: user }),

  tasks: (date) => request('GET', `/tasks?date=${encodeURIComponent(date)}`),
  taskHistory: (page, { q, date } = {}, pageSize = 20) => {
    const params = new URLSearchParams({ page, pageSize });
    if (q) params.set('q', q);
    if (date) params.set('date', date);
    return request('GET', `/tasks/history?${params}`);
  },
  createTask: (task) => request('POST', '/tasks', { json: task }),
  updateTask: (id, task) => request('PUT', `/tasks/${id}`, { json: task }),
  deleteTask: (id) => request('DELETE', `/tasks/${id}`),
  completeTask: (id, form) => request('POST', `/tasks/${id}/complete`, { form }),
  addTaskPhoto: (id, form) => request('POST', `/tasks/${id}/photos`, { form }),
  deleteTaskPhoto: (id, photoId) => request('DELETE', `/tasks/${id}/photos/${photoId}`),

  orders: ({ status, q, date } = {}, page = 1, pageSize = 20) => {
    const params = new URLSearchParams({ page, pageSize });
    if (status) params.set('status', status);
    if (q) params.set('q', q);
    if (date) params.set('date', date);
    return request('GET', `/orders?${params}`);
  },
  createOrder: (form) => request('POST', '/orders', { form }),
  markOrdered: (id) => request('POST', `/orders/${id}/mark-ordered`),
  deleteOrder: (id) => request('DELETE', `/orders/${id}`),
};
