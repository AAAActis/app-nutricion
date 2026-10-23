// Chat con el asistente (S9/S12, Santi). Se activa sola cuando existe /api/assistant/chat (un GET a una ruta
// solo POST da 405, no 404): reemplazá este archivo por la pantalla real (export async function render(container)).
import { renderPending } from "./proximamente.js";

export function render(container) {
  return renderPending(container, {
    title: "Asistente",
    endpoint: "/api/assistant/chat",
    owner: "Santi (S12)",
    description: "Un chat que responde con tus datos y te propone registrar comidas. Lo que cargues sale siempre de la tabla de alimentos, nunca del asistente.",
  });
}
