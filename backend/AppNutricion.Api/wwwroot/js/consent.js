// Texto de consentimiento del registro (Ley 25.326). La versión tiene que ser la misma que
// ConsentVersions.Current en la API: lo controla FrontendTests. Si cambia el texto, cambian las dos.
export const CONSENT_VERSION = "2026-10-21";

export const CONSENT_TEXT = [
  "Para usar la aplicación guardamos tu email, tu contraseña (cifrada con hash, nunca en texto), tu perfil "
    + "(fecha de nacimiento, sexo biológico, altura, nivel de actividad y zona horaria), tu historial de peso, "
    + "tus objetivos, tus comidas, tus rutinas y entrenamientos, y tus conversaciones con el asistente.",
  "Usamos esos datos solo para calcular tus objetivos y mostrarte tu progreso. Son datos de salud: no los "
    + "compartimos ni los vendemos. Al asistente de IA nunca le mandamos tu email ni tu nombre.",
  "Las calorías, los macros, el IMC y las proyecciones son estimaciones, no consejo médico.",
  "Podés descargar todos tus datos o borrar tu cuenta con todos sus datos cuando quieras, desde la "
    + "pantalla Cuenta (Ley 25.326 de Protección de los Datos Personales).",
];
