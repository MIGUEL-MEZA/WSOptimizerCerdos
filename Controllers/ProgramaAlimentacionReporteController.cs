using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using ExpertPdf.HtmlToPdf;
using Newtonsoft.Json;
using WSOptimizer7.App_Data;
using WSOptimizer7.Models;

namespace WSOptimizer7.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;

    public class ProgramaAlimentacionReporteController : Controller
    {
        private static readonly XLColor ExcelDarkBlue = XLColor.FromHtml("#0b2e57");
        private static readonly XLColor ExcelLightBlue = XLColor.FromHtml("#6084d7");
        private static readonly XLColor ExcelGridBlue = XLColor.FromHtml("#d6deed");
        private static readonly XLColor ExcelCategoryBlue = XLColor.FromHtml("#dce9f5");
        private static readonly XLColor ExcelAlternateRow = XLColor.FromHtml("#eef2f8");
        private static readonly CultureInfo ReportNumberCulture = CultureInfo.InvariantCulture;

        private readonly IConfiguration configuration;

        public ProgramaAlimentacionReporteController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        [HttpGet]
        [Route("api/reportes/programaalimentacion/{id}/excel")]
        public IActionResult GetProgramaAlimentacionExcel(long id, [FromQuery] string? seccion = null)
        {
            try
            {
                ProgramaReporteModel reporte = GetReporte(id, seccion);
                byte[] bytes = GenerateExcelBytes(reporte);
                string fileName = $"ProgramaAlimentacion_{id}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                return BadRequest("Error generando el archivo Excel: " + ex.Message);
            }
        }

        [HttpGet]
        [Route("api/reportes/programaalimentacion/{id}/pdf")]
        public IActionResult GetProgramaAlimentacionPdf(long id, [FromQuery] string? seccion = null)
        {
            try
            {
                ProgramaReporteModel reporte = GetReporte(id, seccion);
                byte[] bytes = GeneratePdfBytes(reporte);
                string fileName = $"ProgramaAlimentacion_{id}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

                return File(bytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest("Error generando el archivo PDF: " + ex.Message);
            }
        }

        private ProgramaReporteModel GetReporte(long id, string? seccion)
        {
            if (id <= 0)
            {
                throw new Exception("El id del programa no es valido.");
            }

            string seccionNormalizada = NormalizeSeccion(seccion);
            PlanAContextModel contexto = GetPlanAContext(id, seccionNormalizada);

            return string.Equals(seccionNormalizada, "comparativo", StringComparison.OrdinalIgnoreCase)
                ? BuildComparativoReporte(contexto)
                : BuildPresupuestoReporte(contexto);
        }

        private static string NormalizeSeccion(string? seccion)
        {
            if (string.IsNullOrWhiteSpace(seccion))
            {
                return "presupuesto";
            }

            string value = seccion.Trim().ToLowerInvariant();
            return value == "comparativo" ? "comparativo" : "presupuesto";
        }

        private static PlanAContextModel GetPlanAContext(long id, string seccion)
        {
            DataRow rowResultado = GetPlanAResultadoRow(id);
            DataRow rowPlanA = GetPlanARow(id);
            DataRow? rowCliente = GetClienteRow(rowPlanA);
            RespOptimizerModel response = GetResponse(rowResultado);
            List<PlanAEtapaModel> etapas = GetPlanAEtapas(rowPlanA, id);

            return new PlanAContextModel
            {
                CvePlan = id,
                Seccion = seccion,
                PlanARow = rowPlanA,
                ResultadoRow = rowResultado,
                ClienteRow = rowCliente,
                Response = response,
                Etapas = etapas
            };
        }

        private static DataRow GetPlanAResultadoRow(long id)
        {
            string sql = "SELECT * FROM [OptimizerC_PlanA_Resultado] WHERE CvePlan = " + id.ToString(CultureInfo.InvariantCulture);

            DataTable dt = Database.execQuery(sql);
            if (dt == null || dt.Rows.Count == 0)
            {
                throw new Exception("No se encontraron datos del resultado para el programa indicado.");
            }

            return dt.Rows[0];
        }

        private static DataRow GetPlanARow(long id)
        {
            string sql =
                "SELECT PA.*, " +
                "P.FolioR AS FolioRPN, " +
                "R.NomReferencia " +
                "FROM [OptimizerC_PlanA] PA " +
                "INNER JOIN [OptimizerC_PerfilN] P ON P.CvePerfilN = PA.CvePerfilN " +
                "INNER JOIN [CatOptimizerC_Referencias] R ON R.CveReferencia = PA.CveReferencia " +
                "WHERE PA.CvePlan = " + id.ToString(CultureInfo.InvariantCulture);

            DataTable dt = Database.execQuery(sql);
            if (dt == null || dt.Rows.Count == 0)
            {
                throw new Exception("No se encontraron datos del programa indicado.");
            }

            return dt.Rows[0];
        }

        private static DataRow? GetClienteRow(DataRow rowPrograma)
        {
            if (!rowPrograma.Table.Columns.Contains("CodCliente") || rowPrograma["CodCliente"] == DBNull.Value)
            {
                return null;
            }

            string codCliente = rowPrograma["CodCliente"]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(codCliente))
            {
                return null;
            }

            string sql = "SELECT * FROM [Clientes] WHERE CodCliente = '" + codCliente.Replace("'", "''") + "'";
            DataTable dt = Database.execQuery(sql);
            if (dt == null || dt.Rows.Count == 0)
            {
                return null;
            }

            return dt.Rows[0];
        }

        private static RespOptimizerModel GetResponse(DataRow row)
        {
            string responseJson = row["Response"]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                throw new Exception("El programa no contiene informacion en Response.");
            }

            RespOptimizerModel? response = JsonConvert.DeserializeObject<RespOptimizerModel>(responseJson);
            if (response == null)
            {
                throw new Exception("No fue posible leer el Response del programa.");
            }

            return response;
        }

        private static string GetClienteReporte(DataRow rowPrograma, DataRow? rowCliente)
        {
            string nombreClienteReporte = GetTrimmedValue(rowCliente, "NomClienteR");
            if (!string.IsNullOrWhiteSpace(nombreClienteReporte))
            {
                return nombreClienteReporte;
            }

            string nombreClienteA = GetTrimmedValue(rowCliente, "NomClienteA");
            string nombreCliente = GetTrimmedValue(rowCliente, "NomCliente");

            if (string.IsNullOrWhiteSpace(nombreClienteA))
            {
                return !string.IsNullOrWhiteSpace(nombreCliente)
                    ? nombreCliente
                    : GetTrimmedValue(rowPrograma, "NomCliente");
            }

            if (string.IsNullOrWhiteSpace(nombreCliente))
            {
                return nombreClienteA;
            }

            if (string.Equals(nombreClienteA, nombreCliente, StringComparison.OrdinalIgnoreCase))
            {
                return nombreClienteA;
            }

            return nombreClienteA + " (" + nombreCliente + ")";
        }

        private static string GetTrimmedValue(DataRow? row, string columnName)
        {
            if (row == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return string.Empty;
            }

            return (row[columnName]?.ToString() ?? string.Empty).Trim();
        }

        private static ProgramaReporteModel BuildPresupuestoReporte(PlanAContextModel contexto)
        {
            ProgramaReporteModel reporte = CreateBaseReporte(contexto);
            ResponseOptimizerModel? parametro = GetParametroSeleccionado(contexto);

            if (parametro == null)
            {
                throw new Exception("No se encontraron datos del parametro seleccionado para el programa.");
            }

            List<PlanAEtapaModel> etapasAplicadas = contexto.Etapas
                .Where(e => string.Equals(e.Aplica, "S", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.OrdenVisual)
                .ToList();

            List<TablaModel> datosOrdenados = parametro.Data
                .OrderBy(d => d.Identificador)
                .ToList();

            int coincidenciasPorOrdenVisual = datosOrdenados.Count(d => etapasAplicadas.Any(e => e.OrdenVisual == d.Identificador));
            bool usarMapeoSecuencial = coincidenciasPorOrdenVisual < Math.Min(etapasAplicadas.Count, datosOrdenados.Count);

            List<TablaModel> datosPresupuesto = usarMapeoSecuencial
                ? datosOrdenados.Take(etapasAplicadas.Count).ToList()
                : datosOrdenados.Where(d => etapasAplicadas.Any(e => e.OrdenVisual == d.Identificador)).ToList();

            reporte.PresupuestoFilas = datosPresupuesto
                .Select((d, index) =>
                {
                    PlanAEtapaModel? etapa = usarMapeoSecuencial
                        ? etapasAplicadas.ElementAtOrDefault(index)
                        : etapasAplicadas.FirstOrDefault(e => e.OrdenVisual == d.Identificador);

                    return new ProgramaPresupuestoFilaModel
                    {
                        CveEtapa = etapa?.CveEtapa ?? d.Identificador,
                        NomEtapa = ResolveProgramaStageName(etapa),
                        Costo = d.Costo,
                        Cda = d.CDA_Kg,
                        PresupuestoCerdo = d.PresupuestoCerdo,
                        Gdp = d.GDP,
                        PesoInicial = d.Peso_Inicial,
                        PesoFinal = d.Peso_Final,
                        Ca = d.CA,
                        EdadInicial = Math.Round(d.Edad_Inicial, 0),
                        EdadFinal = Math.Round(d.Edad_Final, 0),
                        DuracionEtapa = Math.Round(Math.Round(d.Edad_Final, 0) - Math.Round(d.Edad_Inicial, 0), 0)
                    };
                })
                .ToList();

            reporte.PresupuestoResumen = new List<ProgramaResumenItemModel>
            {
                new ProgramaResumenItemModel("PRECIO DE VENTA ($/Kg):", parametro.Resultado.PrecioVenta, true, "N2"),
                new ProgramaResumenItemModel("PESO DE VENTA (Kg):", parametro.Resultado.PesoVenta, "N2"),
                new ProgramaResumenItemModel("EDAD DE VENTA (días):", parametro.Resultado.EdadVenta, "N0"),
                new ProgramaResumenItemModel("ALIMENTO PRESUPUESTADO (Kg):", parametro.Resultado.Presupuesto, "N1"),
                new ProgramaResumenItemModel("KILOS PRODUCIDOS (Kg):", parametro.Resultado.KilosProducidos, "N2"),
                new ProgramaResumenItemModel("GDP (Kg):", parametro.Resultado.Gdp, "N2"),
                new ProgramaResumenItemModel("CONVERSIÓN ALIMENTICIA:", parametro.Resultado.Ca, "N2"),
                new ProgramaResumenItemModel("COSTO TOTAL DEL ALIMENTO ($):", parametro.Resultado.CostoTotalAlimento, true, "N2"),
                new ProgramaResumenItemModel("COSTO PONDERADO DE ALIMENTO ($):", parametro.Resultado.CostoPonderado, true, "N2"),
                new ProgramaResumenItemModel("COSTO POR KILO PRODUCIDO, ALIMENTO ($):", parametro.Resultado.CostoKiloProducido, true, "N2"),
                new ProgramaResumenItemModel("UTILIDAD POR CONCEPTO DE ALIMENTO ($):", parametro.Resultado.Utilidad, true, "N2"),
                new ProgramaResumenItemModel("ROI, ALIMENTO (%):", parametro.Resultado.Roi, "N2")
            };

            reporte.PresupuestoTotales = new ProgramaPresupuestoTotalesModel
            {
                Cda = parametro.Resultado.Cda,
                PresupuestoCerdo = parametro.Resultado.Presupuesto,
                Gdp = parametro.Resultado.Gdp,
                Ca = parametro.Resultado.Ca
            };

            return reporte;
        }

        private static ProgramaReporteModel BuildComparativoReporte(PlanAContextModel contexto)
        {
            ProgramaReporteModel reporte = CreateBaseReporte(contexto);
            List<ResponseOptimizerModel> parametros = GetComparativoParametros(contexto);
            if (parametros.Count == 0)
            {
                return reporte;
            }

            List<PlanAEtapaModel> etapasAplicadas = contexto.Etapas
                .Where(e => string.Equals(e.Aplica, "S", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.OrdenVisual)
                .ToList();

            reporte.ComparativoColumnas = new List<string> { string.Empty };
            reporte.ComparativoColumnas.AddRange(parametros.Select(GetComparativoParametroTitulo));

            reporte.ComparativoPresupuestos = etapasAplicadas
                .Select(etapa =>
                {
                    string etiqueta = ResolveProgramaStageName(etapa);
                    List<string> valores = parametros
                        .Select(parametro =>
                        {
                            TablaModel? dato = parametro.Data
                                .FirstOrDefault(d => d.Identificador == etapa.CveEtapa);

                            return dato == null
                                ? string.Empty
                                : dato.PresupuestoCerdo.ToString(CultureInfo.InvariantCulture);
                        })
                        .ToList();

                    return new ProgramaComparativoFilaModel
                    {
                        Etiqueta = etiqueta,
                        Valores = FormatComparativoValores(
                            etiqueta,
                            valores,
                            reporte.ComparativoColumnas.Skip(1).ToList(),
                            "presupuestos"),
                        Visible = HasVisibleComparativoRow(etiqueta, valores)
                    };
                })
                .Where(r => r.Visible)
                .ToList();

            reporte.ComparativoPresupuestosTotales = BuildComparativoTotales(
                reporte.ComparativoPresupuestos,
                reporte.ComparativoColumnas.Skip(1).ToList(),
                "presupuestos");

            reporte.ComparativoVariables = BuildComparativoVariables(parametros, reporte.ComparativoColumnas.Skip(1).ToList());

            return reporte;
        }

        private static List<ProgramaComparativoFilaModel> BuildComparativoVariables(
            List<ResponseOptimizerModel> parametros,
            List<string> columnas)
        {
            List<(string Etiqueta, Func<ResultadoOptimizerModel, double> Selector)> definiciones = new()
            {
                ("PrecioVenta", r => r.PrecioVenta),
                ("PesoVenta", r => r.PesoVenta),
                ("EdadVenta", r => r.EdadVenta),
                ("Presupuesto", r => r.Presupuesto),
                ("KilosProducidos", r => r.KilosProducidos),
                ("GDP", r => r.Gdp),
                ("CA", r => r.Ca),
                ("Costo_TotalAlimento", r => r.CostoTotalAlimento),
                ("Costo_Ponderado", r => r.CostoPonderado),
                ("Costo_KiloProducido", r => r.CostoKiloProducido),
                ("Utilidad", r => r.Utilidad),
                ("ROI", r => r.Roi)
            };

            return definiciones
                .Select(definicion =>
                {
                    List<string> valores = parametros
                        .Select(parametro => definicion.Selector(parametro.Resultado).ToString(CultureInfo.InvariantCulture))
                        .ToList();

                    return new ProgramaComparativoFilaModel
                    {
                        Etiqueta = definicion.Etiqueta,
                        Valores = FormatComparativoValores(
                            definicion.Etiqueta,
                            valores,
                            columnas,
                            "variables"),
                        Visible = HasVisibleComparativoRow(definicion.Etiqueta, valores)
                    };
                })
                .Where(r => r.Visible)
                .ToList();
        }

        private static ProgramaReporteModel CreateBaseReporte(PlanAContextModel contexto)
        {
            return new ProgramaReporteModel
            {
                CvePlan = contexto.CvePlan,
                Seccion = contexto.Seccion,
                Folio = contexto.PlanARow.Table.Columns.Contains("FolioR")
                    ? contexto.PlanARow["FolioR"]?.ToString() ?? string.Empty
                    : string.Empty,
                Referencia = contexto.PlanARow.Table.Columns.Contains("NomReferencia")
                    ? contexto.PlanARow["NomReferencia"]?.ToString() ?? string.Empty
                    : string.Empty,
                Cliente = GetClienteReporte(contexto.PlanARow, contexto.ClienteRow),
                FechaEmision = DateTime.Now
            };
        }

        private static ResponseOptimizerModel? GetParametroSeleccionado(PlanAContextModel contexto)
        {
            int cveParametro = contexto.PlanARow.Table.Columns.Contains("CveParametro") && contexto.PlanARow["CveParametro"] != DBNull.Value
                ? Convert.ToInt32(contexto.PlanARow["CveParametro"])
                : 0;

            return contexto.Response.ResponseParametro.FirstOrDefault(p => p.CveParametro == cveParametro)
                ?? contexto.Response.ResponseParametro.FirstOrDefault();
        }

        private static List<ResponseOptimizerModel> GetComparativoParametros(PlanAContextModel contexto)
        {
            ResponseOptimizerModel? seleccionado = GetParametroSeleccionado(contexto);
            IEnumerable<ResponseOptimizerModel> parametros = contexto.Response.ResponseParametro;

            if (seleccionado == null)
            {
                return parametros
                    .OrderBy(p => p.CveParametro)
                    .ToList();
            }

            return parametros
                .OrderBy(p => p.CveParametro == seleccionado.CveParametro ? 0 : 1)
                .ThenBy(p => p.CveParametro)
                .ToList();
        }

        private static string GetComparativoParametroTitulo(ResponseOptimizerModel parametro)
        {
            string titulo = (parametro.Parametro ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(titulo))
            {
                return titulo;
            }

            return "PARAMETRO " + parametro.CveParametro.ToString(CultureInfo.InvariantCulture);
        }

        private static List<PlanAEtapaModel> GetPlanAEtapas(DataRow rowPrograma, long id)
        {
            string codCliente = GetTrimmedValue(rowPrograma, "CodCliente");
            string sql =
                "SELECT " +
                "ISNULL(PP.CvePlan, 0) AS CvePlan, " +
                "tbl.CveProducto AS CveEtapa, " +
                "ISNULL(tbl.NomProducto, '') AS NomEtapa, " +
                "ISNULL(PP.Aplica, 'S') AS Aplica, " +
                "ISNULL(tbl.Posicion, ISNULL(tbl.CveProducto, 0)) AS OrdenVisual " +
                "FROM [CatOptimizerC_Productos] tbl " +
                "LEFT JOIN [OptimizerC_PlanA] P ON P.CvePlan = " + id.ToString(CultureInfo.InvariantCulture) +
                " AND P.CodCliente = '" + codCliente.Replace("'", "''") + "' " +
                "LEFT JOIN [OptimizerC_PlanA_Productos] PP ON PP.CveProducto = tbl.CveProducto" +
                " AND PP.CvePlan = " + id.ToString(CultureInfo.InvariantCulture) + " " +
                "LEFT JOIN [OptimizerC_PerfilN_Etapas] Et ON Et.CvePerfilN = P.CvePerfilN AND Et.CveEtapa + 4 = tbl.CveProducto " +
                "ORDER BY ISNULL(tbl.Posicion, ISNULL(tbl.CveProducto, 0))";

            DataTable dt = Database.execQuery(sql);
            if (dt == null || dt.Rows.Count == 0)
            {
                return new List<PlanAEtapaModel>();
            }

            return dt.AsEnumerable()
                .Select((r, index) => new PlanAEtapaModel
                {
                    CveEtapa = Convert.ToInt32(r["CveEtapa"]),
                    NomEtapa = r["NomEtapa"]?.ToString() ?? string.Empty,
                    Aplica = r["Aplica"]?.ToString() ?? string.Empty,
                    OrdenVisual = SafeToInt(r["OrdenVisual"]) == int.MaxValue ? index + 1 : SafeToInt(r["OrdenVisual"])
                })
                .ToList();
        }

        private static List<ProgramaComparativoColumnaModel> GetComparativoColumnas(long id)
        {
            string sql = BuildReportesColumnasSql(41, 3, id);
            DataTable dt = Database.execQuery(sql);
            if (dt == null || dt.Rows.Count == 0)
            {
                return new List<ProgramaComparativoColumnaModel>();
            }

            IEnumerable<DataRow> orderedRows = dt.AsEnumerable();
            if (dt.Columns.Contains("Posicion"))
            {
                orderedRows = orderedRows.OrderBy(r => SafeToInt(r["Posicion"]));
            }
            else if (dt.Columns.Contains("CveControl"))
            {
                orderedRows = orderedRows.OrderBy(r => SafeToInt(r["CveControl"]));
            }

            return orderedRows
                .Select((r, index) => new ProgramaComparativoColumnaModel
                {
                    Campo = GetStringColumn(r, "Campo", string.Empty),
                    Titulo = index == 0 ? string.Empty : GetStringColumn(r, "Titulo", GetStringColumn(r, "Campo", string.Empty)),
                    Posicion = dt.Columns.Contains("Posicion") ? SafeToInt(r["Posicion"]) : index + 1
                })
                .ToList();
        }

        private static List<List<string>> GetComparativoDatos(int cveMenu, long id, List<ProgramaComparativoColumnaModel> columnas)
        {
            string sql = BuildReportesDatosSql(41, cveMenu, id.ToString(CultureInfo.InvariantCulture));
            DataTable dt = Database.execQuery(sql);
            if (dt == null || dt.Rows.Count == 0)
            {
                return new List<List<string>>();
            }

            List<ProgramaComparativoColumnaModel> columnasVisibles = columnas
                .Where(c => !string.IsNullOrWhiteSpace(c.Campo))
                .ToList();

            return dt.AsEnumerable()
                .Select(row => columnasVisibles
                    .Select(col => dt.Columns.Contains(col.Campo)
                        ? FormatDataValue(row[col.Campo], col.Campo)
                        : string.Empty)
                    .ToList())
                .ToList();
        }

        private static List<string> BuildComparativoTotales(
            List<ProgramaComparativoFilaModel> filas,
            List<string> columnas,
            string seccion)
        {
            if (filas.Count == 0)
            {
                return new List<string>();
            }

            int totalColumnas = filas.Max(f => f.Valores.Count);
            List<string> totales = new List<string>();
            for (int i = 0; i < totalColumnas; i++)
            {
                double total = 0;
                foreach (ProgramaComparativoFilaModel fila in filas)
                {
                    if (i < fila.Valores.Count && TryParseDisplayNumber(fila.Valores[i], out double valor))
                    {
                        total += valor;
                    }
                }

                bool esMoneda = i < columnas.Count && IsComparativoCurrency(seccion, string.Empty, columnas[i]);
                totales.Add(FormatDisplayNumber(total, esMoneda));
            }

            return totales;
        }

        private static bool HasVisibleComparativoRow(string etiqueta, List<string> valores)
        {
            if (string.IsNullOrWhiteSpace(etiqueta))
            {
                return false;
            }

            return valores.Any(v => !string.IsNullOrWhiteSpace(v));
        }

        private static List<string> FormatComparativoValores(
            string etiqueta,
            List<string> valores,
            List<string> columnas,
            string seccion)
        {
            List<string> resultado = new List<string>();
            for (int i = 0; i < valores.Count; i++)
            {
                string columna = i < columnas.Count ? columnas[i] : string.Empty;
                bool esMoneda = IsComparativoCurrency(seccion, etiqueta, columna);

                if (TryParseDisplayNumber(valores[i], out double numero))
                {
                    resultado.Add(FormatDisplayNumber(numero, esMoneda));
                }
                else
                {
                    resultado.Add(valores[i]);
                }
            }

            return resultado;
        }

        private static bool IsComparativoCurrency(string seccion, string etiqueta, string columna)
        {
            string seccionNormalizada = (seccion ?? string.Empty).Trim().ToLowerInvariant();
            string etiquetaNormalizada = NormalizeToken(etiqueta);
            string columnaNormalizada = NormalizeToken(columna);

            if (seccionNormalizada == "presupuestos")
            {
                return columnaNormalizada.Contains("COSTO");
            }

            return etiquetaNormalizada == "PRECIOVENTA"
                || etiquetaNormalizada == "COSTOTOTALALIMENTO"
                || etiquetaNormalizada == "COSTOTOTALDELALIMENTO"
                || etiquetaNormalizada == "COSTOPONDERADO"
                || etiquetaNormalizada == "COSTOPONDERADODEALIMENTO"
                || etiquetaNormalizada == "COSTOKILOPRODUCIDO"
                || etiquetaNormalizada == "COSTOPORKILOPRODUCIDOALIMENTO"
                || etiquetaNormalizada == "UTILIDAD";
        }

        private static string NormalizeToken(string? value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .ToUpperInvariant();
        }

        private static bool TryParseDisplayNumber(string? value, out double numero)
        {
            string limpio = (value ?? string.Empty)
                .Replace("$", string.Empty)
                .Replace(",", string.Empty)
                .Trim();

            return double.TryParse(
                limpio,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out numero);
        }

        private static string FormatDisplayNumber(double value, bool asCurrency)
        {
            return asCurrency
                ? FormatCurrency(value)
                : value.ToString("N2", ReportNumberCulture);
        }

        private static string BuildReportesColumnasSql(int cvePlataforma, int cveMenu, long id)
        {
            return " DECLARE @CvePlataforma int=" + cvePlataforma.ToString(CultureInfo.InvariantCulture) +
                   " DECLARE @CveMenu int=" + cveMenu.ToString(CultureInfo.InvariantCulture) +
                   " DECLARE @Id bigint=" + id.ToString(CultureInfo.InvariantCulture) +
                   " DECLARE @Estatus int=0" +
                   " DECLARE @Mensaje varchar(250)=''" +
                   " EXEC spp_Reportes_Columnas @CvePlataforma,@CveMenu,@Id,@Estatus Output,@Mensaje Output";
        }

        private static string BuildReportesDatosSql(int cvePlataforma, int cveMenu, string filtros)
        {
            return " DECLARE @CvePlataforma int=" + cvePlataforma.ToString(CultureInfo.InvariantCulture) +
                   " DECLARE @CveMenu int=" + cveMenu.ToString(CultureInfo.InvariantCulture) +
                   " DECLARE @Filtros varchar(MAX) ='" + filtros + "'" +
                   " DECLARE @Estatus int=0" +
                   " DECLARE @Mensaje varchar(250)=''" +
                   " EXEC spp_Reportes_Datos @CvePlataforma,@CveMenu,@Filtros,@Estatus Output,@Mensaje Output";
        }

        private static string FormatDataValue(object value, string columnName)
        {
            if (value == DBNull.Value || value == null)
            {
                return string.Empty;
            }

            if (!double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out double numericValue))
            {
                return value.ToString() ?? string.Empty;
            }

            return columnName switch
            {
                "Titulo" => value.ToString() ?? string.Empty,
                _ => numericValue.ToString(CultureInfo.InvariantCulture)
            };
        }

        private byte[] GenerateExcelBytes(ProgramaReporteModel reporte)
        {
            string templatePath = GetDesignPath("Nuptimizer-PerfilNutricional.xlsx");
            if (!System.IO.File.Exists(templatePath))
            {
                throw new Exception("No se encontro la plantilla base de Excel.");
            }

            using XLWorkbook workbook = new XLWorkbook(templatePath);
            IXLWorksheet worksheet = workbook.Worksheet(1);

            if (string.Equals(reporte.Seccion, "comparativo", StringComparison.OrdinalIgnoreCase))
            {
                BuildComparativoExcel(worksheet, reporte);
            }
            else
            {
                BuildPresupuestoExcel(worksheet, reporte);
            }

            using MemoryStream stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private byte[] GeneratePdfBytes(ProgramaReporteModel reporte)
        {
            string licenseKey = configuration["ExpertPdf:LicenseKey"] ?? throw new Exception("No se encontro la licencia de ExpertPdf en la configuracion.");
            PdfConverter pdf = new PdfConverter
            {
                LicenseKey = licenseKey
            };

            pdf.PdfDocumentOptions.EmbedFonts = true;
            pdf.PdfDocumentOptions.GenerateSelectablePdf = true;
            pdf.PdfDocumentOptions.PdfPageSize = PdfPageSize.Letter;
            pdf.PdfDocumentOptions.FitWidth = true;
            pdf.PdfDocumentOptions.FitHeight = false;
            pdf.PdfDocumentOptions.TopMargin = 5;
            pdf.PdfDocumentOptions.BottomMargin = 5;
            pdf.PdfDocumentOptions.LeftMargin = 10;
            pdf.PdfDocumentOptions.RightMargin = 10;
            pdf.PdfDocumentOptions.PdfPageOrientation = PDFPageOrientation.Landscape;
            pdf.PdfDocumentOptions.ShowHeader = true;
            pdf.PdfDocumentOptions.ShowFooter = true;
            pdf.PdfHeaderOptions.DrawHeaderLine = false;
            pdf.PdfHeaderOptions.HtmlToPdfArea = new HtmlToPdfArea(
                BuildHeaderHtml(reporte),
                GetTemplatePath("perfil_nutricional_header.html"));
            pdf.PdfHeaderOptions.HeaderHeight = 115;
            pdf.PdfFooterOptions.DrawFooterLine = false;
            pdf.PdfFooterOptions.HtmlToPdfArea = new HtmlToPdfArea(
                BuildFooterHtml(),
                GetTemplatePath("perfil_nutricional_footer.html"));
            pdf.PdfFooterOptions.FooterHeight = 55;
            pdf.PdfFooterOptions.FooterTextColor = Color.Black;
            pdf.PdfFooterOptions.FooterTextFontType = PdfFontType.Helvetica;
            pdf.PdfFooterOptions.FooterTextFontSize = 8;
            pdf.PdfFooterOptions.ShowPageNumber = true;
            pdf.PdfFooterOptions.PageNumberText = "Pagina";
            pdf.PdfFooterOptions.PageNumberTextColor = Color.Black;
            pdf.PdfFooterOptions.PageNumberTextFontType = PdfFontType.Helvetica;
            pdf.PdfFooterOptions.PageNumberTextFontSize = 8;
            pdf.PdfFooterOptions.PageNumberYLocation = 6;

            return pdf.GetPdfBytesFromHtmlString(BuildPdfHtml(reporte));
        }

        private void BuildPresupuestoExcel(IXLWorksheet worksheet, ProgramaReporteModel reporte)
        {
            int lastColumn = 11;
            BuildExcelHeader(worksheet, reporte, "PROGRAMA DE ALIMENTACIÓN", lastColumn);
            int row = 4;

            string[] headers =
            {
                string.Empty,
                "COSTO\n($/kg)",
                "CDA\n(Kg)",
                "PRESUPUESTO POR CERDO\n(Kg)",
                "GDP\n(Kg)",
                "PESO INICIAL\n(Kg)",
                "PESO FINAL\n(Kg)",
                "C.A.",
                "EDAD INICIAL\n(días)",
                "EDAD FINAL\n(días)",
                "DURACIÓN ETAPA\n(días)"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                IXLCell cell = worksheet.Cell(row, i + 1);
                cell.Value = headers[i];
                ApplyHeaderCellStyle(cell, ExcelDarkBlue);
            }

            row++;
            bool alternate = false;
            foreach (ProgramaPresupuestoFilaModel fila in reporte.PresupuestoFilas)
            {
                ApplyPresupuestoRow(worksheet, row, fila, alternate);
                alternate = !alternate;
                row++;
            }

            
            ApplyFooterBand(worksheet.Range(row, 1, row, 11));
            worksheet.Cell(row, 3).Value = reporte.PresupuestoTotales.Cda.ToString("N2", CultureInfo.InvariantCulture);
            worksheet.Cell(row, 4).Value = reporte.PresupuestoTotales.PresupuestoCerdo.ToString("N1", CultureInfo.InvariantCulture);
            worksheet.Cell(row, 5).Value = reporte.PresupuestoTotales.Gdp.ToString("N3", CultureInfo.InvariantCulture);
            worksheet.Cell(row, 8).Value = reporte.PresupuestoTotales.Ca.ToString("N2", CultureInfo.InvariantCulture);
            


            row += 2;

            alternate = false;
            foreach (ProgramaResumenItemModel item in reporte.PresupuestoResumen)
            {
                worksheet.Cell(row, 1).Value = item.Etiqueta;
                worksheet.Cell(row, 2).Value = FormatSummaryValue(item);
                ApplySummaryLabelStyle(worksheet.Cell(row, 1));
                ApplySummaryValueStyle(worksheet.Cell(row, 2), alternate);
                alternate = !alternate;
                row++;
            }

            worksheet.Columns().AdjustToContents();
        }

        private void BuildComparativoExcel(IXLWorksheet worksheet, ProgramaReporteModel reporte)
        {
            int lastColumn = Math.Max(2, reporte.ComparativoColumnas.Count);
            BuildExcelHeader(worksheet, reporte, "PROGRAMA DE ALIMENTACIÓN", lastColumn);
            int row = 4;

            row = WriteComparativoSection(worksheet, row, "PRESUPUESTO POR CERDO", reporte.ComparativoColumnas, reporte.ComparativoPresupuestos, reporte.ComparativoPresupuestosTotales);
            row += 2;
            WriteComparativoSection(worksheet, row, "VARIABLES ECONÓMICAS", reporte.ComparativoColumnas, reporte.ComparativoVariables, null);

            worksheet.Columns().AdjustToContents();
        }

        private static int WriteComparativoSection(IXLWorksheet worksheet, int startRow, string titulo, List<string> columnas, List<ProgramaComparativoFilaModel> filas, List<string>? totales)
        {
            worksheet.Range(startRow, 1, startRow, columnas.Count).Merge();
            worksheet.Cell(startRow, 1).Value = titulo;
            ApplyCategoryBand(worksheet.Range(startRow, 1, startRow, columnas.Count));
            startRow++;
            startRow++;

            for (int i = 0; i < columnas.Count; i++)
            {
                IXLCell cell = worksheet.Cell(startRow, i + 1);
                cell.Value = GetComparativoHeaderLabel(titulo, columnas[i], i);
                ApplyHeaderCellStyle(cell, ExcelDarkBlue);
            }

            startRow++;
            bool alternate = false;
            foreach (ProgramaComparativoFilaModel fila in filas)
            {
                worksheet.Cell(startRow, 1).Value = fila.Etiqueta;
                ApplyRowLabelStyle(worksheet.Cell(startRow, 1), alternate);

                for (int i = 0; i < fila.Valores.Count; i++)
                {
                    worksheet.Cell(startRow, i + 2).Value = fila.Valores[i];
                    ApplyBodyStyle(worksheet.Cell(startRow, i + 2), alternate);
                }

                alternate = !alternate;
                startRow++;
            }

            if (totales != null && totales.Count > 0)
            {
                worksheet.Cell(startRow, 1).Value = string.Empty;
                ApplyFooterBand(worksheet.Range(startRow, 1, startRow, 1));
                for (int i = 0; i < totales.Count; i++)
                {
                    worksheet.Cell(startRow, i + 2).Value = totales[i];
                    ApplyFooterBand(worksheet.Range(startRow, i + 2, startRow, i + 2));
                }

                startRow++;
            }

            return startRow;
        }

        // Deshace cualquier combinacion que cruce el rango indicado, para que las nuevas
        // no queden traslapadas con las que trae la plantilla.
        private static void UnmergeHeaderRange(IXLWorksheet worksheet, int firstRow, int lastRow, int firstColumn, int lastColumn)
        {
            foreach (IXLRange mergedRange in worksheet.MergedRanges
                .Where(range => range.FirstRow().RowNumber() <= lastRow
                    && range.LastRow().RowNumber() >= firstRow
                    && range.FirstColumn().ColumnNumber() <= lastColumn
                    && range.LastColumn().ColumnNumber() >= firstColumn)
                .ToList())
            {
                mergedRange.Unmerge();
            }
        }

        private static void BuildExcelHeader(IXLWorksheet worksheet, ProgramaReporteModel reporte, string titulo, int lastColumn)
        {
            // La plantilla trae la banda partida en bloques combinados: combinar encima
            // sin deshacerlos primero deja rangos traslapados y Excel abre el archivo
            // pidiendo repararlo.
            UnmergeHeaderRange(worksheet, 1, 3, 1, lastColumn);

            // Se limpia desde la columna 1: la plantilla trae contenido quemado en los
            // extremos de la banda.
            worksheet.Range(1, 1, 2, lastColumn).Clear(XLClearOptions.Contents);
            worksheet.Range(1, 1, 1, lastColumn).Merge();
            worksheet.Range(2, 1, 2, lastColumn).Merge();

            ApplyExcelHeaderBandStyle(worksheet, lastColumn);
            ApplyExcelSpacerRowStyle(worksheet, lastColumn);

            worksheet.Cell(1, 1).Value = titulo;
            ApplyExcelHeaderTitleStyle(worksheet.Cell(1, 1));
            ApplyExcelHeaderDetail(worksheet.Cell(2, 1), reporte);

            worksheet.Row(1).Height = Math.Max(worksheet.Row(1).Height, 34d);
            worksheet.Row(2).Height = Math.Max(worksheet.Row(2).Height, 40d);
            worksheet.Row(3).Height = Math.Max(worksheet.Row(3).Height, 10d);
            worksheet.Column(1).Width = Math.Max(worksheet.Column(1).Width, 10d);
        }

        private static void ApplyPresupuestoRow(IXLWorksheet worksheet, int row, ProgramaPresupuestoFilaModel fila, bool alternate)
        {
            XLColor background = alternate ? ExcelAlternateRow : XLColor.White;
            worksheet.Cell(row, 1).Value = fila.NomEtapa;
            ApplyRowLabelStyle(worksheet.Cell(row, 1), alternate);

            string[] values =
            {
                FormatCurrency(fila.Costo),
                fila.Cda.ToString("N3", CultureInfo.InvariantCulture),
                fila.PresupuestoCerdo.ToString("N2", CultureInfo.InvariantCulture),
                fila.Gdp.ToString("N3", CultureInfo.InvariantCulture),
                fila.PesoInicial.ToString("N2", CultureInfo.InvariantCulture),
                fila.PesoFinal.ToString("N2", CultureInfo.InvariantCulture),
                fila.Ca.ToString("N2", CultureInfo.InvariantCulture),
                fila.EdadInicial.ToString("N0", CultureInfo.InvariantCulture),
                fila.EdadFinal.ToString("N0", CultureInfo.InvariantCulture),
                fila.DuracionEtapa.ToString("N0", CultureInfo.InvariantCulture)
            };

            for (int i = 0; i < values.Length; i++)
            {
                worksheet.Cell(row, i + 2).Value = values[i];
                ApplyBodyStyle(worksheet.Cell(row, i + 2), alternate);
            }
        }

        private static void ApplyHeaderCellStyle(IXLCell cell, XLColor background)
        {
            cell.Style.Fill.BackgroundColor = background;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.White;
            cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.InsideBorderColor = XLColor.White;
        }

        private static void ApplyRowLabelStyle(IXLCell cell, bool alternate)
        {
            cell.Style.Fill.BackgroundColor = alternate ? ExcelAlternateRow : XLColor.White;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.FromHtml("#1f2937");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = ExcelGridBlue;
            cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.InsideBorderColor = ExcelGridBlue;
        }

        private static void ApplyBodyStyle(IXLCell cell, bool alternate)
        {
            cell.Style.Fill.BackgroundColor = alternate ? ExcelAlternateRow : XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = ExcelGridBlue;
            cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.InsideBorderColor = ExcelGridBlue;
        }

        private static void ApplyFooterBand(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = ExcelDarkBlue;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = XLColor.White;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorderColor = XLColor.White;
        }

        private static void ApplyCategoryBand(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#003d7a");
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = XLColor.FromHtml("#FFFFFF");
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = ExcelGridBlue;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorderColor = ExcelGridBlue;
        }

        private static void ApplySummaryLabelStyle(IXLCell cell)
        {
            cell.Style.Fill.BackgroundColor =  ExcelAlternateRow ;
            cell.Style.Font.FontColor = XLColor.FromHtml("#1f2937");
            cell.Style.Font.Bold = false;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = ExcelGridBlue;
        }

        private static void ApplySummaryValueStyle(IXLCell cell, bool alternate)
        {
            cell.Style.Fill.BackgroundColor = alternate ? ExcelAlternateRow : XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = ExcelGridBlue;
        }

        private string BuildPdfHtml(ProgramaReporteModel reporte)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("<!DOCTYPE html>");
            builder.AppendLine("<html><head><meta charset=\"utf-8\" />");
            builder.AppendLine("<style>");
            builder.AppendLine("body{font-family:Helvetica,Arial,sans-serif;font-size:10pt;color:#1f2937;margin:0;padding:10px 10px 0 10px;}");
            builder.AppendLine("table{width:100%;border-collapse:collapse;margin-bottom:18px;}");
            builder.AppendLine("thead{display:table-header-group;}");
            builder.AppendLine("tfoot{display:table-row-group;}");
            builder.AppendLine("th{background:#0b2e57;color:#fff;font-weight:400;border:1px solid #d6deed;padding:8px 6px;text-align:center;}");
            builder.AppendLine("td{border:1px solid #d6deed;padding:6px 8px;text-align:center;}");
            builder.AppendLine(".row-label{text-align:left;font-weight:bold;}");
            builder.AppendLine(".category{background:#003d7a;color:#fff;font-weight:bold;text-align:left;}");
            builder.AppendLine(".section-gap{height:10px;background:#fff;border:none;padding:0;}");
            builder.AppendLine(".alt{background:#eef2f8;}");
            builder.AppendLine(".summary th{background:#0b2e57;color:#fff;text-align:left;}");
            builder.AppendLine(".summary td{text-align:left;line-height:1.35;background:#eef2f8;}");
            builder.AppendLine(".summary .label{font-weight:bold;}");
            builder.AppendLine(".section-block{page-break-inside:avoid;break-inside:avoid-page;margin-bottom:18px;}");
            // El aire que queda debajo del ultimo bloque desborda la pagina y ExpertPdf
            // agrega una hoja mas con solo el encabezado.
            builder.AppendLine("body > *:last-child{margin-bottom:0;}");
            builder.AppendLine("body > *:last-child table:last-child{margin-bottom:0;}");
            builder.AppendLine("</style></head><body>");

            if (string.Equals(reporte.Seccion, "comparativo", StringComparison.OrdinalIgnoreCase))
            {
                AppendComparativoPdf(builder, reporte);
            }
            else
            {
                AppendPresupuestoPdf(builder, reporte);
            }

            builder.AppendLine("</body></html>");
            return builder.ToString();
        }

        private string BuildHeaderHtml(ProgramaReporteModel reporte)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("<!DOCTYPE html>");
            builder.AppendLine("<html><head><meta charset=\"utf-8\" />");
            builder.AppendLine("<style>");
            builder.AppendLine("body{margin:0;padding:0;font-family:Helvetica,Arial,sans-serif;}");
            builder.AppendLine(".header{background:#0b2e57;color:#fff;border-radius:10px;padding:16px 24px;display:flex;justify-content:space-between;align-items:center;gap:18px;}");
            builder.AppendLine(".header-left{display:flex;align-items:center;gap:18px;min-width:0;}");
            builder.AppendLine(".header-left img{display:block;max-height:56px;width:auto;}");
            builder.AppendLine(".header-right img{display:block;max-height:52px;width:auto;}");
            builder.AppendLine(".header-text{min-width:0;}");
            builder.AppendLine(".header h1{margin:0;font-size:24pt;font-weight:400;line-height:1.05;}");
            builder.AppendLine(".folio{color:#61a7ff;font-size:12pt;margin-top:6px;}");
            builder.AppendLine(".cliente{font-weight:bold;font-size:12pt;margin-top:6px;}");
            builder.AppendLine("</style></head><body>");
            builder.AppendLine("<div class=\"header\">");
            builder.AppendLine("<div class=\"header-left\">");
            builder.AppendLine("<img src=\"" + EscapeHtml(GetPdfImageUri(GetDesignPath("Icono-PerfilNutricional.png"))) + "\" alt=\"Perfil\" />");
            builder.AppendLine("<div class=\"header-text\">");
            builder.AppendLine("<h1>PROGRAMA DE ALIMENTACIÓN</h1>");
            builder.AppendLine("<div class=\"folio\">" + EscapeHtml(BuildFolioReferencia(reporte)) + "</div>");
            builder.AppendLine("<div class=\"cliente\">" + EscapeHtml(reporte.Cliente) + "</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("<div class=\"header-right\"><img src=\"" + EscapeHtml(GetPdfImageUri(GetDesignPath("Logo_Nuptimizer.png"))) + "\" alt=\"Nuptimizer\" /></div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</body></html>");
            return builder.ToString();
        }

        private static void AppendPresupuestoPdf(StringBuilder builder, ProgramaReporteModel reporte)
        {
            builder.AppendLine("<table>");
            builder.AppendLine("<tr><th></th><th>COSTO<br/>($/kg)</th><th>CDA<br/>(Kg)</th><th>PRESUPUESTO POR CERDO<br/>(Kg)</th><th>GDP<br/>(Kg)</th><th>PESO INICIAL<br/>(Kg)</th><th>PESO FINAL<br/>(Kg)</th><th>C.A.</th><th>EDAD INICIAL<br/>(días)</th><th>EDAD FINAL<br/>(días)</th><th>DURACIÓN ETAPA<br/>(días)</th></tr>");
            bool alternate = false;
            foreach (ProgramaPresupuestoFilaModel fila in reporte.PresupuestoFilas)
            {
                string rowClass = alternate ? " class=\"alt\"" : string.Empty;
                builder.AppendLine("<tr" + rowClass + ">");
                builder.AppendLine("<td class=\"row-label\">" + EscapeHtml(fila.NomEtapa) + "</td>");
                builder.AppendLine("<td>" + FormatCurrency(fila.Costo) + "</td>");
                builder.AppendLine("<td>" + fila.Cda.ToString("N3", CultureInfo.InvariantCulture) + "</td>");
                builder.AppendLine("<td>" + fila.PresupuestoCerdo.ToString("N2", CultureInfo.InvariantCulture) + "</td>");
                builder.AppendLine("<td>" + fila.Gdp.ToString("N3", CultureInfo.InvariantCulture) + "</td>");
                builder.AppendLine("<td>" + fila.PesoInicial.ToString("N2", CultureInfo.InvariantCulture) + "</td>");
                builder.AppendLine("<td>" + fila.PesoFinal.ToString("N2", CultureInfo.InvariantCulture) + "</td>");
                builder.AppendLine("<td>" + fila.Ca.ToString("N2", CultureInfo.InvariantCulture) + "</td>");
                builder.AppendLine("<td>" + fila.EdadInicial.ToString("N0", CultureInfo.InvariantCulture) + "</td>");
                builder.AppendLine("<td>" + fila.EdadFinal.ToString("N0", CultureInfo.InvariantCulture) + "</td>");
                builder.AppendLine("<td>" + fila.DuracionEtapa.ToString("N0", CultureInfo.InvariantCulture) + "</td>");
                builder.AppendLine("</tr>");
                alternate = !alternate;
            }

            builder.AppendLine("<tr>");
            builder.AppendLine("<th></th>");
            builder.AppendLine("<th></th>");
            builder.AppendLine("<th>" + reporte.PresupuestoTotales.Cda.ToString("N2", CultureInfo.InvariantCulture) + "</th>");
            builder.AppendLine("<th>" + reporte.PresupuestoTotales.PresupuestoCerdo.ToString("N1", CultureInfo.InvariantCulture) + "</th>");
            builder.AppendLine("<th>" + reporte.PresupuestoTotales.Gdp.ToString("N3", CultureInfo.InvariantCulture) + "</th>");
            builder.AppendLine("<th></th>");
            builder.AppendLine("<th></th>");
            builder.AppendLine("<th>" + reporte.PresupuestoTotales.Ca.ToString("N2", CultureInfo.InvariantCulture) + "</th>");
            builder.AppendLine("<th></th>");
            builder.AppendLine("<th></th>");
            builder.AppendLine("<th></th>");
            builder.AppendLine("</tr>");
            builder.AppendLine("</table>");

            builder.AppendLine("<div class=\"section-block\">");
            builder.AppendLine("<table  style=\"width:50%;\">");
            bool alternateSummary = false;
            foreach (ProgramaResumenItemModel item in reporte.PresupuestoResumen)
            {
                string rowClass = alternateSummary ? " class=\"summary alt\"" : string.Empty;
                builder.AppendLine("<tr><td class=\"label summary alt\" >" + EscapeHtml(item.Etiqueta) + "</td><td "+rowClass+">" + EscapeHtml(FormatSummaryValue(item)) + "</td></tr>");
                alternateSummary = !alternateSummary;
            }

            builder.AppendLine("</table>");
            builder.AppendLine("</div>");
        }

        private static void AppendComparativoPdf(StringBuilder builder, ProgramaReporteModel reporte)
        {
            AppendComparativoPdfSection(builder, "PRESUPUESTO POR CERDO", reporte.ComparativoColumnas, reporte.ComparativoPresupuestos, reporte.ComparativoPresupuestosTotales);
            AppendComparativoPdfSection(builder, "VARIABLES ECONÓMICAS", reporte.ComparativoColumnas, reporte.ComparativoVariables, null);
        }

        private static void AppendComparativoPdfSection(StringBuilder builder, string titulo, List<string> columnas, List<ProgramaComparativoFilaModel> filas, List<string>? totales)
        {
            builder.AppendLine("<div class=\"section-block\">");
            builder.AppendLine("<table>");
            builder.AppendLine("<tr><td class=\"category\" colspan=\"" + columnas.Count + "\">" + EscapeHtml(titulo) + "</td></tr>");
            builder.AppendLine("<tr><td class=\"section-gap\" colspan=\"" + columnas.Count + "\"></td></tr>");
            builder.AppendLine("<tr>");
            for (int i = 0; i < columnas.Count; i++)
            {
                builder.AppendLine("<th style=\"background:#0b2e57;\">" + EscapeHtml(GetComparativoHeaderLabel(titulo, columnas[i], i)) + "</th>");
            }

            builder.AppendLine("</tr>");
            bool alternate = false;
            foreach (ProgramaComparativoFilaModel fila in filas)
            {
                string rowClass = alternate ? " class=\"alt\"" : string.Empty;
                builder.AppendLine("<tr" + rowClass + ">");
                builder.AppendLine("<td class=\"row-label\">" + EscapeHtml(fila.Etiqueta) + "</td>");
                foreach (string valor in fila.Valores)
                {
                    builder.AppendLine("<td>" + EscapeHtml(valor) + "</td>");
                }

                builder.AppendLine("</tr>");
                alternate = !alternate;
            }

            if (totales != null && totales.Count > 0)
            {
                builder.AppendLine("<tr><th></th>");
                foreach (string total in totales)
                {
                    builder.AppendLine("<th>" + EscapeHtml(total) + "</th>");
                }

                builder.AppendLine("</tr>");
            }

            builder.AppendLine("</table>");
            builder.AppendLine("</div>");
        }

        private static string GetComparativoHeaderLabel(string tituloSeccion, string columna, int index)
        {
            if (index == 0)
            {
                return columna;
            }

            if (string.Equals(tituloSeccion, "PRESUPUESTO POR CERDO", StringComparison.OrdinalIgnoreCase))
            {
                return "PRESUPUESTO POR CERDO (Kg) - " + columna;
            }

            return columna;
        }

        private static string BuildFolioReferencia(ProgramaReporteModel reporte)
        {
            if (!string.IsNullOrWhiteSpace(reporte.Folio))
            {
                return "FOLIO: " + reporte.Folio + " | " + reporte.Referencia;
            }

            return reporte.Referencia;
        }

        private static string FormatCurrency(double value)
        {
            return "$" + value.ToString("N2", ReportNumberCulture);
        }

        private static string FormatSummaryValue(ProgramaResumenItemModel item)
        {
            return item.EsMoneda
                ? FormatCurrency(item.Valor)
                : item.Valor.ToString(item.Formato, ReportNumberCulture);
        }

        private static string GetPdfImageUri(string filePath)
        {
            return new Uri(filePath).AbsoluteUri;
        }

        private string BuildFooterHtml()
        {
            return GetTemplate("perfil_nutricional_footer.html")
                .Replace("@@PieTexto", EscapeHtml("AV. DEL MARQUES NO.32, FRACC. IND. BERNARDO QUINTANA, 76246, EL MARQUES, QRO.  |  T.+52 (442) 196 0100  |  www.gponutec.com"));
        }

        private static string GetDesignPath(string fileName)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Diseno", fileName);
        }

        private static string GetTemplate(string templateName)
        {
            string path = GetTemplatePath(templateName);
            if (!System.IO.File.Exists(path))
            {
                throw new Exception("No se encontro la plantilla " + templateName + ".");
            }

            return System.IO.File.ReadAllText(path, Encoding.UTF8);
        }

        private static string GetTemplatePath(string templateName)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Views", "Template", templateName);
        }

        private static string ResolveProgramaStageName(PlanAEtapaModel? etapa)
        {
            return etapa?.NomEtapa?.Trim() ?? string.Empty;
        }

        private static int SafeToInt(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return int.MaxValue;
            }

            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out int result)
                ? result
                : int.MaxValue;
        }

        private static string GetStringColumn(DataRow row, string columnName, string defaultValue)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return defaultValue;
            }

            return row[columnName]?.ToString() ?? defaultValue;
        }

        private static void ApplyExcelHeaderBandStyle(IXLWorksheet worksheet, int lastColumn)
        {
            IXLRange band = worksheet.Range(1, 1, 2, lastColumn);
            band.Style.Fill.BackgroundColor = ExcelDarkBlue;
            band.Style.Font.FontColor = XLColor.White;
            band.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            band.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            band.Style.Alignment.WrapText = true;
            band.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            band.Style.Border.OutsideBorderColor = ExcelDarkBlue;
            band.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            band.Style.Border.InsideBorderColor = ExcelDarkBlue;
        }

        private static void ApplyExcelSpacerRowStyle(IXLWorksheet worksheet, int lastColumn)
        {
            IXLRange spacer = worksheet.Range(3, 1, 3, lastColumn);
            spacer.Clear(XLClearOptions.Contents);
            spacer.Style.Fill.BackgroundColor = XLColor.White;
            spacer.Style.Border.OutsideBorder = XLBorderStyleValues.None;
            spacer.Style.Border.InsideBorder = XLBorderStyleValues.None;
        }

        private static void ApplyExcelHeaderTitleStyle(IXLCell cell)
        {
            cell.Style.Font.FontSize = 20d;
            cell.Style.Font.Bold = false;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
        }

        private static void ApplyExcelHeaderDetail(IXLCell cell, ProgramaReporteModel reporte)
        {
            cell.Value = string.Empty;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            cell.Style.Font.FontSize = 11d;

            IXLRichText richText = cell.RichText;
            richText.ClearText();
            richText
                .AddText(BuildFolioReferencia(reporte))
                .SetFontColor(XLColor.FromHtml("#61a7ff"))
                .SetFontSize(11d);

            if (!string.IsNullOrWhiteSpace(reporte.Cliente))
            {
                richText
                    .AddText(Environment.NewLine + reporte.Cliente)
                    .SetFontColor(XLColor.White)
                    .SetBold(true)
                    .SetFontSize(11d);
            }
        }

        private static string EscapeHtml(string? text)
        {
            return System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
        }

        private sealed class ProgramaReporteModel
        {
            public long CvePlan { get; set; }
            public string Seccion { get; set; } = "presupuesto";
            public string Folio { get; set; } = string.Empty;
            public string Cliente { get; set; } = string.Empty;
            public string Referencia { get; set; } = string.Empty;
            public DateTime FechaEmision { get; set; }
            public List<ProgramaPresupuestoFilaModel> PresupuestoFilas { get; set; } = new();
            public List<ProgramaResumenItemModel> PresupuestoResumen { get; set; } = new();
            public ProgramaPresupuestoTotalesModel PresupuestoTotales { get; set; } = new();
            public List<string> ComparativoColumnas { get; set; } = new();
            public List<ProgramaComparativoFilaModel> ComparativoPresupuestos { get; set; } = new();
            public List<string> ComparativoPresupuestosTotales { get; set; } = new();
            public List<ProgramaComparativoFilaModel> ComparativoVariables { get; set; } = new();
        }

        private sealed class PlanAContextModel
        {
            public long CvePlan { get; set; }
            public string Seccion { get; set; } = "presupuesto";
            public DataRow PlanARow { get; set; } = null!;
            public DataRow ResultadoRow { get; set; } = null!;
            public DataRow? ClienteRow { get; set; }
            public RespOptimizerModel Response { get; set; } = null!;
            public List<PlanAEtapaModel> Etapas { get; set; } = new();
        }

        private sealed class ProgramaPresupuestoFilaModel
        {
            public int CveEtapa { get; set; }
            public string NomEtapa { get; set; } = string.Empty;
            public double Costo { get; set; }
            public double Cda { get; set; }
            public double PresupuestoCerdo { get; set; }
            public double Gdp { get; set; }
            public double DuracionEtapa { get; set; }
            public double PesoInicial { get; set; }
            public double PesoFinal { get; set; }
            public double Ca { get; set; }
            public double EdadInicial { get; set; }
            public double EdadFinal { get; set; }
        }

        private sealed class ProgramaResumenItemModel
        {
            public ProgramaResumenItemModel(string etiqueta, double valor, string formato)
            {
                Etiqueta = etiqueta;
                Valor = valor;
                Formato = formato;
            }

            public ProgramaResumenItemModel(string etiqueta, double valor, bool esMoneda, string formato)
                : this(etiqueta, valor, formato)
            {
                EsMoneda = esMoneda;
            }

            public string Etiqueta { get; }
            public double Valor { get; }
            public string Formato { get; }
            public bool EsMoneda { get; }
        }

        private sealed class ProgramaPresupuestoTotalesModel
        {
            public double Cda { get; set; }
            public double PresupuestoCerdo { get; set; }
            public double Gdp { get; set; }
            public double Ca { get; set; }
        }

        private sealed class ProgramaComparativoFilaModel
        {
            public string Etiqueta { get; set; } = string.Empty;
            public List<string> Valores { get; set; } = new();
            public bool Visible { get; set; }
        }

        private sealed class ProgramaComparativoColumnaModel
        {
            public string Campo { get; set; } = string.Empty;
            public string Titulo { get; set; } = string.Empty;
            public int Posicion { get; set; }
        }

        private sealed class PlanAEtapaModel
        {
            public int CveEtapa { get; set; }
            public string NomEtapa { get; set; } = string.Empty;
            public string Aplica { get; set; } = string.Empty;
            public int OrdenVisual { get; set; }
        }
    }
}

