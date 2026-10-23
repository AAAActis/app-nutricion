# tools/foods-seed

Genera `backend/AppNutricion.Api/SeedData/foods_seed.json` (contrato en `backend/AppNutricion.Api/SeedData/README.md`).

## Cómo correrlo

Desde la raíz del repo, con el SDK de .NET 10 (no hace falta Python ni paquetes):

```
dotnet run tools/foods-seed/foods-seed.cs
```

Si Open Food Facts está caído (503), `dotnet run tools/foods-seed/foods-seed.cs -- --solo-argenfoods` genera el archivo solo con ARGENFOODS.

Tarda unos 30 s: baja una ficha por alimento de ARGENFOODS (con 300 ms entre pedidos) y hace 2 búsquedas a Open Food Facts (7 s entre ellas; si OFF responde 503 espera 60 s y reintenta, hasta 3 veces). Al final muestra cuántos quedaron por fuente y cada descarte con su motivo. Si quedan menos de 30 productos de OFF, sale con código 1.

Antes de bajar nada corre un autochequeo (parser de la ficha, kJ → kcal y reglas de validación).

## Qué hace

**ARGENFOODS (básicos).** La lista de códigos elegidos está al principio de `foods-seed.cs`. Por cada código baja `https://www.argenfood.unlu.edu.ar/Tablas/Codigo/{código}.htm` y lee, por 100 g de porción comestible:
- Energía en kcal (si no está, kJ ÷ 4,184).
- Proteínas y Grasa Total.
- Carbohidratos disponibles; si la ficha no los trae, totales menos fibra; si tampoco hay fibra, totales (casi todas las fichas traen solo totales).

Si un macro viene como `...` o con la celda vacía (la UNLu no documenta qué significa), se toma como **0 solo si** la kcal publicada en la misma ficha coincide con 4·P + 4·C + 9·G con ese macro en 0, con margen de ±5 % o ±5 kcal (el más amplio). Si no coincide, la ficha se descarta. El margen no se amplía; las que quedan afuera por menos del doble del margen se listan aparte. La ficha sin energía se descarta siempre.

Fichas con un macro tomado como 0 (extracción del 2026-10-22):

| Código | Ficha | Macro = 0 | kcal publicada | 4P+4C+9G | Dif. |
|---|---|---|---|---|---|
| 307 | Manteca, fresca | carbohidratos | 758 | 758 | 0 |
| 345 | Huevo de gallina, clara, cruda | carbohidratos | 48 | 48,2 | 0,2 |
| 346 | Huevo de gallina, yema, cruda | carbohidratos | 325 | 324,7 | 0,3 |
| 348 | Huevo de gallina, frito | carbohidratos | 196 | 195,7 | 0,3 |
| 233 | Vacuno, lomo, fresco, crudo | carbohidratos | 116 | 116 | 0 |
| 234 | Vacuno, nalga, fresco, crudo | carbohidratos | 97 | 97,4 | 0,4 |
| 229 | Vacuno, cuadril y bife angosto flaco, fresco, crudo | carbohidratos | 111 | 111 | 0 |
| 226 | Vacuno, asado, fresco, crudo | carbohidratos | 170 | 169,9 | 0,1 |
| 239 | Vacuno, vacío flaco, fresco, crudo | carbohidratos | 171 | 170,8 | 0,2 |
| 553 | Vacuno, hamburguesas crudas | carbohidratos | 218 | 217,7 | 0,3 |
| 221 | Pollo, carne-pellejo-menudos, fresco, crudo | carbohidratos | 88 | 87,7 | 0,3 |
| 223 | Pollo, asado al horno | carbohidratos | 162 | 162,2 | 0,2 |
| 216 | Cerdo, jamón cocido | carbohidratos | 211 | 210,8 | 0,2 |
| 110 | Tomate, fresco, crudo | grasas | 20 | 20,4 | 0,4 |
| 87 | Papa, pulpa sin cáscara, fresca, cruda | grasas | 88 | 88 | 0 |
| 88 | Papa, pulpa sin cáscara, hervida | grasas | 88 | 88 | 0 |
| 69 | Cebolla blanca, bulbo, cruda | grasas | 17 | 17,2 | 0,2 |
| 117 | Zapallito, parte tierna, fresco, crudo | grasas | 13 | 12,8 | 0,2 |
| 75 | Espinaca, hoja, fresca, cruda | grasas | 24 | 23,6 | 0,4 |
| 61 | Batata, raiz, fresca, cruda | grasas | 84 | 83,6 | 0,4 |
| 104 | Repollo blanco, hoja, fresco, crudo | grasas | 17 | 17,2 | 0,2 |
| 146 | Pomelo, pulpa, fresco | grasas | 26 | 25,6 | 0,4 |
| 151 | Aceite de girasol Familiar | proteínas y carbohidratos | 900 | 900 | 0 |
| 157 | Aceite de oliva Cocinero | proteínas y carbohidratos | 900 | 900 | 0 |

Afuera por poco: ninguna. Descartada: 114 (Zanahoria, raíz, pelada, fresca), porque la ficha no trae energía. El script imprime esta lista en cada corrida.

`source = "Argenfoods"`, `externalId` = código de la tabla, `name` = nombre de la ficha sin el nombre científico. Porciones solo en leche, yogur y huevo (pesos de las Guías Alimentarias para la Población Argentina 2016: 1 huevo = 50 g, 1 taza = 200 ml).

**Open Food Facts Argentina (envasados).** `GET /api/v2/search?countries_tags=en:argentina&sort_by=popularity_key&page_size=100`, páginas 1 y 2. Se ordena por `popularity_key` porque con `unique_scans_n` los primeros resultados son productos extranjeros con la etiqueta argentina. Se pide `categories_tags` además de los campos de macros, solo para contar bebidas (tope 10; OFF las da por 100 ml y se guardan como 100 g). Se queda con los primeros 70 que tienen nombre, kcal (o kJ), proteínas, carbohidratos y grasas por 100 g y pasan las reglas. `externalId = barcode = code`, `brand` = primera marca. Si `serving_quantity` está entre 1 y 2000 se agrega la porción.

**Validación.** Las mismas reglas que `FoodValidator` en la API (copiadas en `InvalidReason`): macros ≥ 0, ≤ 900 kcal, P + C + G ≤ 100, largos de columna, `(source, externalId)` único. Redondea a 2 decimales y escribe el JSON ordenado por `source` y `name`.

## Fuentes y licencias

- **ARGENFOODS**: Universidad Nacional de Luján. Tabla de Composición de Alimentos (base ARGENFOODS, proyecto INFOODS/FAO). https://www.argenfood.unlu.edu.ar/ (actualización del sitio: 12/9/2024). Condiciones de uso: todos los derechos son de la UNLu; se autoriza el uso sin fines comerciales, sin alterar los datos y citando autores, titulares y la referencia electrónica completa.
- **Open Food Facts**: datos bajo Open Database License (ODbL), contenidos bajo DbCL. Atribución con link a https://openfoodfacts.org. La base derivada se comparte en las mismas condiciones. Sin garantía y no apto para uso médico.

Fecha de extracción: la imprime el script y queda registrada en D21-seed (`docs/ESTADO.md`).
