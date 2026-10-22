// Mi día (pantalla de Lauti; base en J12). No hay endpoint de resumen diario en develop (L6 pendiente): se suman
// en la página las comidas de GET /api/meals?date=YYYY-MM-DD contra GET /api/me/targets.
// Carga: POST /api/meals { foodId, quantity, isUnits, mealType, date, recipeId }. Edición: PUT /api/meals/{id}
// { quantity, isUnits, mealType, date }. Borrado: DELETE /api/meals/{id}.
// Alimentos: GET /api/foods/search?q&limit, GET /api/foods/favorites, GET /api/foods/recent,
// POST y DELETE /api/foods/{id}/favorite. Los de Open Food Facts traen attribution (ODbL).
import { api } from "../api.js";
import { el, setChildren } from "../dom.js";
import { field, formError, notice, onSubmit, options } from "../forms.js";
import { num, today } from "../format.js";

const MEAL_TYPES = [
  ["Breakfast", "Desayuno"],
  ["Lunch", "Almuerzo"],
  ["AfternoonSnack", "Merienda"],
  ["Dinner", "Cena"],
  ["Snack", "Colación"],
];
const SEARCH_LIMIT = 20;
const SEARCH_DELAY_MS = 300;

export async function render(container) {
  const state = { date: today(await profileTimeZone()), meals: [], targets: null, favorites: new Set() };

  const status = el("div");
  const summary = el("section", { class: "card" });
  const mealsCard = el("section", { class: "card" });
  const dateInput = el("input", { type: "date", name: "date", value: state.date });

  const reload = async () => {
    const [meals, targets] = await Promise.all([api(`/api/meals?date=${encodeURIComponent(state.date)}`), loadTargets()]);
    state.meals = meals;
    state.targets = targets;
    renderSummary(summary, state);
    renderMeals(mealsCard, state, reload, status);
  };

  dateInput.addEventListener("change", async () => {
    if (!dateInput.value) return;
    state.date = dateInput.value;
    status.replaceChildren();
    await reload();
  });

  container.append(
    el("h1", {}, "Mi día"),
    el("div", { class: "row" }, el("label", { for: "dia-fecha" }, "Fecha"), Object.assign(dateInput, { id: "dia-fecha" })),
    status,
    summary,
    mealsCard,
    await foodPicker(state, reload, status));

  await reload();
}

async function profileTimeZone() {
  try {
    return (await api("/api/me/profile")).timeZoneId;
  } catch (error) {
    if (error.status === 404) return undefined;
    throw error;
  }
}

async function loadTargets() {
  try {
    return await api("/api/me/targets");
  } catch (error) {
    if (error.status === 404) return null;
    throw error;
  }
}

// ---------- Resumen del día contra los targets ----------

function renderSummary(card, { meals, targets }) {
  const total = (key) => meals.reduce((sum, m) => sum + Number(m[key]), 0);
  const rows = [
    ["Calorías", total("savedCalories"), targets?.calories, "kcal", 0],
    ["Proteína", total("savedProtein"), targets?.proteinG, "g", 1],
    ["Carbohidratos", total("savedCarbs"), targets?.carbsG, "g", 1],
    ["Grasa", total("savedFat"), targets?.fatG, "g", 1],
  ];
  setChildren(card,
    el("h2", {}, "Resumen"),
    targets ? null : notice("warning", "Sin objetivo activo: solo se ven los totales. ", el("a", { href: "#/objetivo" }, "Crear objetivo")),
    rows.map(([label, value, target, unit, decimals]) => el("div", { class: "field" },
      el("div", { class: "item" },
        el("span", { class: "name" }, label),
        el("span", {}, target
          ? `${num(value, decimals)} de ${num(target, decimals)} ${unit} · ${restText(value, target, unit, decimals)}`
          : `${num(value, decimals)} ${unit}`)),
      target ? el("progress", { max: target, value: Math.min(value, target), class: value > target ? "over" : false }) : null)));
}

function restText(value, target, unit, decimals) {
  const rest = Number(target) - value;
  return rest >= 0 ? `faltan ${num(rest, decimals)} ${unit}` : `pasado por ${num(-rest, decimals)} ${unit}`;
}

// ---------- Comidas del día ----------

