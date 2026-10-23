// Pantallas de la app. Para sumar una: crear js/screens/<screen>.js que exporte render(container) y agregar
// una línea acá. El orden es el del menú.
//
//   path:   lo que va después de "#" (#/peso)
//   screen: nombre del archivo en js/screens/ (sin .js)
//   title:  título de la pestaña y texto del menú
//   auth:   true = hace falta estar logueado (si no, va a #/login)
//   nav:    true = aparece en el menú

export const routes = [
  { path: "/login", screen: "login", title: "Ingresar", auth: false, nav: false },
  { path: "/registro", screen: "registro", title: "Crear cuenta", auth: false, nav: false },
  { path: "/dia", screen: "dia", title: "Mi día", auth: true, nav: true },
  { path: "/peso", screen: "peso", title: "Peso", auth: true, nav: true },
  { path: "/objetivo", screen: "objetivo", title: "Objetivo", auth: true, nav: true },
  { path: "/perfil", screen: "perfil", title: "Perfil", auth: true, nav: true },
  { path: "/rutinas", screen: "rutinas", title: "Rutinas", auth: true, nav: true },
  { path: "/chat", screen: "chat", title: "Asistente", auth: true, nav: true },
  { path: "/cuenta", screen: "cuenta", title: "Cuenta", auth: true, nav: true },
];

// Adónde ir con "#/" o una ruta desconocida.
export const homeLoggedIn = "/dia";
export const homeLoggedOut = "/login";
