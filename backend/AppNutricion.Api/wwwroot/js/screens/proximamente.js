// Pantalla de otro integrante que todavía no está: "Próximamente" hasta que exista su endpoint, y un aviso
// cuando ya existe (la pantalla completa la hace quien hace la API). No es una ruta: la usan rutinas.js y chat.js.
import { endpointExists } from "../api.js";
import { el } from "../dom.js";
import { notice } from "../forms.js";

export async function renderPending(container, { title, endpoint, description, owner }) {
  const available = await endpointExists(endpoint);
  container.append(
    el("h1", {}, title),
    available
      ? notice("ok", `La API ya tiene ${endpoint}. Esta pantalla la completa ${owner}.`)
      : el("section", { class: "card" }, el("h2", {}, "Próximamente"), el("p", {}, description)));
}