function renderMeals(card, state, reload, status) {
  const byType = MEAL_TYPES.map(([type, label]) => [label, state.meals.filter((m) => m.mealType === type)])
    .filter(([, list]) => list.length > 0);
  setChildren(card,
    el("h2", {}, "Comidas"),
    byType.length === 0 ? el("p", { class: "muted" }, "No hay comidas cargadas este día.") : null,
    byType.map(([label, list]) => [
      el("h3", {}, label),
      el("ul", { class: "list" }, list.map((meal) => mealItem(meal, state, reload, status))),
    ]));
}

function mealItem(meal, state, reload, status) {
  const li = el("li");
  const show = () => setChildren(li,
    el("div", { class: "item" },
      el("span", { class: "name" }, meal.itemName),
      el("span", {}, `${num(meal.quantityGrams)} g · ${num(meal.savedCalories, 0)} kcal`),
      el("span", { class: "row" },
        el("button", { type: "button", class: "button secondary small", onclick: edit }, "Editar"),
        el("button", { type: "button", class: "button secondary small", onclick: remove }, "Borrar"))),
    el("div", { class: "muted small" },
      `P ${num(meal.savedProtein)} g · C ${num(meal.savedCarbs)} g · G ${num(meal.savedFat)} g`));

  async function remove() {
    if (!confirm(`¿Borrar "${meal.itemName}"?`)) return;
    try {
      await api(`/api/meals/${meal.id}`, { method: "DELETE" });
      await reload();
    } catch (error) {
      setChildren(status, notice("error", error.title));
    }
  }

  function edit() {
    const form = el("form", { class: "inline", novalidate: true },
      field("Gramos", el("input", { type: "number", name: "quantity", min: 1, step: "1", value: meal.quantityGrams, required: true })),
      field("Comida", el("select", { name: "mealType" }, options(MEAL_TYPES, meal.mealType))),
      formError(),
      el("div", { class: "row" },
        el("button", { type: "submit", class: "button small" }, "Guardar"),
        el("button", { type: "button", class: "button secondary small", onclick: show }, "Cancelar")));
    onSubmit(form, async (data) => {
      const quantity = data.get("quantity");
      await api(`/api/meals/${meal.id}`, {
        method: "PUT",
        body: { quantity: quantity === "" ? null : Number(quantity), isUnits: false, mealType: data.get("mealType"), date: null },
      });
      await reload();
    });
    setChildren(li, el("div", { class: "name" }, meal.itemName), form);
  }

  show();
  return li;
}

// ---------- Buscar y cargar alimentos ----------

async function foodPicker(state, reload, status) {
  try {
    state.favorites = new Set((await api("/api/foods/favorites")).map((f) => f.id));
  } catch {
    state.favorites = new Set();
  }

  const results = el("ul", { class: "list" });
  const chosen = el("div");
  const search = el("input", { type: "search", id: "buscar-alimento", placeholder: "Ej.: arroz, manzana, yogur", autocomplete: "off" });
  const tabs = [["search", "Buscar"], ["favorites", "Favoritos"], ["recent", "Recientes"]];
  let tab = "search";
  let timer = null;
  let lastQuery = 0;

  const tabButtons = tabs.map(([key, label]) => el("button", {
    type: "button",
    role: "tab",
    class: "button secondary small",
    "aria-selected": key === tab ? "true" : "false",
    onclick: () => select(key).catch((error) => setChildren(results, el("li", { class: "form-error" }, error.title))),
  }, label));

  const pick = (food) => setChildren(chosen, addForm(food, state, async () => {
    chosen.replaceChildren();
    setChildren(status, notice("ok", `Cargado: ${food.name}.`));
    await reload();
  }));

  const show = (foods, emptyText) => setChildren(results,
    foods.length === 0 ? el("li", { class: "muted" }, emptyText) : foods.map((food) => foodItem(food, state, pick)));

  async function select(key) {
    tab = key;
    tabButtons.forEach((b, i) => b.setAttribute("aria-selected", tabs[i][0] === key ? "true" : "false"));
    search.hidden = key !== "search";
    if (key === "favorites") show(await api("/api/foods/favorites"), "Todavía no marcaste favoritos.");
    else if (key === "recent") show(await api("/api/foods/recent"), "Todavía no cargaste comidas.");
    else runSearch();
  }

  async function runSearch() {
    const q = search.value.trim();
    if (q.length < 2) {
      show([], "Escribí al menos 2 letras.");
      return;
    }
    const id = ++lastQuery;
    const foods = await api(`/api/foods/search?q=${encodeURIComponent(q)}&limit=${SEARCH_LIMIT}`);
    if (id === lastQuery) show(foods, "No se encontraron alimentos.");
  }

  search.addEventListener("input", () => {
    clearTimeout(timer);
    timer = setTimeout(() => runSearch().catch((error) => setChildren(results, el("li", { class: "form-error" }, error.title))), SEARCH_DELAY_MS);
  });

  show([], "Escribí al menos 2 letras.");
  return el("section", { class: "card" },
    el("h2", {}, "Agregar comida"),
    el("div", { class: "tabs", role: "tablist" }, tabButtons),
    el("label", { for: "buscar-alimento", class: "muted small" }, "Buscar alimento"),
    search,
    results,
    chosen);
}

