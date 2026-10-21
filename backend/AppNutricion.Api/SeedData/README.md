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
