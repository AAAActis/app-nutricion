# SeedData · `foods_seed.json`

La API carga `SeedData/foods_seed.json` al arrancar (`FoodSeedHostedService`, L2). Si el archivo no existe, sigue sin cargar nada. Si la carga falla, deja un log de error y la API levanta igual.

El archivo lo genera Santi (S3). Acá no se commitean datos inventados.

## Contrato

Un array de objetos. Las macros van por **100 g**:

```json
[
  {
    "name": "Leche entera",
    "brand": "Marca",
    "barcode": "7790000000000",
    "source": "Argenfoods",
    "externalId": "AF-0123",
    "caloriesPer100g": 61,
    "proteinPer100g": 3.2,
    "carbsPer100g": 4.8,
    "fatPer100g": 3.3,
    "portionName": "vaso",
    "portionWeightGrams": 200
  }
]
```

| Campo | Obligatorio | Notas |
|---|---|---|
| `name` | sí | `NormalizedName` lo arma la API (minúsculas, sin tildes) |
| `source` | sí | `"Argenfoods"` o `"OpenFoodFacts"`, exacto. `Custom` se rechaza |
| `externalId` | sí | Junto con `source` forma la clave de idempotencia |
| `caloriesPer100g`, `proteinPer100g`, `carbsPer100g`, `fatPer100g` | sí | ≥ 0. Se redondean a 2 decimales |
| `brand`, `barcode`, `portionName`, `portionWeightGrams` | no | |

## Qué se descarta

Cada ítem descartado deja un warning en el log con el motivo:
- Falta `name`, `source` o `externalId`.
- `source` es `Custom` o un valor desconocido.
- Algún macro es negativo.
- Más de 900 kcal por 100 g.
- Proteína + carbos + grasas suman más de 100 g.
- Un texto supera el largo de su columna: `name` 200, `externalId` 64, `brand` 120, `barcode` 32, `portionName` 60.
- `(source, externalId)` repetido dentro del archivo (se queda el primero).

## Idempotencia

- Si `(source, externalId)` ya existe, el alimento se actualiza solo si cambió algún campo. Si no existe, se inserta.
- Los alimentos `Custom` de los usuarios nunca se tocan.
- Volver a arrancar con el mismo archivo da `0 insertados, 0 actualizados`.

Los datos de Open Food Facts van bajo licencia ODbL: hay que atribuirlos (ver D18 en `docs/ESTADO.md`).

# SeedData · `exercises_seed.json` (S7)

`TrainingSeedHostedService` lo carga al arrancar, con el mismo patrón que `foods_seed`: si no existe no carga nada, y si falla deja un log de error y la API levanta igual. Trae dos listas:

- `exercises`: `name` (único), `muscleGroup` (enum `MuscleGroup`), `metValue` y `compendiumCode` (solo para trazar la fuente: la API no lo usa).
- `routines`: rutinas precargadas (`UserId` null) con `days[].exercises[]`, que referencian el ejercicio por `exercise` (el nombre), `order`, `targetSets` y `targetReps`. Sin peso objetivo.

**Idempotencia:** los ejercicios se insertan por `name` y las rutinas precargadas por `name` con `UserId` null. Lo que ya existe no se toca (un MET cambiado en el archivo no pisa el de la base). Volver a arrancar da `0 ejercicios y 0 rutinas`. Si una rutina usa un ejercicio que no está en el catálogo, o un `muscleGroup` desconocido, la carga falla con el motivo en el log.

## Fuente de los MET

Herrmann SD, Willis EA, Ainsworth BE, et al. *2024 Adult Compendium of Physical Activities: A third update of the energy costs of human activities*. J Sport Health Sci 2024;13:6–12. https://pacompendium.com (páginas Conditioning Exercise, Bicycling, Running y Walking; consultadas el 2026-10-22).

| Ejercicio | Grupo | MET | Código | Actividad del Compendium |
|---|---|---|---|---|
| Sentadilla | Legs | 5.0 | 02052 | Resistance training, squats, deadlift |
| Peso muerto | Back | 5.0 | 02052 | Resistance training, squats, deadlift |
| Press de banca | Chest | 3.5 | 02054 | Resistance training, multiple exercises, 8–15 reps |
| Press inclinado con mancuernas | Chest | 3.5 | 02054 | ídem |
| Aperturas con mancuernas | Chest | 3.5 | 02054 | ídem |
| Remo con barra | Back | 3.5 | 02054 | ídem |
| Jalón al pecho | Back | 3.5 | 02054 | ídem |
| Press militar | Shoulders | 3.5 | 02054 | ídem |
| Elevaciones laterales | Shoulders | 3.5 | 02054 | ídem |
| Curl de bíceps | Arms | 3.5 | 02054 | ídem |
| Extensión de tríceps en polea | Arms | 3.5 | 02054 | ídem |
| Prensa de piernas | Legs | 3.5 | 02054 | ídem |
| Curl femoral | Legs | 3.5 | 02054 | ídem |
| Hip thrust | Legs | 3.5 | 02054 | ídem |
| Elevación de talones | Legs | 3.5 | 02054 | ídem |
| Flexiones de brazos | Chest | 3.8 | 02022 | Calisthenics, moderate (push-ups, pull-ups, lunges) |
| Dominadas | Back | 3.8 | 02022 | ídem |
| Fondos en paralelas | Arms | 3.8 | 02022 | ídem |
| Estocadas | Legs | 3.8 | 02022 | ídem |
| Plancha | Core | 2.8 | 02024 | Calisthenics, light (curl-ups, crunches, plank) |
| Abdominales (crunch) | Core | 2.8 | 02024 | ídem |
| Swing con kettlebell | FullBody | 9.8 | 02058 | Kettlebell swings |
| Burpees | FullBody | 7.5 | 02020 | Calisthenics, vigorous (push-ups, pull-ups, burpees) |
| Cinta: caminata (5 km/h) | Cardio | 3.8 | 17355 | Walking, treadmill, 3.0–3.4 mph, 0 % grade |
| Cinta: trote (8 km/h) | Cardio | 8.5 | 12030 | Running, 5.0–5.2 mph |
| Bicicleta fija | Cardio | 6.8 | 01200 | Bicycling, stationary, general |
| Elíptico | Cardio | 5.0 | 02048 | Elliptical trainer, moderate effort |
| Remo ergómetro | Cardio | 7.3 | 02070 | Rowing, stationary ergometer, general, vigorous |
| Saltar la soga | Cardio | 11.0 | 02068 | Rope skipping, general |

Notas:
- El Compendium no trae un valor por ejercicio de musculación aislado. Los ejercicios con barra, mancuerna o máquina usan 02054 (series de 8 a 15 repeticiones), salvo sentadilla y peso muerto, que tienen código propio (02052). Los de peso corporal usan 02022, que nombra flexiones, dominadas y estocadas. Los fondos no aparecen con nombre: entran por analogía.
- Para la cinta, la velocidad está en el nombre porque el MET depende de ella: 5 km/h ≈ 3,1 mph y 8 km/h ≈ 5,0 mph.
- En el sitio, los códigos con valor estimado (no medido) aparecen en rojo. Ese color no se pudo leer en la extracción, así que no está verificado cuáles de estos son estimados.
- Las series y repeticiones de las rutinas precargadas son una programación habitual de principiante e intermedio (de 3 a 4 series de 5 a 15 repeticiones). No son datos medidos.
