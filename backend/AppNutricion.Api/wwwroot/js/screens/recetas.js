// Recetas (L12, sobre la API de L9). GET /api/recipes y GET /api/recipes/{id} → { id, name, servings, totalGrams,
// totals, perServing: { calories, proteinG, carbsG, fatG }, ingredients: [{ foodId, foodName, quantityGrams, calories,
// proteinG, carbsG, fatG }] } (calculado desde Foods en cada pedido). Alta: POST /api/recipes { name, servings,
// ingredients: [{ foodId, quantityGrams }] }; edición: PUT /api/recipes/{id}; borrado: DELETE /api/recipes/{id}.
// Cargar al día: POST /api/meals/recipe { recipeId, servings, mealType, date }. Buscador: GET /api/foods/search (con el
// token trae también los alimentos propios).
import { api } from "../api.js";
import { el, setChildren } from "../dom.js";
import { field, formError, notice, onSubmit, options, setNotice } from "../forms.js";
import { MEAL_TYPES, num, today } from "../format.js";
import { profileTimeZone } from "../profile.js";

const MAX_INGREDIENTS = 30;
const SEARCH_LIMIT = 10;
const SEARCH_DELAY_MS = 300;
const DEFAULT_GRAMS = 100;
// El mismo texto que el resumen de Mi día (NutritionDefaults.Disclaimer); la receta no lo trae.
const DISCLAIMER = "Estimación, no es consejo médico.";

