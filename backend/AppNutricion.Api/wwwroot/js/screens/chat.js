// Chat con el asistente (S9). Conversaciones: GET y POST /api/assistant/conversations → { id, title, createdAt,
// updatedAt }. Historial: GET /api/assistant/conversations/{id}/messages → [{ id, role, content, createdAt,
// suggestions }]. Enviar: POST /api/assistant/conversations/{id}/messages { content } (1 a 1000 caracteres; con
// límite por minuto → 429 con Retry-After) → { message, suggestions, degraded }. degraded = el LLM no respondió y
// el texto es el de respaldo. Sugerencias: POST /api/assistant/suggestions/{id}/accept (crea la comida, con los
// valores de la tabla de alimentos) o /ignore → { status, mealEntryId, … }.
import { api } from "../api.js";
import { el, setChildren } from "../dom.js";
import { clearErrors, field, formError, notice, setNotice, showErrors } from "../forms.js";
import { MEAL_TYPES, dateText, dateTimeText, num } from "../format.js";
import { profileTimeZone } from "../profile.js";

const CONTENT_MAX = 1000;
const DISCLAIMER = "Estimación, no es consejo médico.";
const MEAL_LABELS = Object.fromEntries(MEAL_TYPES);

export async function render(container) {
  const state = { timeZone: await profileTimeZone(), conversations: [], current: null, cooldownTimer: null };

  const conversationsList = el("ul", { class: "list" });
  const log = el("div", { class: "chat-log", "aria-live": "polite" });
  const status = el("div");
  const title = el("h2", {}, "Conversación");

  const textarea = el("textarea", {
    name: "content",
    rows: 3,
    maxlength: 1000,
    required: true,
    placeholder: "Ej.: ¿Cuánta proteína me falta hoy? Comí 150 g de pollo en el almuerzo.",
  });
  const counter = el("div", { class: "muted small" }, `0 / ${CONTENT_MAX}`);
  textarea.addEventListener("input", () => { counter.textContent = `${textarea.value.length} / ${CONTENT_MAX}`; });
  const sendButton = el("button", { type: "submit", class: "button" }, "Enviar");
  const form = el("form", { novalidate: true }, field("Mensaje", textarea), counter, formError(), sendButton);

  const loadConversations = async () => {
    state.conversations = await api("/api/assistant/conversations");
    setChildren(conversationsList,
      state.conversations.length === 0 ? el("li", { class: "muted" }, "Todavía no hay conversaciones.") : null,
      state.conversations.map((c) => el("li", {},
        el("div", { class: "item" },
          el("span", { class: "name" }, conversationTitle(c, state.timeZone)),
          el("button", {
            type: "button",
            class: "button secondary small",
            "aria-current": c.id === state.current?.id ? "true" : false,
            onclick: () => open(c).catch((error) => setNotice(status, "error", error.title)),
          }, c.id === state.current?.id ? "Abierta" : "Abrir")))));
  };

  async function open(conversation) {
    state.current = conversation;
    title.textContent = conversationTitle(conversation, state.timeZone);
    status.replaceChildren();
    setChildren(log, el("p", { class: "muted" }, "Cargando…"));
    const messages = await api(`/api/assistant/conversations/${conversation.id}/messages`);
    // Los mensajes "Tool" son internos del asistente (resultados de herramientas): no se muestran.
    const visible = messages.filter((m) => m.role === "User" || m.role === "Assistant");
    setChildren(log,
      visible.length === 0 ? el("p", { class: "muted" }, "Escribí tu primera pregunta.") : null,
      visible.map((m) => messageNode(m.role, m.content, m.createdAt, m.suggestions, state)));
    await loadConversations();
    textarea.focus();
  }

  async function newConversation() {
    const conversation = await api("/api/assistant/conversations", { method: "POST" });
    await open(conversation);
    return conversation;
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    clearErrors(form);
    if (state.cooldownTimer) return;
    sendButton.disabled = true;
    const content = textarea.value;
    try {
      const conversation = state.current ?? await newConversation();
      const reply = await api(`/api/assistant/conversations/${conversation.id}/messages`, { method: "POST", body: { content } });
      if (log.querySelector("p.muted")) log.replaceChildren();
      log.append(
        messageNode("User", content.trim(), new Date().toISOString(), [], state),
        messageNode("Assistant", reply.message.content, reply.message.createdAt, reply.suggestions, state));
      if (reply.degraded)
        setNotice(status, "warning", "El asistente no está disponible en este momento: la respuesta es automática, con tus totales de hoy. Probá de nuevo en un rato.");
      else status.replaceChildren();
      textarea.value = "";
      counter.textContent = `0 / ${CONTENT_MAX}`;
      log.lastElementChild?.scrollIntoView({ behavior: "smooth", block: "nearest" });
      await loadConversations();
    } catch (error) {
      if (error && "status" in error) {
        showErrors(form, error);
        if (error.status === 429) cooldown(error.retryAfter);
      } else {
        console.error(error);
        showErrors(form, { title: "Algo falló en la página. Recargá y probá de nuevo.", fieldErrors: {} });
      }
    } finally {
      if (!state.cooldownTimer) sendButton.disabled = false;
    }
  });

  // Con un 429 el botón queda deshabilitado hasta que pase el Retry-After (sin él, 30 segundos).
  function cooldown(seconds) {
    let left = seconds ?? 30;
    sendButton.disabled = true;
    sendButton.textContent = `Esperá ${left} s`;
    state.cooldownTimer = setInterval(() => {
      left -= 1;
      if (left > 0) {
        sendButton.textContent = `Esperá ${left} s`;
        return;
      }
      clearInterval(state.cooldownTimer);
      state.cooldownTimer = null;
      sendButton.disabled = false;
      sendButton.textContent = "Enviar";
    }, 1000);
  }

  container.append(
    el("h1", {}, "Asistente"),
    el("p", { class: "disclaimer" }, DISCLAIMER),
    el("section", { class: "card" },
      el("div", { class: "item" },
        el("h2", {}, "Conversaciones"),
        el("button", {
          type: "button",
          class: "button small",
          onclick: () => newConversation().catch((error) => setNotice(status, "error", error.title)),
        }, "Nueva conversación")),
      conversationsList),
    el("section", { class: "card" },
      title,
      el("p", { class: "muted small" },
        "El asistente responde con tus datos y puede proponerte registrar comidas. Las calorías y los macros salen siempre de la tabla de alimentos, nunca del asistente."),
      status,
      log,
      form,
      el("p", { class: "muted small disclaimer" }, DISCLAIMER)));

  await loadConversations();
  if (state.conversations.length > 0) await open(state.conversations[0]);
  else setChildren(log, el("p", { class: "muted" }, "Escribí tu primera pregunta: la conversación se crea sola."));
}

