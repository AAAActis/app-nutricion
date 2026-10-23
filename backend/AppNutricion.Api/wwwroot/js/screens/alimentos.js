// Mis alimentos (L12, sobre la API de L8). GET /api/foods/custom → [{ id, name, brand, caloriesPer100g, proteinPer100g,
// carbsPer100g, fatPer100g, portionName, portionWeightGrams }]. Alta: POST /api/foods/custom; edición: PUT
// /api/foods/custom/{id} (mismo cuerpo); borrado: DELETE /api/foods/custom/{id} → 409 si está en una receta.
import { api } from "../api.js";
import { el, setChildren } from "../dom.js";
import { field, formError, onSubmit, setNotice } from "../forms.js";
import { num } from "../format.js";

export async function render(container) {
  const status = el("div");
  const list = el("section", { class: "card" });
  const editor = el("section", { class: "card" });

  const reload = async () => renderList(list, await api("/api/foods/custom"), { edit, remove });

  function edit(food) {
    renderForm(editor, food, async (saved) => {
      setNotice(status, "ok", `Guardado: ${saved.name}.`);
      edit(null);
      await reload();
    });
    if (food) editor.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  async function remove(food) {
    if (!confirm(`¿Borrar "${food.name}"?`)) return;
    try {
      await api(`/api/foods/custom/${food.id}`, { method: "DELETE" });
      setNotice(status, "ok", `Borrado: ${food.name}.`);
      await reload();
    } catch (error) {
      // 409: el alimento está en una receta. Es un aviso (hay que sacarlo de la receta), no una falla.
      setNotice(status, error.status === 409 ? "warning" : "error", error.title);
    }
  }

  container.append(
    el("h1", {}, "Mis alimentos"),
    el("p", { class: "muted" }, "Solo vos ves estos alimentos. Aparecen en el buscador de Mi día y en tus recetas."),
    status,
    list,
    editor);

  edit(null);
  await reload();
}

function renderList(card, foods, { edit, remove }) {
  setChildren(card,
    el("h2", {}, "Mis alimentos"),
    foods.length === 0 ? el("p", { class: "muted" }, "Todavía no creaste alimentos.") : null,
    el("ul", { class: "list" }, foods.map((food) => el("li", {},
      el("div", { class: "item" },
        el("span", {}, el("span", { class: "name" }, food.name), food.brand ? el("span", { class: "muted" }, ` · ${food.brand}`) : null),
        el("span", { class: "row" },
          el("button", { type: "button", class: "button secondary small", onclick: () => edit(food) }, "Editar"),
          el("button", { type: "button", class: "button secondary small", onclick: () => remove(food) }, "Borrar"))),
      el("div", { class: "muted small" },
        `${num(food.caloriesPer100g, 0)} kcal · P ${num(food.proteinPer100g)} · C ${num(food.carbsPer100g)} · G ${num(food.fatPer100g)} cada 100 g`,
        food.portionName && food.portionWeightGrams ? ` · 1 ${food.portionName} = ${num(food.portionWeightGrams)} g` : "")))));
}

// food null = alta; si no, edición de ese alimento.
function renderForm(card, food, onSaved) {
  const number = (name, label, max) => field(label, el("input", {
    type: "number", name, min: 0, max, step: "0.1", inputmode: "decimal", value: food?.[name] ?? "", required: true,
  }));
  const form = el("form", { novalidate: true },
    field("Nombre", el("input", { name: "name", maxlength: 120, value: food?.name ?? "", required: true })),
    field("Marca (opcional)", el("input", { name: "brand", maxlength: 120, value: food?.brand ?? "" })),
    number("caloriesPer100g", "kcal cada 100 g", 900),
    number("proteinPer100g", "Proteína (g cada 100 g)", 100),
    number("carbsPer100g", "Carbohidratos (g cada 100 g)", 100),
    number("fatPer100g", "Grasa (g cada 100 g)", 100),
    field("Porción (opcional)", el("input", { name: "portionName", maxlength: 60, value: food?.portionName ?? "", placeholder: "Ej.: porción, unidad, feta" })),
    field("Gramos de la porción", el("input", { type: "number", name: "portionWeightGrams", min: 0.1, step: "0.1", inputmode: "decimal", value: food?.portionWeightGrams ?? "" })),
    formError(),
    el("div", { class: "row" },
      el("button", { type: "submit", class: "button" }, food ? "Guardar cambios" : "Crear alimento"),
      food ? el("button", { type: "button", class: "button secondary", onclick: () => renderForm(card, null, onSaved) }, "Cancelar") : null));

  onSubmit(form, async (data) => {
    const text = (name) => String(data.get(name) ?? "").trim() || null;
    const decimal = (name) => (data.get(name) === "" ? null : Number(data.get(name)));
    const saved = await api(food ? `/api/foods/custom/${food.id}` : "/api/foods/custom", {
      method: food ? "PUT" : "POST",
      body: {
        name: text("name"),
        brand: text("brand"),
        caloriesPer100g: decimal("caloriesPer100g"),
        proteinPer100g: decimal("proteinPer100g"),
        carbsPer100g: decimal("carbsPer100g"),
        fatPer100g: decimal("fatPer100g"),
        portionName: text("portionName"),
        portionWeightGrams: decimal("portionWeightGrams"),
      },
    });
    await onSaved(saved);
  });

  setChildren(card, el("h2", {}, food ? `Editar: ${food.name}` : "Nuevo alimento"), form);
}
