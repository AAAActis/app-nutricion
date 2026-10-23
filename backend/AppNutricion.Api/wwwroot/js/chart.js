// Gráfico de peso en SVG, sin librerías: la línea de los registros y la tendencia (promedio de los últimos 7
// registros en cada punto, la misma ventana que usa la API para la tendencia 7 contra 7).
import { svg } from "./dom.js";
import { dateText, num } from "./format.js";

const WIDTH = 640;
const HEIGHT = 260;
const PAD = { top: 16, right: 16, bottom: 32, left: 48 };
const TREND_WINDOW = 7;

// metrics: [{ date: "YYYY-MM-DD", weightKg }] ordenado por fecha. showTrend: la API ya calculó la tendencia
// (14 registros); con menos, la línea de tendencia no se dibuja.
export function weightChart(metrics, showTrend = false) {
  const weights = metrics.map((m) => Number(m.weightKg));
  const min = Math.floor(Math.min(...weights) - 1);
  const max = Math.ceil(Math.max(...weights) + 1);
  const days = metrics.map((m) => Date.parse(`${m.date}T00:00:00Z`));
  const first = days[0];
  const span = Math.max(days[days.length - 1] - first, 1);

  const x = (i) => metrics.length === 1
    ? PAD.left + (WIDTH - PAD.left - PAD.right) / 2
    : PAD.left + ((days[i] - first) / span) * (WIDTH - PAD.left - PAD.right);
  const y = (kg) => PAD.top + ((max - kg) / (max - min)) * (HEIGHT - PAD.top - PAD.bottom);

  const points = weights.map((kg, i) => `${x(i).toFixed(1)},${y(kg).toFixed(1)}`).join(" ");
  const trend = weights.map((_, i) => {
    const window = weights.slice(Math.max(0, i - TREND_WINDOW + 1), i + 1);
    const avg = window.reduce((a, b) => a + b, 0) / window.length;
    return `${x(i).toFixed(1)},${y(avg).toFixed(1)}`;
  }).join(" ");

  const last = metrics.length - 1;
  return svg("svg", {
    class: "chart",
    viewBox: `0 0 ${WIDTH} ${HEIGHT}`,
    role: "img",
    "aria-label": `Peso entre ${dateText(metrics[0].date)} y ${dateText(metrics[last].date)}: de ${num(weights[0])} a ${num(weights[last])} kg`,
  },
    svg("line", { class: "axis", x1: PAD.left, y1: HEIGHT - PAD.bottom, x2: WIDTH - PAD.right, y2: HEIGHT - PAD.bottom }),
    svg("line", { class: "axis", x1: PAD.left, y1: PAD.top, x2: PAD.left, y2: HEIGHT - PAD.bottom }),
    svg("text", { x: PAD.left - 6, y: y(max) + 4, "text-anchor": "end" }, `${max} kg`),
    svg("text", { x: PAD.left - 6, y: y(min) + 4, "text-anchor": "end" }, `${min} kg`),
    svg("text", { x: PAD.left, y: HEIGHT - 10 }, dateText(metrics[0].date)),
    svg("text", { x: WIDTH - PAD.right, y: HEIGHT - 10, "text-anchor": "end" }, dateText(metrics[last].date)),
    showTrend && metrics.length > 1 ? svg("polyline", { class: "trend", points: trend }) : null,
    metrics.length > 1 ? svg("polyline", { class: "line", points }) : null,
    weights.map((kg, i) => svg("circle", { class: "point", cx: x(i), cy: y(kg), r: 3.5 },
      svg("title", {}, `${dateText(metrics[i].date)}: ${num(kg)} kg`))));
}
