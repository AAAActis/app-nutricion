// Peso: POST /api/me/metrics { weightKg, date? } (201 si crea, 200 si corrige el día) y
// GET /api/me/metrics → { metrics: [{ date, weightKg }], trend: { lastAverageKg, previousAverageKg, changeKg } | null }.
import { api } from "../api.js";
import { weightChart } from "../chart.js";
import { append, el, setChildren } from "../dom.js";
import { field, formError, notice, onSubmit } from "../forms.js";
import { dateText, num } from "../format.js";

export async function render(container) {
  const history = el("div");
  const status = el("div");

  const form = el("form", { class: "inline", novalidate: true },
    field("Peso (kg)", el("input", { type: "number", name: "weightKg", min: 20, max: 400, step: "0.1", required: true, inputmode: "decimal" })),
    field("Fecha", el("input", { type: "date", name: "date" }), "Vacía = hoy."),
    formError(),
    el("button", { type: "submit", class: "button" }, "Registrar"));

  onSubmit(form, async (data) => {
    const weight = data.get("weightKg");
    await api("/api/me/metrics", {
      method: "POST",
      body: { weightKg: weight === "" ? null : Number(weight), date: data.get("date") || null },
    });
    form.reset();
    setChildren(status, notice("ok", "Peso registrado."));
    await loadHistory(history);
  });

  append(container, [
    el("h1", {}, "Peso"),
    status,
    el("section", { class: "card" }, form),
    history]);

  await loadHistory(history);
}

async function loadHistory(slot) {
  let data;
  try {
    data = await api("/api/me/metrics");
  } catch (error) {
    if (error.status === 409) {
      setChildren(slot, notice("warning", error.title, " ", el("a", { href: "#/perfil" }, "Ir al perfil")));
      return;
    }
    throw error;
  }

  const metrics = data.metrics ?? [];
  if (metrics.length === 0) {
    setChildren(slot, el("p", { class: "muted" }, "Todavía no registraste tu peso."));
    return;
  }

  setChildren(slot,
    el("section", { class: "card" },
      el("h2", {}, "Historial"),
      weightChart(metrics, Boolean(data.trend)),
      el("div", { class: "legend" },
        el("span", { class: "swatch-line" }, "— peso"),
        data.trend ? el("span", { class: "swatch-trend" }, "- - tendencia (promedio de 7 registros)") : null),
      trendText(data.trend)),
    el("section", { class: "card" },
      el("h2", {}, "Registros"),
      el("table", {},
        el("thead", {}, el("tr", {}, el("th", {}, "Fecha"), el("th", {}, "Peso (kg)"))),
        el("tbody", {}, [...metrics].reverse().slice(0, 30).map((m) =>
          el("tr", {}, el("td", {}, dateText(m.date)), el("td", {}, num(m.weightKg))))))));
}

function trendText(trend) {
  if (!trend) return el("p", { class: "muted small" }, "La tendencia aparece con 14 registros (últimos 7 contra los 7 anteriores).");
  const change = Number(trend.changeKg);
  const direction = change < 0 ? "bajaste" : change > 0 ? "subiste" : "te mantuviste";
  return el("p", {},
    `Tendencia: ${direction} ${num(Math.abs(change), 2)} kg `,
    `(promedio de los últimos 7: ${num(trend.lastAverageKg, 2)} kg; de los 7 anteriores: ${num(trend.previousAverageKg, 2)} kg).`);
}
