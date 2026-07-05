# Propuesta de optimizacion para `api/optimizado`

Referencia de prueba entregada por usuario:

- `CvePlan: 3`
- `CveReferencia: 6`
- `CveParametro: 6`
- `EdadSalida: 70`
- `EdadVenta: 170`
- `Productos` con etapas `5` a `10`

Objetivo de esta propuesta:

- reducir trabajo repetido dentro de los ciclos anidados del endpoint `[Route("api/optimizado")]`
- mantener la logica funcional actual
- dejar la optimizacion separada para comparacion antes de aplicarla

## Cambios propuestos

### 1. Cache de productos por clave y por posicion

Antes se hacian muchas llamadas como estas dentro del endpoint:

```csharp
objReqDataIni.Productos.Find(p => p.CveProducto == 6);
objReqDataIni.Productos.Find(r => r.Posicion == 8);
```

La propuesta es precalcular:

```csharp
var productosByCve = GetProductosByKey(objReqDataIni.Productos, p => p.CveProducto);
var productosByPosicion = GetProductosByKey(objReqDataIni.Productos, p => p.Posicion);
```

Con esto, en los loops se reemplaza `Find(...)` por accesos O(1):

```csharp
ProductoModel prod6 = productosByCve[6];
productosByPosicion[8].PesoFinal = tModel8.Peso_Final;
```

### 2. Cache de duraciones restantes para evitar `FindAll(...).Sum(...)`

Antes, en cada iteracion de `y` y `z`, el codigo recalculaba:

```csharp
objReqDataIni.Productos.FindAll(t => t.CveProducto > 7).Sum(t => t.DuracionMin);
objReqDataIni.Productos.FindAll(t => t.CveProducto > 8).Sum(t => t.DuracionMax);
```

La propuesta es precalcularlo una vez:

```csharp
var remainingMinDurations = GetRemainingDurationsByProduct(objReqDataIni.Productos, p => p.DuracionMin);
var remainingMaxDurations = GetRemainingDurationsByProduct(objReqDataIni.Productos, p => p.DuracionMax);
```

Y luego usar:

```csharp
double edadTope7 = objReqDataIni.EdadVenta - remainingMinDurations[7];
double edadTope8 = objReqDataIni.EdadVenta - remainingMinDurations[8];
double edadTope8Min = objReqDataIni.EdadVenta - remainingMaxDurations[8];
```

### 3. Calculo de `step` centralizado

Antes:

```csharp
double step6 = 1;
double pDif6 = Math.Round(valMinMax6.ValorMaximo - valMinMax6.ValorMinimo) / intervalStep;
if (pDif6 > 0) step6 = pDif6;
```

Propuesta:

```csharp
double step6 = GetIterationStep(valMinMax6, intervalStep);
double step7 = GetIterationStep(valMinMax7, intervalStep);
double step8 = GetIterationStep(valMinMax8, intervalStep);
double step9 = GetIterationStep(valMinMax9, intervalStep);
```

### 4. Seleccion mas eficiente de mejores resultados

Antes se recorria `lstResult` multiples veces con combinaciones de `Find`, `Max` y `Min`:

```csharp
lstResult.Find(p => p.Optimizer.Find(t => t.Orden == 1).Valor == lstResult.Max(r => r.Optimizer.Find(t => t.Orden == 1).Valor));
```

Propuesta:

```csharp
ResponseOptModel objRespModelOpt1 = GetBestResponse(lstResult, 1, true);
ResponseOptModel objRespModelOpt2 = GetBestResponse(lstResult, 2, false);
ResponseOptModel objRespModelOpt3 = GetBestResponse(lstResult, 3, false);
ResponseOptModel objRespModelOpt4 = GetBestResponse(lstResult, 4, false);
ResponseOptModel objRespModelOpt5 = GetBestResponse(lstResult, 5, true);
ResponseOptModel objRespModelOpt6 = GetBestResponse(lstResult, 6, true);
```

### 5. Limpieza de codigo muerto

Se detectaron variables sin uso real en el flujo del endpoint:

```csharp
VarMinMaxModel valMinMax10 = GetMinMaxValues(...);
ParallelOptions myOptions = new ParallelOptions();
string yatenemosalgo = "";
if (y > 945.30) { String algo = "espera"; }
```

La propuesta es retirarlas para mejorar lectura y mantenimiento.

## Helpers nuevos propuestos

```csharp
private static Dictionary<int, ProductoModel> GetProductosByKey(IEnumerable<ProductoModel> productos, Func<ProductoModel, int> keySelector)
{
    return productos.ToDictionary(keySelector);
}

private static Dictionary<int, double> GetRemainingDurationsByProduct(IEnumerable<ProductoModel> productos, Func<ProductoModel, double> selector)
{
    List<ProductoModel> orderedProducts = productos.OrderByDescending(p => p.CveProducto).ToList();
    Dictionary<int, double> remainingDurations = new Dictionary<int, double>();
    double accumulated = 0;

    foreach (ProductoModel producto in orderedProducts)
    {
        remainingDurations[producto.CveProducto] = accumulated;
        accumulated += selector(producto);
    }

    return remainingDurations;
}

private static double GetIterationStep(VarMinMaxModel values, double intervalStep)
{
    double step = Math.Round(values.ValorMaximo - values.ValorMinimo) / intervalStep;
    return step > 0 ? step : 1;
}

private static ResponseOptModel GetBestResponse(IEnumerable<ResponseOptModel> results, int order, bool selectMax)
{
    ResponseOptModel? bestResponse = null;
    double bestValue = selectMax ? double.MinValue : double.MaxValue;

    foreach (ResponseOptModel result in results)
    {
        OptimizerModel optimizerValue = result.Optimizer.First(t => t.Orden == order);
        if ((selectMax && optimizerValue.Valor > bestValue) || (!selectMax && optimizerValue.Valor < bestValue))
        {
            bestValue = optimizerValue.Valor;
            bestResponse = result;
        }
    }

    return bestResponse ?? throw new Exception($"No se encontro resultado para el parametro {order}");
}
```

## Impacto esperado

- menos costo por iteracion en los loops `k/x/y/z`
- menos busquedas lineales sobre `Productos`
- menor presion de CPU en requests como el que compartiste
- mismo contrato del endpoint y misma estructura de respuesta

## Estado actual

- El archivo original [CalculoOptimizadoController.cs](M:/Proyectos/MBRAVO/wsoptimizer7/Controllers/CalculoOptimizadoController.cs) fue restaurado a su estado previo.
- Esta propuesta se dejo solo como comparativo en este documento.
