// Recetas propias: la API calcula totales y valores por porción desde los alimentos.
// Este módulo solo edita los datos de entrada y representa las respuestas, sin cálculos nutricionales.
import { api } from "../api.js";
import { append, el, setChildren } from "../dom.js";
import { field, formError, notice, onSubmit, options, setNotice } from "../forms.js";
import { MEAL_TYPES, dateText, num, today } from "../format.js";
import { profileTimeZone } from "../profile.js";
import { foodBadge, nutritionFacts } from "../nutrition.js";

const DISCLAIMER = "Estimación, no es consejo médico.";

export async function render(container) {
  const localToday = today(await profileTimeZone());
  const status = el("div", { class: "recipes-status" });
  const collection = el("div", { class: "recipe-list" });
  const detail = el("section", { class: "card recipe-detail", hidden: true });
  const editor = el("section", { class: "card recipe-editor", hidden: true });
  let selected = null;
  let detailRevision = 0;

  async function loadList() {
    const recipes = await api("/api/recipes");
    setChildren(collection, recipes.length === 0
      ? el("section", { class: "card empty-recipes" }, foodBadge(true), el("h2", {}, "Tus recetas, en un solo lugar"),
        el("p", { class: "muted" }, "Todavía no creaste recetas. Combiná alimentos de la búsqueda y guardá las porciones que preparás."))
      : recipes.map((recipe) => el("article", { class: "card recipe-preview" },
        el("div", { class: "food-name" }, foodBadge(true),
          el("div", {}, el("h2", {}, recipe.name),
            el("p", { class: "muted small" }, num(recipe.servings, 0) + " porciones · " + num(recipe.totalGrams) + " g en total"))),
        el("p", { class: "eyebrow" }, "POR PORCIÓN"),
        nutritionFacts(recipe.perServing, true),
        el("button", {
          type: "button", class: "button secondary",
          onclick: () => openRecipe(recipe.id).catch((error) => setNotice(status, "error", error.title)),
        }, "Ver receta"))));
  }

  async function openRecipe(id) {
    const version = ++detailRevision;
    const recipe = await api(`/api/recipes/${id}`);
    if (version !== detailRevision) return;
    selected = recipe;
    editor.hidden = true;
    showRecipe(recipe);
    detail.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function showRecipe(recipe) {
    detail.hidden = false;
    const mealStatus = el("div");
    const mealForm = el("form", { class: "inline", novalidate: true },
      field("Porciones a registrar", el("input", { type: "number", id: "recipe-meal-servings", name: "servings", min: "0.01", max: 20, step: "0.01", value: 1, required: true, inputmode: "decimal" })),
      field("Comida", el("select", { id: "recipe-meal-type", name: "mealType" }, options(MEAL_TYPES, "Lunch"))),
      field("Fecha", el("input", { type: "date", id: "recipe-meal-date", name: "date", value: localToday, required: true })),
      formError(),
      el("button", { type: "submit", class: "button" }, "Registrar comida"));
    onSubmit(mealForm, async (data) => {
      await api("/api/meals/recipe", {
        method: "POST",
        body: { recipeId: recipe.id, servings: Number(data.get("servings")), mealType: data.get("mealType"), date: data.get("date") },
      });
      setChildren(mealStatus, notice("ok", "Receta registrada el " + dateText(data.get("date")) + ". ", el("a", { href: "#/dia" }, "Ver Mi día")));
    });
    setChildren(detail,
      el("div", { class: "item" }, el("div", { class: "food-name" }, foodBadge(true), el("h2", {}, recipe.name)),
        el("div", { class: "row" },
          el("button", { type: "button", class: "button secondary small", onclick: () => editRecipe(recipe) }, "Editar receta"),
          el("button", { type: "button", class: "button secondary small", onclick: () => removeRecipe(recipe) }, "Borrar receta"))),
      el("p", { class: "muted small" }, num(recipe.servings, 0) + " porciones · " + num(recipe.totalGrams) + " g en total"),
      el("h3", {}, "Por porción"), nutritionFacts(recipe.perServing),
      el("h3", {}, "Receta completa"), nutritionFacts(recipe.totals, true),
      el("h3", {}, "Ingredientes"),
      el("ul", { class: "list" }, recipe.ingredients.map((ingredient) => el("li", {},
        el("div", { class: "item" }, el("span", { class: "name" }, ingredient.foodName),
          el("span", {}, num(ingredient.quantityGrams) + " g")),
        nutritionFacts(ingredient, true)))),
      el("p", { class: "muted small disclaimer" }, DISCLAIMER),
      el("h3", {}, "Agregar a mis comidas"), mealForm, mealStatus);
  }

  async function removeRecipe(recipe) {
    if (!confirm('¿Borrar la receta "' + recipe.name + '"?')) return;
    try {
      await api(`/api/recipes/${recipe.id}`, { method: "DELETE" });
      ++detailRevision;
      selected = null;
      detail.hidden = true;
      editor.hidden = true;
      await loadList();
      setNotice(status, "ok", "Receta borrada.");
    } catch (error) { setNotice(status, "error", error.title); }
  }

  function editRecipe(recipe = null) {
    ++detailRevision;
    editor.hidden = false;
    detail.hidden = true;
    let ingredients = (recipe?.ingredients ?? []).map((i) => ({ foodId: i.foodId, foodName: i.foodName, quantityGrams: String(i.quantityGrams) }));
    const ingredientsSlot = el("div", { class: "ingredient-rows" });
    const results = el("ul", { class: "list recipe-search-results", "aria-live": "polite" });
    const searchStatus = el("div");
    const name = el("input", { type: "text", id: "recipe-name", name: "name", maxlength: 120, required: true, value: recipe?.name ?? "" });
    const servings = el("input", { type: "number", id: "recipe-servings", name: "servings", min: 1, max: 50, step: 1, required: true, value: recipe?.servings ?? 1 });
    const form = el("form", { novalidate: true },
      field("Nombre de la receta", name), field("Porciones que rinde", servings),
      el("h3", {}, "Ingredientes"), ingredientsSlot, formError(),
      el("div", { class: "row" },
        el("button", { type: "submit", class: "button" }, "Guardar receta"),
        el("button", { type: "button", class: "button secondary", onclick: () => {
          editor.hidden = true;
          if (selected) showRecipe(selected);
        } }, "Cancelar")));

    function renderIngredients() {
      setChildren(ingredientsSlot, ingredients.length === 0 ? el("p", { class: "muted small" }, "Buscá alimentos abajo y agregalos como ingredientes.") : null,
        ingredients.map((ingredient, i) => {
          const quantity = el("input", {
            type: "number", id: "recipe-ingredient-" + i + "-quantity", name: `ingredients[${i}].quantityGrams`, min: "0.01", max: 5000, step: "0.01",
            value: ingredient.quantityGrams, inputmode: "decimal",
            oninput: (event) => { ingredient.quantityGrams = event.target.value; },
          });
          return el("div", { class: "ingredient-row" },
            el("div", { class: "field" }, el("span", { class: "name" }, ingredient.foodName),
              el("input", { type: "hidden", name: `ingredients[${i}].foodId`, value: ingredient.foodId }),
              el("div", { class: "field-error", "data-error-for": `ingredients[${i}].foodId`, "aria-live": "polite" })),
            field("Cantidad (g)", quantity),
            el("button", { type: "button", class: "button secondary small", "aria-label": "Quitar " + ingredient.foodName, onclick: () => {
              ingredients.splice(i, 1);
              renderIngredients();
            } }, "Quitar"));
        }));
    }
    onSubmit(form, async (data) => {
      const saved = await api(recipe ? `/api/recipes/${recipe.id}` : "/api/recipes", {
        method: recipe ? "PUT" : "POST",
        body: {
          name: data.get("name"), servings: Number(data.get("servings")),
          ingredients: ingredients.map((i) => ({ foodId: i.foodId, quantityGrams: Number(i.quantityGrams) })),
        },
      });
      selected = saved;
      editor.hidden = true;
      showRecipe(saved);
      await loadList();
      setNotice(status, "ok", "Receta guardada.");
    });

    const searchInput = el("input", { type: "search", id: "recipe-food-query", name: "q", autocomplete: "off", placeholder: "Ej.: arroz, manzana, yogur" });
    const searchForm = el("form", { class: "inline", novalidate: true },
      field("Buscar alimento para la receta", searchInput), formError(),
      el("button", { type: "submit", class: "button secondary" }, "Buscar"));
    onSubmit(searchForm, async () => {
      searchStatus.replaceChildren();
      results.replaceChildren();
      const query = searchInput.value.trim();
      if (query.length < 2) {
        setNotice(searchStatus, "warning", "Escribí al menos 2 letras.");
        return;
      }
      const foods = await api(`/api/foods/search?q=${encodeURIComponent(query)}&limit=20`);
      setChildren(results, foods.length === 0 ? el("li", { class: "muted" }, "No se encontraron alimentos.") :
        foods.map((food) => el("li", {},
          el("div", { class: "item" }, el("div", { class: "food-name" }, foodBadge(), el("span", { class: "name" }, food.name)),
            el("button", { type: "button", class: "button secondary small", onclick: () => {
              ingredients.push({ foodId: food.id, foodName: food.name, quantityGrams: "100" });
              renderIngredients();
            } }, "Agregar ingrediente")),
          food.brand ? el("div", { class: "muted small" }, food.brand) : null,
          el("p", { class: "muted small" }, num(food.caloriesPer100g, 0) + " kcal por 100 g"),
          food.attribution ? el("p", { class: "muted small" }, "Fuente: ",
            el("a", { href: "https://world.openfoodfacts.org", target: "_blank", rel: "noopener noreferrer" }, food.attribution)) : null)));
    });
    setChildren(editor, el("h2", {}, recipe ? "Editar receta" : "Nueva receta"),
      el("p", { class: "muted small" }, "Los valores nutricionales se muestran después de guardar la receta."),
      form, el("hr"), searchForm, searchStatus, results);
    renderIngredients();
    editor.scrollIntoView({ behavior: "smooth", block: "start" });
    name.focus({ preventScroll: true });
  }

  append(container, [
    el("h1", {}, "Recetas"),
    el("div", { class: "recipes-toolbar item" },
      el("p", { class: "muted" }, "Tus ingredientes, tus porciones, tus recetas."),
      el("button", { type: "button", class: "button", onclick: () => editRecipe() }, "Nueva receta")),
    status, editor, collection, detail,
  ]);
  await loadList();
}
