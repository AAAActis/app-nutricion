// Router por hash (#/login, #/perfil…). Cada pantalla es un módulo de js/screens/ que exporta render(container);
// se carga recién cuando se entra (import dinámico). Las rutas están en routes.js.

import { isLoggedIn, clearSession } from "./auth.js";
import { append, el, setChildren } from "./dom.js";
import { notice } from "./forms.js";
import { routes, homeLoggedIn, homeLoggedOut } from "./routes.js";

let container;
let nav;
let flash = null;

// Un mensaje para mostrar arriba de la próxima pantalla (por ejemplo, "Cuenta borrada").
export function setFlash(kind, text) {
  flash = { kind, text };
}

export function go(path) {
  location.hash = `#${path}`;
}

export function startRouter(main, navElement) {
  container = main;
  nav = navElement;
  window.addEventListener("hashchange", renderCurrent);
  renderCurrent();
}

function currentPath() {
  const hash = location.hash.replace(/^#/, "");
  return hash.split("?")[0] || "/";
}

async function renderCurrent() {
  const path = currentPath();
  const route = routes.find((r) => r.path === path);
  if (!route) {
    go(isLoggedIn() ? homeLoggedIn : homeLoggedOut);
    return;
  }
  if (route.auth && !isLoggedIn()) {
    go(homeLoggedOut);
    return;
  }
  if (!route.auth && isLoggedIn()) {
    go(homeLoggedIn);
    return;
  }

  renderNav(path);
  document.title = `${route.title} · Nutrición Inteligente`;
  setChildren(container, el("p", { class: "muted" }, "Cargando…"));

  try {
    const screen = await import(`./screens/${route.screen}.js`);
    if (currentPath() !== path) return;   // el usuario ya se fue a otra pantalla
    container.replaceChildren();
    if (flash) {
      append(container, [notice(flash.kind, flash.text)]);
      flash = null;
    }
    await screen.render(container);
    container.focus({ preventScroll: true });
  } catch (error) {
    console.error(error);
    setChildren(container, notice("error", error?.title ?? "No se pudo cargar la pantalla. Recargá la página."));
  }
}

function renderNav(path) {
  if (!isLoggedIn()) {
    nav.replaceChildren();
    return;
  }
  const links = routes.filter((r) => r.nav).map((r) =>
    el("a", { href: `#${r.path}`, "aria-current": r.path === path ? "page" : false }, r.title));
  const logout = el("button", {
    type: "button",
    class: "link-button",
    onclick: () => {
      clearSession();
      go(homeLoggedOut);
    },
  }, "Salir");
  setChildren(nav, links, logout);
}