export async function render(container) {
  const state = { today: today(await profileTimeZone()) };
  const status = el("div");
  const list = el("section", { class: "card" });
  const detail = el("section", { class: "card", hidden: true });
  const editor = el("section", { class: "card" });

  const reload = async () => renderList(list, await api("/api/recipes"), { open, edit });

  async function open(id) {
    detail.hidden = false;
    setChildren(detail, el("p", { class: "muted" }, "Cargando…"));
    try {
      renderDetail(detail, await api(`/api/recipes/${id}`), state, { edit, remove });
    } catch (error) {
      setChildren(detail, notice("error", error.title));
    }
    detail.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function edit(recipe) {
    renderEditor(editor, recipe, async (saved) => {
      setNotice(status, "ok", `Guardada: ${saved.name}.`);
      edit(null);
      await reload();
      await open(saved.id);
    });
    if (recipe) editor.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  async function remove(recipe) {
    if (!confirm(`¿Borrar la receta "${recipe.name}"?`)) return;
    try {
      await api(`/api/recipes/${recipe.id}`, { method: "DELETE" });
      detail.hidden = true;
      setNotice(status, "ok", `Borrada: ${recipe.name}.`);
      await reload();
    } catch (error) {
      setNotice(status, "error", error.title);
    }
  }

  container.append(el("h1", {}, "Recetas"), status, list, detail, editor);
  edit(null);
  await reload();
}

// ---------- Lista ----------

function renderList(card, recipes, { open }) {
  setChildren(card,
    el("h2", {}, "Mis recetas"),
    recipes.length === 0 ? el("p", { class: "muted" }, "Todavía no creaste recetas.") : null,
    el("ul", { class: "list" }, recipes.map((recipe) => el("li", {},
      el("div", { class: "item" },
        el("span", { class: "name" }, recipe.name),
        el("span", {}, `${num(recipe.perServing.calories, 0)} kcal por porción · ${recipe.servings} ${recipe.servings === 1 ? "porción" : "porciones"}`),
        el("button", { type: "button", class: "button secondary small", onclick: () => open(recipe.id) }, "Ver"))))));
}

// ---------- Detalle y cargar al día ----------

function renderDetail(card, recipe, state, { edit, remove }) {
  const macroCells = (m) => [
    el("td", {}, num(m.calories, 0)),
    el("td", {}, `${num(m.proteinG)} / ${num(m.carbsG)} / ${num(m.fatG)}`),
  ];
  setChildren(card,
    el("div", { class: "item" },
      el("h2", {}, recipe.name),
      el("span", { class: "row" },
        el("button", { type: "button", class: "button secondary small", onclick: () => edit(recipe) }, "Editar"),
        el("button", { type: "button", class: "button secondary small", onclick: () => remove(recipe) }, "Borrar"))),
    el("p", { class: "muted small" }, `${recipe.servings} ${recipe.servings === 1 ? "porción" : "porciones"} · ${num(recipe.totalGrams)} g en total`),
    el("table", {},
      el("thead", {}, el("tr", {}, el("th", {}, "Ingrediente"), el("th", {}, "g"), el("th", {}, "kcal"), el("th", {}, "P / C / G (g)"))),
      el("tbody", {}, recipe.ingredients.map((i) => el("tr", {},
        el("td", {}, i.foodName), el("td", {}, num(i.quantityGrams)), macroCells(i)))),
      el("tfoot", {},
        el("tr", {}, el("th", {}, "Total"), el("td", {}, num(recipe.totalGrams)), macroCells(recipe.totals)),
        el("tr", {}, el("th", {}, "Por porción"), el("td", {}, num(recipe.totalGrams / recipe.servings)), macroCells(recipe.perServing)))),
    el("p", { class: "muted small disclaimer" }, DISCLAIMER),
    loadForm(recipe, state));
}

function loadForm(recipe, state) {
  const result = el("div");
  const form = el("form", { class: "inline", novalidate: true },
    field("Porciones", el("input", { type: "number", name: "servings", min: 0.5, max: 20, step: "0.5", value: 1, required: true, inputmode: "decimal" })),
    field("Comida", el("select", { name: "mealType" }, options(MEAL_TYPES, "Lunch"))),
    field("Fecha", el("input", { type: "date", name: "date", value: state.today, required: true })),
    formError(),
    el("button", { type: "submit", class: "button" }, "Cargar al día"));

  onSubmit(form, async (data) => {
    const servings = data.get("servings");
    const date = String(data.get("date"));
    await api("/api/meals/recipe", {
      method: "POST",
      body: { recipeId: recipe.id, servings: servings === "" ? 0 : Number(servings), mealType: data.get("mealType"), date: date || null },
    });
    setNotice(result, "ok", `Cargada: ${num(servings)} ${Number(servings) === 1 ? "porción" : "porciones"} de ${recipe.name}. `,
      el("a", { href: "#/dia" }, "Ver en Mi día"));
  });

  return el("div", {}, el("h3", {}, "Cargar al día"), result, form);
}

// ---------- Crear y editar ----------

// Los ingredientes viven en "rows" y la lista se vuelve a dibujar en cada cambio (como las series de Rutinas): así
// los nombres de los campos ("ingredients[1].quantityGrams") coinciden con el índice de los errores de la API.
function renderEditor(card, recipe, onSaved) {
  let rows = recipe
    ? recipe.ingredients.map((i) => ({ food: { id: i.foodId, name: i.foodName }, grams: i.quantityGrams }))
    : [{ food: null, grams: DEFAULT_GRAMS }];
  const rowsList = el("div");
  const addButton = el("button", {
    type: "button",
    class: "button secondary small",
    onclick: () => {
      rows.push({ food: null, grams: DEFAULT_GRAMS });
      renderRows();
    },
  }, "Agregar ingrediente");

  const renderRows = () => {
    addButton.disabled = rows.length >= MAX_INGREDIENTS;
    setChildren(rowsList,
      rows.length === 0 ? el("p", { class: "muted" }, "Agregá al menos un ingrediente.") : null,
      rows.map((row, i) => ingredientRow(row, i, renderRows, () => {
        rows.splice(i, 1);
        renderRows();
      })),
      rows.length >= MAX_INGREDIENTS ? el("p", { class: "muted small" }, `Máximo ${MAX_INGREDIENTS} ingredientes.`) : null);
  };

  const form = el("form", { novalidate: true },
    field("Nombre", el("input", { type: "text", name: "name", maxlength: 120, value: recipe?.name ?? "", required: true })),
    field("Porciones que rinde", el("input", { type: "number", name: "servings", min: 1, max: 50, step: "1", value: recipe?.servings ?? 1, required: true, inputmode: "numeric" })),
    el("h3", {}, "Ingredientes"),
    rowsList,
    el("div", { class: "field-error", "data-error-for": "ingredients", "aria-live": "polite" }),
    addButton,
    formError(),
    el("div", { class: "row" },
      el("button", { type: "submit", class: "button" }, recipe ? "Guardar cambios" : "Crear receta"),
      recipe ? el("button", { type: "button", class: "button secondary", onclick: () => renderEditor(card, null, onSaved) }, "Cancelar") : null));

  onSubmit(form, async (data) => {
    const servings = data.get("servings");
    const saved = await api(recipe ? `/api/recipes/${recipe.id}` : "/api/recipes", {
      method: recipe ? "PUT" : "POST",
      body: {
        name: String(data.get("name") ?? "").trim() || null,
        servings: servings === "" ? 0 : Number(servings),
        // Sin alimento elegido va 0: la API lo rechaza con el error en la fila.
        ingredients: rows.map((row) => ({ foodId: row.food?.id ?? 0, quantityGrams: row.grams === "" ? 0 : Number(row.grams) })),
      },
    });
    await onSaved(saved);
  });

  renderRows();
  setChildren(card, el("h2", {}, recipe ? `Editar: ${recipe.name}` : "Nueva receta"), form);
}

function ingredientRow(row, i, renderRows, removeRow) {
  const grams = el("input", { type: "number", name: `ingredients[${i}].quantityGrams`, min: 0.1, max: 5000, step: "0.1", value: row.grams, inputmode: "decimal" });
  grams.addEventListener("input", () => { row.grams = grams.value; });

  return el("fieldset", { class: "set-row" },
    el("legend", {}, `Ingrediente ${i + 1}`),
    row.food
      ? el("div", { class: "item" },
        el("span", { class: "name" }, row.food.name),
        el("button", {
          type: "button",
          class: "button secondary small",
          onclick: () => {
            row.food = null;
            renderRows();
          },
        }, "Cambiar"))
      : foodSearch(i, (food) => {
        row.food = food;
        renderRows();
      }),
    el("div", { class: "field-error", "data-error-for": `ingredients[${i}].foodId`, "aria-live": "polite" }),
    field("Gramos", grams),
    el("button", { type: "button", class: "button secondary small", onclick: removeRow }, "Quitar ingrediente"));
}

function foodSearch(i, pick) {
  const results = el("ul", { class: "list" });
  const search = el("input", { type: "search", id: `receta-buscar-${i}`, placeholder: "Ej.: arroz, harina, huevo", autocomplete: "off" });
  let timer = null;
  let lastQuery = 0;

  const runSearch = async () => {
    const q = search.value.trim();
    if (q.length < 2) {
      results.replaceChildren();
      return;
    }
    const id = ++lastQuery;
    const foods = await api(`/api/foods/search?q=${encodeURIComponent(q)}&limit=${SEARCH_LIMIT}`);
    if (id !== lastQuery) return;
    setChildren(results, foods.length === 0
      ? el("li", { class: "muted" }, "No se encontraron alimentos.")
      : foods.map((food) => el("li", {},
        el("div", { class: "item" },
          el("span", {}, el("span", { class: "name" }, food.name), food.brand ? el("span", { class: "muted" }, ` · ${food.brand}`) : null),
          el("button", { type: "button", class: "button small", onclick: () => pick(food) }, "Elegir")),
        el("div", { class: "muted small" }, `${num(food.caloriesPer100g, 0)} kcal cada 100 g`))));
  };

  search.addEventListener("input", () => {
    clearTimeout(timer);
    timer = setTimeout(() => runSearch().catch((error) => setChildren(results, el("li", { class: "form-error" }, error.title))), SEARCH_DELAY_MS);
  });
  // Enter en el buscador no envía la receta.
  search.addEventListener("keydown", (event) => { if (event.key === "Enter") event.preventDefault(); });

  return el("div", {}, el("label", { for: search.id, class: "muted small" }, "Buscar alimento"), search, results);
}
