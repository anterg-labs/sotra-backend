# Comparador de proveedores de IA

Herramienta de consola para elegir el proveedor externo que redacta las sugerencias técnicas. Compara velocidad, costo y calidad con los mismos textos que usa la API.

## Qué mide

Para cada caso de `casos.json`:

1. Arma las secciones de los PDF del corpus con el mismo extractor de la API (`DocumentoPdf`, `SeccionesCandidatas`).
2. Elige la sección del caso: la que coincide con `seccion`, o si no hay pista, la que más palabras comparte con la consulta. No usa embeddings ni base de datos, porque lo que se compara es solo la redacción.
3. Envía a cada proveedor las mismas instrucciones y el mismo mensaje que arma `RedaccionDeSugerencia` en producción.
4. Por cada respuesta registra:
   - el tiempo de respuesta;
   - los tokens de entrada y salida;
   - si pasa `EsResumenConfiable`, el mismo control de números de la API;
   - si conserva los `valoresEsperados`.

El informe en Markdown se guarda en `ComparadorIa/resultados/` (excluida de git), sin importar desde qué carpeta se ejecute. Incluye una tabla resumen por proveedor y, para cada caso, el resumen sin IA y el texto de cada proveedor.

## Cómo ejecutarlo

Las claves se leen de variables de entorno; los proveedores sin clave se omiten. Cada proveedor necesita una cuenta y una clave propias:

- **Claude Haiku 5.5:** `ANTHROPIC_API_KEY`, de console.anthropic.com.
- **Gemini 3.5 Flash-Lite:** `GEMINI_API_KEY`, de aistudio.google.com. En la capa gratuita Google puede usar los datos; no pasarle manuales reales de la empresa.
- **Groq GPT-OSS 20B:** `GROQ_API_KEY`, de console.groq.com.

En PowerShell:

```powershell
$env:ANTHROPIC_API_KEY = "..."
$env:GEMINI_API_KEY = "..."
$env:GROQ_API_KEY = "..."
dotnet run --project ComparadorIa -- --corpus "<ruta>\sotra-test-automation\herramientas-previas\ia\corpus"
```

El corpus de evaluación vive en `sotra-test-automation/herramientas-previas/ia/corpus`: pasá la ruta donde lo tengas.

Opciones:

| Opción | Por defecto | Uso |
|---|---|---|
| `--corpus <carpeta>` | (obligatoria) | Carpeta con los PDF |
| `--casos <archivo>` | `casos.json` | Consultas a evaluar |
| `--proveedores <archivo>` | `proveedores.json` | Proveedores, modelos y precios |
| `--repeticiones <n>` | 3 | Pedidos por caso y proveedor, para medir la variación del tiempo |
| `--tiempo-maximo <seg>` | 30 | Tiempo tras el cual el pedido cuenta como fallido |
| `--salida <carpeta>` | `ComparadorIa/resultados` | Dónde se guarda el informe |

Sin ninguna clave, la herramienta igual corre y muestra qué sección eligió para cada caso. Sirve para revisar `casos.json` antes de gastar en pedidos.

## Antes de la primera corrida

- **Revisar `casos.json`.** Los casos iniciales salen de los valores que verifica `CorpusDeEvaluacionIaPruebas` ("ISO VG 46", "15W-40", "250 horas", "25 Nm", "bloquee y etiquete"). Hay que confirmar contra los PDF que cada consulta apunta a la sección correcta y agregar casos reales de técnicos.
- **Confirmar precios y modelos en `proveedores.json`.** Los precios salen de comparadores de terceros (octubre de 2026). Para sumar otro proveedor compatible con OpenAI (Mistral, DeepSeek, Ollama, etc.) alcanza con una entrada nueva de `"tipo": "openai"`.
- **Costo de una corrida:** 6 casos × 3 repeticiones × 3 proveedores son 54 pedidos de unos 1.500 tokens cada uno. Con los precios cargados, cuesta menos de un centavo de dólar en total.

## Cómo leer el resultado

- **Confiable:** porcentaje de respuestas que la API mostraría. El resto se reemplaza por el resumen sin IA.
- **Valores conservados:** de los datos clave del manual, cuántos copió el modelo tal cual.
- **Mediana y máximo:** tiempo de un pedido completo desde este equipo. En el VPS puede variar por la red.
- **USD cada 1000 consultas:** estimación con los tokens reales y los precios del archivo. En modelos que razonan (Claude Haiku 5.5 con `esfuerzo`, GPT-OSS con `reasoning_effort`), el razonamiento se cobra como salida.

Además de los números, conviene leer los textos del detalle: el control de números no detecta un paso inventado sin cifras ni una redacción confusa.
