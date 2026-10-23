// Wrapper de fetch para la API (mismo origen). Suma el token, interpreta los errores y centraliza el 401 y el 429.
//
//   const perfil = await api("/api/me/profile");
//   await api("/api/me/metrics", { method: "POST", body: { weightKg: 80 } });
//
// Errores: lanza ApiError con status, title (ProblemDetails) y fieldErrors ({ campo: "mensaje" }, de
// ValidationProblemDetails). Para mostrarlos al lado de cada campo, usá showErrors de forms.js.

import { clearSession, getToken } from "./auth.js";

export class ApiError extends Error {
  constructor(status, title, fieldErrors = {}, retryAfter = null) {
    super(title);
    this.status = status;
    this.title = title;
    this.fieldErrors = fieldErrors;
    this.retryAfter = retryAfter;
  }
}

// options:
//   method, body (se manda como JSON),
//   redirectOn401 (por defecto true): con un 401 se borra la sesión y se vuelve a #/login. Login y borrado de
//     cuenta lo apagan, porque ahí un 401 es "contraseña incorrecta".
export async function api(path, { method = "GET", body, redirectOn401 = true } = {}) {
  const headers = { Accept: "application/json" };
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers["Content-Type"] = "application/json";

  let response;
  try {
    response = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  } catch {
    throw new ApiError(0, "No se pudo conectar con la API. ¿Está corriendo?");
  }

  if (response.ok) {
    if (response.status === 204) return null;
    const type = response.headers.get("Content-Type") ?? "";
    return type.includes("json") ? response.json() : response.text();
  }

  const problem = await readProblem(response);

  if (response.status === 401 && redirectOn401) {
    clearSession();
    if (location.hash !== "#/login") location.hash = "#/login";
    throw new ApiError(401, "La sesión venció o no es válida. Ingresá de nuevo.");
  }

  if (response.status === 429) {
    const seconds = Number.parseInt(response.headers.get("Retry-After") ?? "", 10);
    const when = Number.isFinite(seconds) ? ` en ${seconds} segundos` : " en un rato";
    throw new ApiError(429, `Demasiados intentos. Probá de nuevo${when}.`, {}, Number.isFinite(seconds) ? seconds : null);
  }

  // El título de un 5xx es genérico y en inglés ("An error occurred…"): se muestra uno propio.
  const title = response.status >= 500 ? defaultTitle(response.status) : problem.title ?? defaultTitle(response.status);
  throw new ApiError(response.status, title, normalizeErrors(problem.errors));
}

// true si el endpoint existe (cualquier respuesta que no sea 404). Sirve para activar pantallas cuando
// otro integrante suma su API: un GET a una ruta que solo acepta POST da 405, no 404.
export async function endpointExists(path) {
  try {
    const token = getToken();
    const response = await fetch(path, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
    return response.status !== 404;
  } catch {
    return false;
  }
}

async function readProblem(response) {
  const type = response.headers.get("Content-Type") ?? "";
  if (!type.includes("json")) return {};
  try {
    return await response.json();
  } catch {
    return {};
  }
}

// { "weightKg": ["msg"], "$.date": ["msg"] } → { weightKg: "msg", date: "msg" }.
// Las claves vienen del ParamName del manager (ya en camelCase) o del model binding ("$.campo" o "Campo").
function normalizeErrors(errors) {
  const result = {};
  if (!errors || typeof errors !== "object") return result;
  for (const [key, messages] of Object.entries(errors)) {
    const clean = key.replace(/^\$\.?/, "").split(".").pop() || "_";
    const field = clean.charAt(0).toLowerCase() + clean.slice(1);
    const text = Array.isArray(messages) ? messages.join(" ") : String(messages);
    result[field] = result[field] ? `${result[field]} ${text}` : text;
  }
  return result;
}

function defaultTitle(status) {
  if (status === 404) return "No se encontró.";
  if (status === 409) return "No se pudo hacer: hay un conflicto con tus datos.";
  if (status >= 500) return "Algo falló en el servidor. Probá de nuevo.";
  return "No se pudo completar la operación.";
}
