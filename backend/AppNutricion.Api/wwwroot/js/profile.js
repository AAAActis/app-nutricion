// La zona horaria del perfil, para que "hoy" sea el día del diario (DateOnly en la zona del perfil).
// Sin perfil todavía (404) → undefined, y format.today usa la del navegador.
import { api } from "./api.js";

export async function profileTimeZone() {
  try {
    return (await api("/api/me/profile")).timeZoneId;
  } catch (error) {
    if (error.status === 404) return undefined;
    throw error;
  }
}
