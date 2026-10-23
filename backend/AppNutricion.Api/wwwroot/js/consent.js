// Texto de consentimiento del registro (Ley 25.326). La versión tiene que ser la misma que
// ConsentVersions.Current en la API: lo controla FrontendTests. Si cambia el texto, cambian las dos.
export const CONSENT_VERSION = "2026-10-23";

// Leyenda obligatoria del art. 3 de la Resolución 14/2018 de la AAIP, copiada textual de
// https://www.argentina.gob.ar/normativa/nacional/resoluci%C3%B3n-14-2018-307621/texto. No editar ni partir en
// varias líneas: FrontendTests la busca textual.
const AAIP_LEGEND = "LA AGENCIA DE ACCESO A LA INFORMACIÓN PÚBLICA, en su carácter de Órgano de Control de la Ley N° 25.326, tiene la atribución de atender las denuncias y reclamos que interpongan quienes resulten afectados en sus derechos por incumplimiento de las normas vigentes en materia de protección de datos personales";

export const CONSENT_TEXT = [
  "Responsable de los datos: el equipo del proyecto académico Nutrición Inteligente (UNC). "
    + "Contacto: infobbasolutions@gmail.com.",
  "Qué datos guardamos: tu email, tu contraseña (guardada como hash, que no se puede revertir), tu perfil "
    + "(fecha de nacimiento, sexo biológico, altura, nivel de actividad y zona horaria), tu historial de peso, "
    + "tus objetivos, tus comidas, tus rutinas y entrenamientos, y tus conversaciones con el asistente.",
  "Finalidad: calcular tus objetivos de calorías y macros, registrar lo que comés y entrenás, y mostrarte tu "
    + "progreso. Son datos de salud: no los compartimos ni los vendemos. Al asistente de IA nunca le mandamos tu "
    + "email ni tu nombre.",
  "Estos datos son necesarios para usar el servicio: sin ellos la aplicación no puede calcular ni guardar nada.",
  "Las calorías, los macros, el IMC y las proyecciones son estimaciones, no consejo médico.",
  "El asistente usa un servicio externo de inteligencia artificial (Google Gemini). Le enviamos "
    + "los mensajes que escribís en el chat, tu objetivo, tus totales del día y tus rutinas; nunca tu email ni tu nombre. "
    + "Según las condiciones de Google, esos datos pueden usarse para mejorar sus servicios.",
  "Tus derechos, según el art. 6 de la Ley 25.326: acceso (podés descargar todos tus datos desde Cuenta), "
    + "rectificación (desde Perfil) y supresión (desde Cuenta), que borra la cuenta con todos sus datos.",
  `${AAIP_LEGEND}.`,
];