function foodItem(food, state, pick) {
  const favorite = state.favorites.has(food.id);
  const star = el("button", {
    type: "button",
    class: "button secondary small",
    "aria-pressed": favorite ? "true" : "false",
    title: favorite ? "Quitar de favoritos" : "Agregar a favoritos",
    onclick: async () => {
      const now = star.getAttribute("aria-pressed") !== "true";
      try {
        await api(`/api/foods/${food.id}/favorite`, { method: now ? "POST" : "DELETE" });
      } catch (error) {
        star.title = error.title;
        return;
      }
      if (now) state.favorites.add(food.id); else state.favorites.delete(food.id);
      star.setAttribute("aria-pressed", now ? "true" : "false");
      star.textContent = now ? "★" : "☆";
    },
  }, favorite ? "★" : "☆");

  return el("li", {},
    el("div", { class: "item" },
      el("span", {}, el("span", { class: "name" }, food.name), food.brand ? el("span", { class: "muted" }, ` · ${food.brand}`) : null),
      el("span", { class: "row" },
        star,
        el("button", { type: "button", class: "button small", onclick: () => pick(food) }, "Elegir"))),
    el("div", { class: "muted small" },
      `${num(food.caloriesPer100g, 0)} kcal · P ${num(food.proteinPer100g)} · C ${num(food.carbsPer100g)} · G ${num(food.fatPer100g)} cada 100 g`,
      food.portionName && food.portionWeightGrams ? ` · 1 ${food.portionName} = ${num(food.portionWeightGrams)} g` : ""),
    attribution(food));
}

// Open Food Facts pide atribución (licencia ODbL) en los datos que vienen de ahí.
function attribution(food) {
  if (!food.attribution) return null;
  return el("div", { class: "muted small" },
    "Fuente: ",
    el("a", { href: "https://world.openfoodfacts.org", target: "_blank", rel: "noopener noreferrer" }, food.attribution));
}

function addForm(food, state, onAdded) {
  const units = food.portionName && food.portionWeightGrams
    ? [["grams", "gramos"], ["units", `${food.portionName} (${num(food.portionWeightGrams)} g)`]]
    : [["grams", "gramos"]];
  const form = el("form", { class: "inline", novalidate: true },
    field("Cantidad", el("input", { type: "number", name: "quantity", min: 0.1, step: "0.1", value: 100, required: true, inputmode: "decimal" })),
    field("Unidad", el("select", { name: "unit" }, options(units, "grams"))),
    field("Comida", el("select", { name: "mealType" }, options(MEAL_TYPES, mealTypeNow()))),
    formError(),
    el("button", { type: "submit", class: "button" }, "Agregar"));

  onSubmit(form, async (data) => {
    const quantity = data.get("quantity");
    await api("/api/meals", {
      method: "POST",
      body: {
        foodId: food.id,
        quantity: quantity === "" ? 0 : Number(quantity),
        isUnits: data.get("unit") === "units",
        mealType: data.get("mealType"),
        date: state.date,
        recipeId: null,
      },
    });
    await onAdded();
  });

  return el("div", { class: "card" }, el("h3", {}, food.name), attribution(food), form);
}

// La comida más probable según la hora del navegador.
function mealTypeNow() {
  const hour = new Date().getHours();
  if (hour < 11) return "Breakfast";
  if (hour < 15) return "Lunch";
  if (hour < 19) return "AfternoonSnack";
  if (hour < 23) return "Dinner";
  return "Snack";
}
