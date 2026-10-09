import AsyncStorage from '@react-native-async-storage/async-storage';

// Same REST API as the web app, on the production server.
const SERVER = 'https://ynzserver.duckdns.org/dailytasks';

const SESSION_KEY = 'dt.session';

export class ApiError extends Error {
  constructor(status, body) {
    super(body?.title || `Request failed (${status})`);
    this.status = status;
    this.code = body?.code ?? null;
    this.fieldErrors = body?.errors ?? null;
  }
}

let session = null;
let onUnauthorized = () => {};

export function setUnauthorizedHandler(handler) {
  onUnauthorized = handler;
}

export async function loadStored() {
  try {
    const storedSession = await AsyncStorage.getItem(SESSION_KEY);
    const parsed = storedSession ? JSON.parse(storedSession) : null;
    session = parsed && new Date(parsed.expiresAt) > new Date() ? parsed : null;
  } catch {
    session = null;
  }
  return {session};
}

export async function saveSession(next) {
  session = next;
  if (next) {
    await AsyncStorage.setItem(SESSION_KEY, JSON.stringify(next)).catch(
      () => {},
    );
  } else {
    await AsyncStorage.removeItem(SESSION_KEY).catch(() => {});
  }
}

export function authHeaders() {
  return session ? {Authorization: `Bearer ${session.token}`} : {};
}

export function photoSource(path) {
  return {uri: `${SERVER}/api${path}`, headers: authHeaders()};
}

async function request(
  method,
  path,
  {json, form, skipAuthHandling, timeoutMs = 20000} = {},
) {
  const headers = {Accept: 'application/json', ...authHeaders()};
  let body;
  if (json !== undefined) {
    headers['Content-Type'] = 'application/json';
    body = JSON.stringify(json);
  } else if (form) {
    body = form;
  }

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  let response;
  try {
    response = await fetch(`${SERVER}/api${path}`, {
      method,
      headers,
      body,
      signal: controller.signal,
    });
  } catch {
    throw new ApiError(0, {code: 'network'});
  } finally {
    clearTimeout(timer);
  }

  if (response.status === 401 && !skipAuthHandling) {
    onUnauthorized();
  }
  if (response.status === 204) {
    return null;
  }
  const text = await response.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = null;
  }
  if (!response.ok) {
    throw new ApiError(
      response.status,
      data ?? (response.status === 429 ? {code: 'tooManyRequests'} : null),
    );
  }
  return data;
}

// React Native's FormData takes {uri, name, type} for files.
export function photoPart(photo) {
  return {
    uri: photo.uri,
    name: photo.fileName || 'photo.jpg',
    type: photo.type || 'image/jpeg',
  };
}

export const api = {
  login: (name, password) =>
    request('POST', '/auth/login', {
      json: {name, password},
      skipAuthHandling: true,
    }),

  changePassword: (currentPassword, newPassword) =>
    request('POST', '/auth/change-password', {
      json: {currentPassword, newPassword},
    }),

  users: () => request('GET', '/users'),
  createUser: user => request('POST', '/users', {json: user}),

  tasks: date => request('GET', `/tasks?date=${encodeURIComponent(date)}`),
  taskHistory: (page, {q, date} = {}, pageSize = 20) => {
    let query = `page=${page}&pageSize=${pageSize}`;
    if (q) {
      query += `&q=${encodeURIComponent(q)}`;
    }
    if (date) {
      query += `&date=${date}`;
    }
    return request('GET', `/tasks/history?${query}`);
  },
  createTask: task => request('POST', '/tasks', {json: task}),
  updateTask: (id, task) => request('PUT', `/tasks/${id}`, {json: task}),
  deleteTask: id => request('DELETE', `/tasks/${id}`),
  addTaskPhoto: (id, form) =>
    request('POST', `/tasks/${id}/photos`, {form, timeoutMs: 60000}),
  deleteTaskPhoto: (id, photoId) =>
    request('DELETE', `/tasks/${id}/photos/${photoId}`),
  // A progress / completion / follow-up card: FormData with outcome, comment and photos.
  addTaskUpdate: (id, form) =>
    request('POST', `/tasks/${id}/updates`, {form, timeoutMs: 60000}),

  orders: ({status, q, date} = {}, page = 1, pageSize = 20) => {
    let query = `page=${page}&pageSize=${pageSize}`;
    if (status) {
      query += `&status=${status}`;
    }
    if (q) {
      query += `&q=${encodeURIComponent(q)}`;
    }
    if (date) {
      query += `&date=${date}`;
    }
    return request('GET', `/orders?${query}`);
  },
  createOrder: form => request('POST', '/orders', {form, timeoutMs: 60000}),
  markOrdered: id => request('POST', `/orders/${id}/mark-ordered`),
  deleteOrder: id => request('DELETE', `/orders/${id}`),
};
