// Perfil: GET /api/me/profile (404 = todavía no tiene) y PUT /api/me/profile
// { birthDate, biologicalSex, heightCm, activityLevel, timeZoneId }.
import { api } from "../api.js";
import { append, el, setChildren } from "../dom.js";
import { field, formError, notice, onSubmit, options } from "../forms.js";
import { go } from "../router.js";
import { DEFAULT_TIME_ZONE, timeZones } from "../timezones.js";

const SEXES = [["Female", "Mujer"], ["Male", "Hombre"]];
const ACTIVITY = [
  ["Sedentary", "Sedentaria (poco o nada de ejercicio)"],
  ["Light", "Liviana (1 a 3 días por semana)"],
  ["Moderate", "Moderada (3 a 5 días por semana)"],
  ["Active", "Alta (6 a 7 días por semana)"],
  ["VeryActive", "Muy alta (trabajo físico o doble turno)"],
];

export async function render(container) {
  let profile = null;
  try {
    profile = await api("/api/me/profile");
  } catch (error) {
    if (error.status !== 404) throw error;
  }

  const status = el("div");
  const form = el("form", { novalidate: true },
    field("Fecha de nacimiento", el("input", { type: "date", name: "birthDate", required: true, value: profile?.birthDate })),
    field("Sexo biológico", el("select", { name: "biologicalSex" }, options(SEXES, profile?.biologicalSex)),
      "Lo usa la fórmula de gasto (Mifflin-St Jeor)."),
    field("Altura (cm)", el("input", { type: "number", name: "heightCm", min: 100, max: 250, step: "0.1", required: true, value: profile?.heightCm })),
    field("Nivel de actividad", el("select", { name: "activityLevel" }, options(ACTIVITY, profile?.activityLevel ?? "Moderate"))),
    field("Zona horaria", el("select", { name: "timeZoneId" },
      options(timeZones().map((z) => [z, z.replaceAll("_", " ")]), profile?.timeZoneId ?? DEFAULT_TIME_ZONE)),
      "Define qué día es \"hoy\" para tus comidas y tu peso."),
    formError(),
    el("button", { type: "submit", class: "button" }, profile ? "Guardar cambios" : "Guardar perfil"));

  onSubmit(form, async (data) => {
    const height = data.get("heightCm");
    const saved = await api("/api/me/profile", {
      method: "PUT",
      body: {
        birthDate: data.get("birthDate") || null,
        biologicalSex: data.get("biologicalSex"),
        heightCm: height === "" ? null : Number(height),
        activityLevel: data.get("activityLevel"),
        timeZoneId: data.get("timeZoneId"),
      },
    });
    const first = profile === null;
    profile = saved;
    if (first) go("/peso");
    else setChildren(status, notice("ok", "Perfil guardado."));
  });

  append(container, [
    el("h1", {}, "Perfil"),
    profile ? null : notice("warning", "Completá tu perfil para calcular tus objetivos."),
    status,
    el("section", { class: "card profile-card" }, form)]);
}
