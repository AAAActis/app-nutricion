// Presentación de valores que ya devuelve la API. No calcula nutrientes ni objetivos.
import { el } from "./dom.js";
import { num } from "./format.js";
import { icon } from "./icons.js";

export function nutritionFacts(macros, compact = false) {
  return el("dl", { class: "nutrition-facts" + (compact ? " compact" : "") },
    [["calories", "Calorías", "kcal", 0], ["proteinG", "Proteína", "g", 1],
      ["carbsG", "Carbohidratos", "g", 1], ["fatG", "Grasas", "g", 1]]
      .map(([key, label, unit, decimals]) => el("div", { class: "nutrition-fact" },
        el("dt", {}, label),
        el("dd", {}, num(macros?.[key], decimals), el("span", {}, " " + unit)))));
}

export function foodBadge(recipe = false) {
  return el("span", { class: "food-badge", "aria-hidden": "true" }, icon(recipe ? "recetas" : "food"));
}
