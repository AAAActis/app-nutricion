// Cuenta: exportar mis datos (GET /api/me/export → descarga .json) y borrar la cuenta (DELETE /api/me { password }).
// Esos endpoints son de J5 (feature/cuenta-borrado-export): mientras no estén en develop, la API responde
// 404/405 y la pantalla lo dice. Se activan solos cuando se mergea J5.
import { api } from "../api.js";
import { clearSession } from "../auth.js";
import { el, setChildren } from "../dom.js";
import { field, formError, notice, onSubmit } from "../forms.js";
import { today } from "../format.js";
import { go, setFlash } from "../router.js";

const NOT_YET = "Todavía no está disponible en esta versión de la API (llega con J5).";

export async function render(container) {
  const exportStatus = el("div");
  const exportButton = el("button", { type: "button", class: "button" }, "Descargar mis datos (.json)");
  exportButton.addEventListener("click", async () => {
    exportButton.disabled = true;
    try {
      const data = await api("/api/me/export");
      download(`mis-datos-${today()}.json`, JSON.stringify(data, null, 2));
      setChildren(exportStatus, notice("ok", "Listo: se descargó el archivo con todos tus datos."));
    } catch (error) {
      setChildren(exportStatus, notice("error", error.status === 404 ? NOT_YET : error.title));
    } finally {
      exportButton.disabled = false;
    }
  });

  const confirmBox = el("input", { type: "checkbox", name: "confirm" });
  const form = el("form", { novalidate: true },
    field("Contraseña actual", el("input", { type: "password", name: "password", autocomplete: "current-password", required: true })),
    el("label", { class: "checkbox" }, confirmBox, el("span", {}, "Entiendo que se borran mi cuenta y todos mis datos, y que no se puede deshacer.")),
    formError(),
    el("button", { type: "submit", class: "button danger" }, "Borrar mi cuenta"));

  onSubmit(form, async (data) => {
    if (!confirmBox.checked) {
      throw { status: 400, title: "Marcá la casilla para confirmar.", fieldErrors: {} };
    }
    if (!confirm("¿Seguro? Se borran tu cuenta y todos tus datos. No se puede deshacer.")) return;
    try {
      // Acá un 401 es "contraseña incorrecta": no se redirige.
      await api("/api/me", { method: "DELETE", body: { password: String(data.get("password")) }, redirectOn401: false });
    } catch (error) {
      if (error.status === 401) throw { status: 401, title: "", fieldErrors: { password: error.title } };
      if (error.status === 404 || error.status === 405) throw { status: error.status, title: NOT_YET, fieldErrors: {} };
      throw error;
    }
    clearSession();
    setFlash("ok", "Tu cuenta y todos tus datos se borraron.");
    go("/login");
  });

  container.append(
    el("h1", {}, "Cuenta"),
    el("section", { class: "card" },
      el("h2", {}, "Exportar mis datos"),
      el("p", {}, "Un archivo JSON con tu cuenta, perfil, pesos, objetivos, comidas, favoritos, alimentos propios, recetas, rutinas, entrenamientos y conversaciones (Ley 25.326)."),
      exportStatus,
      exportButton),
    el("section", { class: "card" },
      el("h2", {}, "Borrar mi cuenta"),
      el("p", {}, "Se borran tu cuenta y todos tus datos. Antes podés descargarlos."),
      form));
}

// Descarga un texto como archivo, sin pasar por el servidor.
function download(fileName, text) {
  const url = URL.createObjectURL(new Blob([text], { type: "application/json" }));
  const link = el("a", { href: url, download: fileName });
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
