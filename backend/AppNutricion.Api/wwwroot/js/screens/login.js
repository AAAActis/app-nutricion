// Ingreso: POST /api/auth/login { email, password } → { accessToken, expiresAt }.
import { api } from "../api.js";
import { saveSession } from "../auth.js";
import { append, el } from "../dom.js";
import { field, formError, onSubmit } from "../forms.js";
import { go } from "../router.js";

export async function render(container) {
  const form = el("form", { novalidate: true },
    field("Email", el("input", { type: "email", name: "email", autocomplete: "email", required: true })),
    field("Contraseña", el("input", { type: "password", name: "password", autocomplete: "current-password", required: true })),
    formError(),
    el("button", { type: "submit", class: "button" }, "Ingresar"));

  onSubmit(form, async (data) => {
    await login(String(data.get("email")), String(data.get("password")));
    await goToFirstScreen();
  });

  append(container, [
    el("h1", {}, "Ingresar"),
    el("section", { class: "card" }, form),
    el("p", {}, "¿No tenés cuenta? ", el("a", { href: "#/registro" }, "Creá una"))]);
}

// Login y guardado de la sesión. Lo usa también el registro.
export async function login(email, password) {
  // Acá un 401 es "email o contraseña incorrectos": no se redirige.
  const session = await api("/api/auth/login", { method: "POST", body: { email, password }, redirectOn401: false });
  saveSession(session);
}

// Sin perfil, primero el perfil; si no, el día.
export async function goToFirstScreen() {
  try {
    await api("/api/me/profile");
    go("/dia");
  } catch (error) {
    if (error.status === 404) go("/perfil");
    else throw error;
  }
}
