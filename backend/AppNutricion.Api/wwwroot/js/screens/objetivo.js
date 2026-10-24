// Objetivo: POST /api/me/goals { goalType, weeklyRateKg, targetWeightKg? } → 201 con los targets, y
// GET /api/me/targets (404 = sin objetivo activo). Los targets: calories, proteinG, carbsG, fatG, bmi,
// bmiCategory, projection { weeks, estimatedDate } | null, warnings [{ code, message }], disclaimer.
import { api } from "../api.js";
import { append, el, setChildren } from "../dom.js";
import { field, formError, notice, onSubmit, options } from "../forms.js";
import { dateText, num } from "../format.js";

export const DISCLAIMER = "Estimación, no es consejo médico.";

const GOALS = [["LoseFat", "Bajar grasa"], ["Maintain", "Mantener"], ["GainMuscle", "Ganar músculo"]];
const GOAL_LABEL = Object.fromEntries(GOALS);
const BMI_LABEL = {
  Underweight: "Bajo peso",
  Normal: "Normal",
  Overweight: "Sobrepeso",
  ObesityClassI: "Obesidad grado I",
  ObesityClassII: "Obesidad grado II",
  ObesityClassIII: "Obesidad grado III",
};

export async function render(container) {
  const targetsSlot = el("div");
  let targets = null;
  try {
    targets = await api("/api/me/targets");
  } catch (error) {
    if (error.status !== 404) throw error;
  }
  showTargets(targetsSlot, targets);

  const rate = el("input", { type: "number", name: "weeklyRateKg", min: 0, max: 1, step: "0.05", value: "0.5", inputmode: "decimal" });
  const type = el("select", { name: "goalType" }, options(GOALS, targets?.goalType ?? "LoseFat"));
  const rateField = field("Ritmo semanal (kg)", rate, "Si es más que el máximo seguro, se recorta y se avisa.");
  const syncRate = () => { rateField.hidden = type.value === "Maintain"; };
  type.addEventListener("change", syncRate);
  syncRate();

  const form = el("form", { novalidate: true },
    field("Objetivo", type),
    rateField,
    field("Peso meta (kg)", el("input", { type: "number", name: "targetWeightKg", min: 20, max: 400, step: "0.1", inputmode: "decimal" }),
      "Opcional. No va con Mantener."),
    formError(),
    el("button", { type: "submit", class: "button" }, targets ? "Cambiar objetivo" : "Crear objetivo"));

  onSubmit(form, async (data) => {
    const goalType = data.get("goalType");
    const rateValue = data.get("weeklyRateKg");
    const target = data.get("targetWeightKg");
    const created = await api("/api/me/goals", {
      method: "POST",
      body: {
        goalType,
        weeklyRateKg: goalType === "Maintain" || rateValue === "" ? null : Number(rateValue),
        targetWeightKg: target === "" ? null : Number(target),
      },
    });
    // La respuesta del POST trae el aviso de ritmo recortado (GET /targets ya no lo repite).
    showTargets(targetsSlot, created);
  });

  append(container, [
    el("h1", {}, "Objetivo"),
    el("p", { class: "disclaimer" }, DISCLAIMER),
    targetsSlot,
    el("section", { class: "card" }, el("h2", {}, targets ? "Nuevo objetivo" : "Crear objetivo"), form)]);
}

function showTargets(slot, t) {
  if (!t) {
    setChildren(slot, notice("warning", "Todavía no tenés un objetivo. Necesitás el perfil y al menos un peso registrado."));
    return;
  }
  setChildren(slot, el("section", { class: "card" },
    el("h2", {}, `Tu objetivo: ${GOAL_LABEL[t.goalType] ?? t.goalType}`),
    (t.warnings ?? []).map((w) => notice("warning", w.message)),
    el("div", { class: "stats" },
      stat(num(t.calories, 0), "kcal por día"),
      stat(`${num(t.proteinG)} g`, "proteína"),
      stat(`${num(t.carbsG)} g`, "carbohidratos"),
      stat(`${num(t.fatG)} g`, "grasa")),
    el("ul", { class: "list" },
      el("li", {}, `IMC: ${num(t.bmi)} (${BMI_LABEL[t.bmiCategory] ?? t.bmiCategory})`),
      t.goalType === "Maintain" ? null : el("li", {}, `Ritmo: ${num(t.appliedWeeklyRateKg, 2)} kg por semana`),
      t.targetWeightKg ? el("li", {}, `Peso meta: ${num(t.targetWeightKg)} kg`) : null,
      t.projection
        ? el("li", {}, `Proyección: ${num(t.projection.weeks)} semanas, cerca del ${dateText(t.projection.estimatedDate)}`)
        : null),
    el("p", { class: "disclaimer" }, t.disclaimer || DISCLAIMER)));
}

function stat(value, label) {
  return el("div", { class: "stat" }, el("span", { class: "value" }, value), el("span", { class: "label" }, label));
}