function conversationTitle(conversation, timeZone) {
  return conversation.title || `Conversación del ${dateTimeText(conversation.createdAt, timeZone)}`;
}

function messageNode(role, content, createdAt, suggestions, state) {
  const mine = role === "User";
  return el("div", { class: `chat-message ${mine ? "user" : "assistant"}` },
    el("div", { class: "muted small" }, `${mine ? "Vos" : "Asistente"} · ${dateTimeText(createdAt, state.timeZone)}`),
    el("p", { class: "chat-text" }, content),
    suggestions.length > 0 ? el("ul", { class: "list" }, suggestions.map(suggestionItem)) : null);
}

// Una comida propuesta por el asistente: se carga recién cuando el usuario la acepta.
function suggestionItem(suggestion) {
  const li = el("li", { class: "suggestion" });
  const status = el("div");

  const show = (s) => setChildren(li,
    el("div", { class: "item" },
      el("span", {},
        el("span", { class: "name" }, s.foodName),
        ` · ${num(s.quantityGrams)} g · ${MEAL_LABELS[s.mealType] ?? s.mealType} del ${dateText(s.date)}`),
      s.status === "Pending"
        ? el("span", { class: "row" },
          el("button", { type: "button", class: "button small", onclick: () => resolve(`/api/assistant/suggestions/${s.id}/accept`) }, "Aceptar"),
          el("button", { type: "button", class: "button secondary small", onclick: () => resolve(`/api/assistant/suggestions/${s.id}/ignore`) }, "Ignorar"))
        : el("span", { class: "muted small" },
          s.status === "Accepted" ? ["Cargada en ", el("a", { href: "#/dia" }, "Mi día")] : "Ignorada")),
    status);

  async function resolve(path) {
    for (const button of li.querySelectorAll("button")) button.disabled = true;
    try {
      show(await api(path, { method: "POST" }));
    } catch (error) {
      for (const button of li.querySelectorAll("button")) button.disabled = false;
      const detail = Object.values(error.fieldErrors ?? {}).join(" ");
      setChildren(status, notice("error", [error.title, detail].filter(Boolean).join(" ")));
    }
  }

  show(suggestion);
  return li;
}
