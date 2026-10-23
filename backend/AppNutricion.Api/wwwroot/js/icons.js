// Íconos decorativos locales. Los textos de cada enlace conservan su nombre accesible.
import { svg } from "./dom.js";

const paths = {
  alimentos: ["M12 5c-4-4-11 0-8 8 1 4 3 8 6 7 1-1 3-1 4 0 3 1 5-3 6-7 3-8-4-12-8-8Z", "M12 5c0-3 2-4 4-4"],
  recetas: ["M5 3h14v18H5Z", "M8 3v18m4-13h4m-4 4h4m-4 4h3"],
  food: ["M3 11h18c0 5-4 9-9 9s-9-4-9-9Zm3 9h12", "M8 3v4m4-5v5m4-4v4"],
  dia: ["M4 3v6m3-6v6M4 6h3M5.5 9v12", "M16 3c-3 3-3 8 0 9h3V3h-3Zm2 9v9"],
  peso: ["m3 17 6-6 4 3 8-10", "M3 3v18h18"],
  objetivo: ["M21 12a9 9 0 1 1-9-9", "M17 12a5 5 0 1 1-5-5", "m12 12 9-9m-5 0h5v5"],
  perfil: ["M20 21v-2a6 6 0 0 0-6-6h-4a6 6 0 0 0-6 6v2", "M16 5a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z"],
  rutinas: ["m6 4-2 2 14 14 2-2L6 4ZM3 9l6-6m6 18 6-6M2 6l4-4m12 20 4-4"],
  chat: ["M21 11a8 8 0 0 1-8 8H7l-5 3 2-6a8 8 0 1 1 17-5Z", "M8 10h8m-8 4h5"],
  cuenta: ["M4 4h16v16H4Z", "M15 9a3 3 0 1 1-6 0 3 3 0 0 1 6 0ZM7 19v-1a5 5 0 0 1 10 0v1"],
  salir: ["M9 4H4v16h5m6-4 5-4-5-4m-6 4h11"],
};

export function icon(name) {
  return svg("svg", {
    class: "nav-icon", viewBox: "0 0 24 24", fill: "none", stroke: "currentColor",
    "stroke-width": 1.6, "stroke-linecap": "round", "stroke-linejoin": "round",
    "aria-hidden": "true", focusable: "false",
  }, (paths[name] ?? paths.dia).map((d) => svg("path", { d })));
}
