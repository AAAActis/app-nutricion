// Rutinas y entrenamientos (S7). Rutinas: GET /api/routines → [{ id, name, description, isPreset }] y
// GET /api/routines/{id} → { …, days: [{ id, dayNumber, name, exercises: [{ exerciseId, exerciseName, muscleGroup,
// order, targetSets, targetReps, targetWeightKg }] }] }. Ejercicios: GET /api/exercises.
// Entrenamientos: POST /api/workouts { routineDayId, date, durationMinutes, notes, sets: [{ exerciseId, setNumber,
// reps, weightKg }] } → { estimatedCaloriesBurned, disclaimer, … }; 409 sin peso registrado.
// Historial: GET /api/workouts?from&to (hasta 366 días), GET y DELETE /api/workouts/{id}.
import { api } from "../api.js";
import { append, el, setChildren } from "../dom.js";
import { field, formError, notice, onSubmit, options, setNotice } from "../forms.js";
import { addDays, dateText, num, plural, today } from "../format.js";
import { profileTimeZone } from "../profile.js";

const MUSCLE_GROUPS = {
  Chest: "Pecho",
  Back: "Espalda",
  Legs: "Piernas",
  Shoulders: "Hombros",
  Arms: "Brazos",
  Core: "Core",
  FullBody: "Cuerpo completo",
  Cardio: "Cardio",
};
const HISTORY_DAYS = 30;
const DEFAULT_DURATION = 45;
const DEFAULT_REPS = 10;

export async function render(container) {
  const state = { today: today(await profileTimeZone()), exercises: [] };
  const [routines, exercises] = await Promise.all([api("/api/routines"), api("/api/exercises")]);
  state.exercises = exercises;

  const detail = el("section", { class: "card", hidden: true });
  const history = historyCard(state);
  const workout = workoutCard(state, () => history.reload());

  append(container, [
    el("h1", {}, "Rutinas"),
    routinesCard(routines, (id) => showRoutine(detail, id, workout)),
    detail,
    workout.card,
    history.card]);

  await history.reload();
}

// ---------- Lista de rutinas (precargadas y propias) ----------

function routinesCard(routines, onOpen) {
  const groups = [
    ["Precargadas", routines.filter((r) => r.isPreset)],
    ["Mis rutinas", routines.filter((r) => !r.isPreset)],
  ];
  return el("section", { class: "card" },
    el("h2", {}, "Rutinas"),
    groups.map(([title, list]) => [
      el("h3", {}, title),
      list.length === 0
        ? el("p", { class: "muted" }, title === "Mis rutinas" ? "Todavía no tenés rutinas propias." : "No hay rutinas precargadas.")
        : el("ul", { class: "list" }, list.map((routine) => el("li", {},
          el("div", { class: "item" },
            el("span", { class: "name" }, routine.name),
            el("button", { type: "button", class: "button secondary small", onclick: () => onOpen(routine.id) }, "Ver")),
          routine.description ? el("div", { class: "muted small" }, routine.description) : null))),
    ]));
}

async function showRoutine(card, id, workout) {
  card.hidden = false;
  setChildren(card, el("p", { class: "muted" }, "Cargando…"));
  try {
    const routine = await api(`/api/routines/${id}`);
    setChildren(card,
      el("h2", {}, routine.name),
      el("p", { class: "muted small" }, routine.isPreset ? "Rutina precargada" : "Rutina propia"),
      routine.description ? el("p", {}, routine.description) : null,
      routine.days.length === 0 ? el("p", { class: "muted" }, "Esta rutina no tiene días.") : null,
      routine.days.map((day) => [
        el("div", { class: "item" },
          el("h3", {}, `Día ${day.dayNumber}: ${day.name}`),
          el("button", { type: "button", class: "button small", onclick: () => workout.startFromDay(routine, day) }, "Registrar este día")),
        el("ul", { class: "list" }, day.exercises.map((exercise) => el("li", {},
          el("div", { class: "item" },
            el("span", { class: "name" }, exercise.exerciseName),
            el("span", {}, targetText(exercise))),
          el("div", { class: "muted small" }, MUSCLE_GROUPS[exercise.muscleGroup] ?? exercise.muscleGroup)))),
      ]));
  } catch (error) {
    setChildren(card, notice("error", error.title));
  }
  card.scrollIntoView({ behavior: "smooth", block: "start" });
}

function targetText(exercise) {
  const weight = exercise.targetWeightKg ? ` · ${num(exercise.targetWeightKg)} kg` : "";
  return `${exercise.targetSets} × ${exercise.targetReps}${weight}`;
}

