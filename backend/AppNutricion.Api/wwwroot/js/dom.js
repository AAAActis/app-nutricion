// Armado del DOM sin convertir texto en HTML: todo texto entra como nodo de texto (como textContent), así un
// dato del usuario nunca se interpreta como HTML. Usalo en todas las pantallas (FrontendTests lo controla).
//
//   el("p", { class: "muted" }, "Hola ", nombre)
//   el("button", { type: "button", onclick: () => ... }, "Guardar")
//
// props: "class" → className; "on<evento>" → addEventListener; true/false → atributo presente o no;
// el resto → setAttribute. Hijos: strings/números (texto), nodos, arrays o null/undefined (se ignoran).

export function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  applyProps(node, props);
  append(node, children);
  return node;
}

const SVG_NS = "http://www.w3.org/2000/svg";

export function svg(tag, props = {}, ...children) {
  const node = document.createElementNS(SVG_NS, tag);
  applyProps(node, props);
  append(node, children);
  return node;
}

function applyProps(node, props) {
  for (const [key, value] of Object.entries(props ?? {})) {
    if (value === undefined || value === null || value === false) continue;
    if (key === "class") node.setAttribute("class", value);
    else if (key.startsWith("on") && typeof value === "function") node.addEventListener(key.slice(2), value);
    else if (value === true) node.setAttribute(key, "");
    else node.setAttribute(key, String(value));
  }
}

export function append(node, children) {
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return node;
}

// Reemplaza todo el contenido de un nodo.
export function setChildren(node, ...children) {
  node.replaceChildren();
  return append(node, children);
}
