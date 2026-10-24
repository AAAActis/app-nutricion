// Punto de entrada: arranca el router sobre <main id="app"> y el menú <nav id="nav">.
import { startRouter } from "./router.js";

startRouter(document.getElementById("app"), document.getElementById("nav"));

// El enlace de accesibilidad enfoca el contenido sin cambiar la ruta por hash.
document.querySelector(".skip-link").addEventListener("click", (event) => {
  event.preventDefault();
  document.getElementById("app").focus();
});
