// Sesión del usuario: el access token y su vencimiento, en sessionStorage (se borra al cerrar la pestaña).
// No hay refresh token todavía (J3): cuando vence, se vuelve a #/login.

const KEY = "appnutricion.session";

export function saveSession({ accessToken, expiresAt }) {
  sessionStorage.setItem(KEY, JSON.stringify({ accessToken, expiresAt }));
}

export function clearSession() {
  sessionStorage.removeItem(KEY);
}

// El token, o null si no hay sesión o ya venció.
export function getToken() {
  const raw = sessionStorage.getItem(KEY);
  if (!raw) return null;
  try {
    const { accessToken, expiresAt } = JSON.parse(raw);
    if (!accessToken || !expiresAt || Date.parse(expiresAt) <= Date.now()) {
      clearSession();
      return null;
    }
    return accessToken;
  } catch {
    clearSession();
    return null;
  }
}

export function isLoggedIn() {
  return getToken() !== null;
}
