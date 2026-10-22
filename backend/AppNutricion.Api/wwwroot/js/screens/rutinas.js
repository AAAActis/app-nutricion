// Rutinas y entrenamientos (S7/S12, Santi). Se activa sola cuando existe GET /api/routines: reemplazá este
// archivo por la pantalla real (export async function render(container)).
import { renderPending } from "./proximamente.js";

export function render(container) {
  return renderPending(container, {
    title: "Rutinas",
    endpoint: "/api/routines",
    owner: "Santi (S12)",
    description: "Rutinas precargadas y propias, y el registro de tus entrenamientos.",
  });
}
