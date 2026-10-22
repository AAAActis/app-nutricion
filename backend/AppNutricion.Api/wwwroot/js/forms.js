// Formularios: campos con su lugar para el error, y cómo mostrar un ApiError (cada error al lado de su campo;
// lo que no es de un campo, arriba del botón).

import { el, setChildren } from "./dom.js";

// Un campo con etiqueta y lugar para el error. "name" tiene que ser el nombre que usa la API
// (el mismo que vuelve en ValidationProblemDetails), por ejemplo "weightKg".
export function field(labelText, input, hint) {
  const id = input.id || `f-${input.name}`;
  input.id = id;
  const error = el("div", { class: "field-error", id: `${id}-error`, "data-error-for": input.name, "aria-live": "polite" });
  input.setAttribute("aria-describedby", `${id}-error`);
  return el("div", { class: "field" },
    el("label", { for: id }, labelText),
    input,
    hint ? el("div", { class: "muted small" }, hint) : null,
    error);
}

export function formError() {
  return el("div", { class: "form-error", role: "alert" });
}

export function clearErrors(form) {
  for (const node of form.querySelectorAll("[data-error-for]")) node.textContent = "";
  for (const node of form.querySelectorAll("[aria-invalid]")) node.removeAttribute("aria-invalid");
  const general = form.querySelector(".form-error");
  if (general) general.textContent = "";
}

// Muestra un error de la API. Los errores de campos que el formulario no tiene van al mensaje general.
export function showErrors(form, error) {
  clearErrors(form);
  const leftovers = [];
  for (const [name, message] of Object.entries(error.fieldErrors ?? {})) {
    const slot = form.querySelector(`[data-error-for="${CSS.escape(name)}"]`);
    const input = form.elements.namedItem(name);
    if (slot) {
      slot.textContent = message;
      if (input && "setAttribute" in input) input.setAttribute("aria-invalid", "true");
    } else {
      leftovers.push(message);
    }
  }
  const general = form.querySelector(".form-error");
  const hasFieldErrors = Object.keys(error.fieldErrors ?? {}).length > 0;
  const text = [hasFieldErrors && leftovers.length === 0 ? "" : error.title, ...leftovers].filter(Boolean).join(" ");
  if (general) general.textContent = text;
  else if (text) alertText(text);
}

function alertText(text) {
  console.warn(text);
}

// Envío con el botón deshabilitado mientras espera, y los errores mostrados en el formulario.
export function onSubmit(form, handler) {
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    clearErrors(form);
    const button = form.querySelector("button[type=submit]");
    if (button) button.disabled = true;
    try {
      await handler(new FormData(form));
    } catch (error) {
      if (error && "status" in error) showErrors(form, error);
      else {
        console.error(error);
        showErrors(form, { title: "Algo falló en la página. Recargá y probá de nuevo.", fieldErrors: {} });
      }
    } finally {
      if (button) button.disabled = false;
    }
  });
  return form;
}

// Mensaje arriba de la pantalla (ok / warning / error).
export function notice(kind, ...text) {
  return el("div", { class: `notice ${kind}`, role: kind === "ok" ? "status" : "alert" }, ...text);
}

// Opciones de un <select> a partir de [valor, texto].
export function options(pairs, selected) {
  return pairs.map(([value, label]) => el("option", { value, selected: value === selected }, label));
}

export function setNotice(slot, kind, ...text) {
  setChildren(slot, notice(kind, ...text));
}
