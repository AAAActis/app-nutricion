// Registro: POST /api/auth/register { email, password, consentAccepted } → 201. Después entra solo (login).
import { api } from "../api.js";
import { CONSENT_TEXT, CONSENT_VERSION } from "../consent.js";
import { append, el } from "../dom.js";
import { formError, field, onSubmit } from "../forms.js";
import { go } from "../router.js";
import { login } from "./login.js";

export async function render(container) {
  const consentError = el("div", { class: "field-error", "data-error-for": "consentAccepted", id: "consent-error" });
  const form = el("form", { novalidate: true },
    field("Email", el("input", { type: "email", name: "email", autocomplete: "email", required: true })),
    field("Contraseña", el("input", {
      type: "password", name: "password", autocomplete: "new-password", required: true, minlength: 8, maxlength: 128,
    }), "Entre 8 y 128 caracteres."),
    el("div", { class: "field" },
      el("strong", {}, `Consentimiento (versión ${CONSENT_VERSION})`),
      el("div", { class: "consent", tabindex: 0 }, CONSENT_TEXT.map((p) => el("p", {}, p))),
      el("label", { class: "checkbox" },
        el("input", { type: "checkbox", name: "consentAccepted", "aria-describedby": "consent-error" }),
        el("span", {}, "Leí y acepto el tratamiento de mis datos.")),
      consentError),
    formError(),
    el("button", { type: "submit", class: "button" }, "Crear cuenta"));

  onSubmit(form, async (data) => {
    const email = String(data.get("email"));
    const password = String(data.get("password"));
    await api("/api/auth/register", {
      method: "POST",
      body: { email, password, consentAccepted: data.get("consentAccepted") === "on" },
    });
    await login(email, password);
    go("/perfil");
  });

  append(container, [
    el("h1", {}, "Crear cuenta"),
    el("section", { class: "card" }, form),
    el("p", {}, "¿Ya tenés cuenta? ", el("a", { href: "#/login" }, "Ingresá"))]);
}
