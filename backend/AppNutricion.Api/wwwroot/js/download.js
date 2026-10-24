// Descarga un texto como archivo, sin pasar por el servidor (Cuenta: el .json; Mi día: el .csv de comidas).
import { append, el } from "./dom.js";

export function download(fileName, text, type = "application/json") {
  const url = URL.createObjectURL(new Blob([text], { type }));
  const link = el("a", { href: url, download: fileName });
  append(document.body, [link]);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