// ---------- Registrar un entrenamiento ----------

// Las series viven en "sets" y la lista se vuelve a dibujar en cada cambio: así los nombres de los campos
// ("sets[0].reps") coinciden siempre con el índice que usa la API en sus errores.
function workoutCard(state, onSaved) {
  const result = el("div");
  const dayInfo = el("div");
  const setsList = el("div");
  let sets = [];
  let routineDayId = null;

  const exerciseOptions = state.exercises.map((e) => [String(e.id), `${e.name} (${MUSCLE_GROUPS[e.muscleGroup] ?? e.muscleGroup})`]);

  const renderSets = () => setChildren(setsList,
    sets.length === 0 ? el("p", { class: "muted" }, "Agregá al menos una serie.") : null,
    sets.map((set, i) => setRow(set, i)));

  function setRow(set, i) {
    const exercise = el("select", { name: `sets[${i}].exerciseId` }, options(exerciseOptions, String(set.exerciseId)));
    const reps = el("input", { type: "number", name: `sets[${i}].reps`, min: 1, max: 100, step: "1", value: set.reps, inputmode: "numeric" });
    const weight = el("input", { type: "number", name: `sets[${i}].weightKg`, min: 0, max: 500, step: "0.5", value: set.weightKg, inputmode: "decimal" });
    exercise.addEventListener("change", () => { set.exerciseId = Number(exercise.value); });
    reps.addEventListener("input", () => { set.reps = reps.value; });
    weight.addEventListener("input", () => { set.weightKg = weight.value; });
    return el("fieldset", { class: "set-row" },
      el("legend", {}, `Serie ${i + 1}`),
      field("Ejercicio", exercise),
      field("Repeticiones", reps),
      field("Peso (kg)", weight, "0 = peso corporal o cardio"),
      el("button", {
        type: "button",
        class: "button secondary small",
        onclick: () => {
          sets.splice(i, 1);
          renderSets();
        },
      }, "Quitar serie"));
  }

  const addSet = (exerciseId = state.exercises[0]?.id, reps = DEFAULT_REPS, weightKg = 0) => {
    sets.push({ exerciseId, reps, weightKg });
  };

  const showDay = (label) => setChildren(dayInfo, label
    ? el("p", { class: "item" },
      el("span", {}, `De la rutina: ${label}`),
      el("button", {
        type: "button",
        class: "button secondary small",
        onclick: () => {
          routineDayId = null;
          showDay(null);
        },
      }, "No asociar a la rutina"))
    : null);

  const form = el("form", { novalidate: true },
    dayInfo,
    field("Fecha", el("input", { type: "date", name: "date", value: state.today, max: state.today, required: true })),
    field("Duración (minutos)", el("input", { type: "number", name: "durationMinutes", min: 1, max: 600, step: "1", value: DEFAULT_DURATION, required: true })),
    field("Notas", el("textarea", { name: "notes", maxlength: 500, rows: 2 })),
    el("h3", {}, "Series"),
    setsList,
    el("div", { class: "field-error", "data-error-for": "sets", "aria-live": "polite" }),
    el("button", {
      type: "button",
      class: "button secondary small",
      onclick: () => {
        addSet();
        renderSets();
      },
    }, "Agregar serie"),
    formError(),
    el("button", { type: "submit", class: "button" }, "Guardar entrenamiento"));

  onSubmit(form, async (data) => {
    const duration = data.get("durationMinutes");
    const notes = String(data.get("notes") ?? "").trim();
    const numbers = new Map();   // setNumber por ejercicio: 1, 2, 3… en el orden de la lista
    const saved = await api("/api/workouts", {
      method: "POST",
      body: {
        routineDayId,
        date: data.get("date"),
        durationMinutes: duration === "" ? 0 : Number(duration),
        notes: notes === "" ? null : notes,
        sets: sets.map((set) => {
          const setNumber = (numbers.get(set.exerciseId) ?? 0) + 1;
          numbers.set(set.exerciseId, setNumber);
          return {
            exerciseId: Number(set.exerciseId),
            setNumber,
            reps: set.reps === "" ? 0 : Number(set.reps),
            weightKg: set.weightKg === "" ? 0 : Number(set.weightKg),
          };
        }),
      },
    });
    setNotice(result, "ok",
      `Entrenamiento guardado: ${plural(saved.sets.length, "serie", "series")}, ${saved.durationMinutes} min, `,
      el("strong", {}, `${num(saved.estimatedCaloriesBurned, 0)} kcal estimadas`), ". ",
      el("span", { class: "disclaimer" }, saved.disclaimer));
    form.reset();
    sets = [];
    routineDayId = null;
    showDay(null);
    addSet();
    renderSets();
    await onSaved();
  });

  addSet();
  renderSets();

  const card = el("section", { class: "card" },
    el("h2", {}, "Registrar entrenamiento"),
    el("p", { class: "muted small" }, "Las kcal se estiman con el MET de cada ejercicio, tu último peso y la duración."),
    result,
    form);

  return {
    card,
    // Arranca el formulario con los ejercicios del día: una fila por serie objetivo.
    startFromDay(routine, day) {
      routineDayId = day.id;
      sets = [];
      for (const exercise of day.exercises)
        for (let n = 0; n < exercise.targetSets; n++)
          addSet(exercise.exerciseId, exercise.targetReps, exercise.targetWeightKg ?? 0);
      if (sets.length === 0) addSet();
      renderSets();
      showDay(`${routine.name}, día ${day.dayNumber} (${day.name})`);
      result.replaceChildren();
      card.scrollIntoView({ behavior: "smooth", block: "start" });
    },
  };
}

