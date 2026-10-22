// Zonas horarias IANA para el perfil (la API solo acepta IANA). Primero las de Argentina; después, todas las
// que conoce el navegador (si las expone).

export const DEFAULT_TIME_ZONE = "America/Argentina/Cordoba";

const ARGENTINA = [
  "America/Argentina/Buenos_Aires",
  "America/Argentina/Catamarca",
  "America/Argentina/Cordoba",
  "America/Argentina/Jujuy",
  "America/Argentina/La_Rioja",
  "America/Argentina/Mendoza",
  "America/Argentina/Rio_Gallegos",
  "America/Argentina/Salta",
  "America/Argentina/San_Juan",
  "America/Argentina/San_Luis",
  "America/Argentina/Tucuman",
  "America/Argentina/Ushuaia",
];

const OTHERS = [
  "America/Montevideo", "America/Santiago", "America/Sao_Paulo", "America/Asuncion", "America/La_Paz",
  "America/Lima", "America/Bogota", "America/Mexico_City", "America/New_York", "Europe/Madrid", "UTC",
];

export function timeZones() {
  let all = [];
  try {
    if (typeof Intl.supportedValuesOf === "function") all = Intl.supportedValuesOf("timeZone");
  } catch {
    all = [];
  }
  const rest = [...new Set([...OTHERS, ...all])].filter((z) => !ARGENTINA.includes(z)).sort();
  return [...ARGENTINA, ...rest];
}
