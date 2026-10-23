// Formatos compartidos: números en es-AR, fechas "YYYY-MM-DD" (como las manda la API) y "hoy" en una zona.

const numberFormats = new Map();

export function num(value, decimals = 1) {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return "—";
  if (!numberFormats.has(decimals))
    numberFormats.set(decimals, new Intl.NumberFormat("es-AR", { maximumFractionDigits: decimals, minimumFractionDigits: 0 }));
  return numberFormats.get(decimals).format(Number(value));
}

// "2026-10-21" → "21/10/2026". Sin pasar por Date para no correr el día por la zona horaria.
export function dateText(iso) {
  if (!iso) return "—";
  const [y, m, d] = iso.split("-");
  return `${d}/${m}/${y}`;
}

// Hoy como "YYYY-MM-DD" en la zona horaria pedida (la del perfil); sin zona, la del navegador.
export function today(timeZone) {
  try {
    return new Intl.DateTimeFormat("en-CA", { timeZone: timeZone || undefined, year: "numeric", month: "2-digit", day: "2-digit" })
      .format(new Date());
  } catch {
    return new Intl.DateTimeFormat("en-CA", { year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date());
  }
}

// "YYYY-MM-DD" más n días (n puede ser negativo), en UTC para que no lo mueva la zona horaria.
export function addDays(iso, days) {
  const date = new Date(`${iso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

// Un DateTime UTC de la API ("2026-10-22T14:05:00Z") como fecha y hora en la zona pedida.
export function dateTimeText(isoUtc, timeZone) {
  if (!isoUtc) return "—";
  const options = { dateStyle: "short", timeStyle: "short", timeZone: timeZone || undefined };
  try {
    return new Intl.DateTimeFormat("es-AR", options).format(new Date(isoUtc));
  } catch {
    return new Intl.DateTimeFormat("es-AR", { dateStyle: "short", timeStyle: "short" }).format(new Date(isoUtc));
  }
}

// Tipos de comida de la API (MealType) con su nombre en pantalla, en el orden del día.
export const MEAL_TYPES = [
  ["Breakfast", "Desayuno"],
  ["Lunch", "Almuerzo"],
  ["AfternoonSnack", "Merienda"],
  ["Dinner", "Cena"],
  ["Snack", "Colación"],
];