// ---------- Historial por rango de fechas ----------

function historyCard(state) {
  const list = el("div");
  const form = el("form", { class: "inline", novalidate: true },
    field("Desde", el("input", { type: "date", name: "from", value: addDays(state.today, -(HISTORY_DAYS - 1)), required: true })),
    field("Hasta", el("input", { type: "date", name: "to", value: state.today, required: true })),
    formError(),
    el("button", { type: "submit", class: "button small" }, "Ver"));

  const load = async () => {
    const from = encodeURIComponent(form.elements.namedItem("from").value);
    const to = encodeURIComponent(form.elements.namedItem("to").value);
    const workouts = await api(`/api/workouts?from=${from}&to=${to}`);
    renderHistory(list, workouts, load);
  };

  onSubmit(form, load);

  return {
    card: el("section", { class: "card" }, el("h2", {}, "Historial de entrenamientos"), form, list),
    reload: () => load().catch((error) => setNotice(list, "error", error.title)),
  };
}

function renderHistory(list, workouts, reload) {
  if (workouts.length === 0) {
    setChildren(list, el("p", { class: "muted" }, "No hay entrenamientos en esas fechas."));
    return;
  }
  const total = workouts.reduce((sum, w) => sum + Number(w.estimatedCaloriesBurned), 0);
  setChildren(list,
    el("p", {}, `${plural(workouts.length, "entrenamiento", "entrenamientos")} · ${num(total, 0)} kcal estimadas en total.`),
    el("ul", { class: "list" }, workouts.map((workout) => workoutItem(workout, reload))),
    el("p", { class: "muted small disclaimer" }, workouts[0].disclaimer));
}

function workoutItem(workout, reload) {
  const sets = el("div");
  const li = el("li", {},
    el("div", { class: "item" },
      el("span", { class: "name" }, dateText(workout.date)),
      el("span", {}, `${workout.durationMinutes} min · ${plural(workout.setCount, "serie", "series")} · ${num(workout.volumeKg, 0)} kg de volumen · ${num(workout.estimatedCaloriesBurned, 0)} kcal`),
      el("span", { class: "row" },
        el("button", { type: "button", class: "button secondary small", onclick: toggle }, "Series"),
        el("button", { type: "button", class: "button secondary small", onclick: remove }, "Borrar"))),
    workout.notes ? el("div", { class: "muted small" }, workout.notes) : null,
    sets);

  async function toggle() {
    if (sets.childNodes.length > 0) {
      sets.replaceChildren();
      return;
    }
    try {
      const full = await api(`/api/workouts/${workout.id}`);
      setChildren(sets, el("ol", { class: "small" }, full.sets.map((s) =>
        el("li", {}, `${s.exerciseName}: ${s.reps} reps${Number(s.weightKg) > 0 ? ` × ${num(s.weightKg)} kg` : ""}`))));
    } catch (error) {
      setNotice(sets, "error", error.title);
    }
  }

  async function remove() {
    if (!confirm(`¿Borrar el entrenamiento del ${dateText(workout.date)}?`)) return;
    try {
      await api(`/api/workouts/${workout.id}`, { method: "DELETE" });
      await reload();
    } catch (error) {
      setNotice(sets, "error", error.title);
    }
  }

  return li;
}
