// Punto de entrada: arranca el router sobre <main id="app"> y el menú <nav id="nav">.
import { startRouter } from "./router.js";

startRouter(document.getElementById("app"), document.getElementById("nav"));
