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
