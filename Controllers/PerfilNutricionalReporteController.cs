using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using ExpertPdf.HtmlToPdf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WSOptimizer7.App_Data;
using WSOptimizer7.Models;

namespace WSOptimizer7.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;

    public class PerfilNutricionalReporteController : Controller
    {
        private const int ExcelHeaderRow = 4;
        private const int ExcelBodyStartRow = 5;
        private const int ExcelTemplateBodyEndRow = 20;
        private static readonly XLColor ExcelDarkBlue = XLColor.FromHtml("#0b2e57");
        private static readonly XLColor ExcelLightBlue = XLColor.FromHtml("#6084d7");
        private static readonly XLColor ExcelGridBlue = XLColor.FromHtml("#d6deed");
        private static readonly XLColor ExcelRowHeaderBlue = XLColor.FromHtml("#eef2f8");

        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;

        public PerfilNutricionalReporteController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        [Route("api/reportes/perfilnutricional/{id}/excel")]
        public IActionResult GetPerfilNutricionalExcel(long id, [FromQuery] int? versionReporte = null)
        {
            try
            {
                ReportePerfilModel reporte = GetReportePerfil(id, versionReporte ?? 1);
                byte[] bytes = GenerateExcelBytes(reporte);
                string fileName = $"PerfilNutricional_{id}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

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
        [Route("api/reportes/perfilnutricional/{id}/pdf")]
        public IActionResult GetPerfilNutricionalPdf(long id, [FromQuery] int? versionReporte = null)
        {
            try
            {
                ReportePerfilModel reporte = GetReportePerfil(id, versionReporte ?? 1);
                byte[] bytes = GeneratePdfBytes(reporte);
                string fileName = $"PerfilNutricional_{id}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

                return File(bytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest("Error generando el archivo PDF: " + ex.Message);
            }
        }

        private ReportePerfilModel GetReportePerfil(long id, int versionReporte)
        {
            if (id <= 0)
                throw new Exception("El id del perfil no es valido.");

            int version = NormalizeVersionReporte(versionReporte);

            string sqlPerfil = @"SELECT OPNR.*, OPN.*, C.*,
CASE
    WHEN ISNULL(LTRIM(RTRIM(C.NomClienteA)), '') = '' THEN ISNULL(C.NomCliente, '')
    WHEN ISNULL(LTRIM(RTRIM(C.NomCliente)), '') = '' THEN C.NomClienteA
    WHEN UPPER(LTRIM(RTRIM(C.NomClienteA))) = UPPER(LTRIM(RTRIM(C.NomCliente))) THEN C.NomClienteA
    ELSE C.NomClienteA + ' (' + C.NomCliente + ')'
END AS NomClientePerfil,
C.NomClienteA AS NomClienteReporte
FROM OptimizerC_PerfilN_Resultado OPNR
INNER JOIN OptimizerC_PerfilN OPN ON OPNR.CvePerfilN = OPN.CvePerfilN
INNER JOIN Clientes C ON OPN.CodCliente = C.CodCliente
WHERE OPNR.CvePerfilN = " + id;

            DataTable dtPerfil = Database.execQuery(sqlPerfil);
            if (dtPerfil == null || dtPerfil.Rows.Count == 0)
                throw new Exception("No se encontraron datos para el perfil indicado.");

            if (!dtPerfil.Columns.Contains("Response2"))
                throw new Exception("La tabla OptimizerC_PerfilN_Resultado no contiene la columna Response2. Es necesario aplicar primero el cambio de base de datos.");

            DataRow row = dtPerfil.Rows[0];
            string requestJson = row["Request"]?.ToString() ?? "";
            string responseJson = row["Response2"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(responseJson))
                throw new Exception("El perfil no contiene informacion en Response2. Es necesario inicializar Response2 con la respuesta ajustada antes de generar el reporte.");

            RequestModel request = JsonConvert.DeserializeObject<RequestModel>(requestJson)
                ?? throw new Exception("No fue posible leer el Request del perfil.");
            ResponseModel response = DeserializeResponse(responseJson);

            string referencia = GetNomReferencia(request.Referencia);
            Dictionary<int, string> etapas = GetEtapasCatalogo(id);
            Dictionary<int, VariableReporteConfig> variablesCatalogo = GetVariablesCatalogo();
            List<ResponseDataModel> variablesVisibles = response.Variables
                .Where(v => variablesCatalogo.TryGetValue(v.NoVariable, out VariableReporteConfig? config)
                    && IsVariableVisible(config, version))
                .OrderBy(v => variablesCatalogo[v.NoVariable].Posicion ?? v.Posicion)
                .ToList();

            List<int> etapasOrdenadas = variablesVisibles
                .SelectMany(v => v.Etapas)
                .Select(e => e.Clave)
                .Distinct()
                .OrderBy(k => k)
                .ToList();

            List<ReporteEtapaModel> columnas = etapasOrdenadas
                .Where(etapas.ContainsKey)
                .Select(k => new ReporteEtapaModel
                {
                    Clave = k,
                    Nombre = etapas[k]
                })
                .ToList();

            List<ReporteCategoriaModel> categorias;

            if (version == 3)
            {
                // Para la versión 3 no agrupar por categorías: listar variables en orden
                categorias = new List<ReporteCategoriaModel>
                {
                    new ReporteCategoriaModel
                    {
                        SinCategoria = true,
                        CveCategoria = 0,
                        Nombre = string.Empty,
                        Posicion = 0,
                        Filas = variablesVisibles
                            .OrderBy(variable => variablesCatalogo[variable.NoVariable].Posicion ?? variable.Posicion)
                            .Select(variable => new ReporteFilaModel
                            {
                                Variable = variablesCatalogo[variable.NoVariable].NomVariable ?? variable.Variable,
                                Posicion = variablesCatalogo[variable.NoVariable].Posicion ?? variable.Posicion,
                                Decimales = variablesCatalogo[variable.NoVariable].Decimales,
                                Valores = columnas.Select(columna => new ReporteCeldaModel
                                {
                                    ClaveEtapa = columna.Clave,
                                    Valor = variable.Etapas.FirstOrDefault(e => e.Clave == columna.Clave)?.Valor
                                }).ToList()
                            })
                            .ToList()
                    }
                };
            }
            else
            {
                categorias = variablesVisibles
                    .GroupBy(variable =>
                    {
                        VariableReporteConfig config = variablesCatalogo[variable.NoVariable];
                        return new
                        {
                            SinCategoria = config.CveCategoria == null || string.IsNullOrWhiteSpace(config.NomCategoria),
                            config.CveCategoria,
                            config.PosicionCategoria,
                            Nombre = string.IsNullOrWhiteSpace(config.NomCategoria) ? string.Empty : config.NomCategoria!.Trim()
                        };
                    })
                    .OrderBy(group => group.Key.SinCategoria ? 0 : 1)
                    .ThenBy(group => group.Key.PosicionCategoria ?? int.MaxValue)
                    .ThenBy(group => group.Key.CveCategoria)
                    .ThenBy(group => group.Min(variable => variablesCatalogo[variable.NoVariable].Posicion ?? variable.Posicion))
                    .Select(group => new ReporteCategoriaModel
                    {
                        SinCategoria = group.Key.SinCategoria,
                        CveCategoria = group.Key.CveCategoria ?? 0,
                        Nombre = group.Key.Nombre,
                        Posicion = group.Key.PosicionCategoria ?? group.Min(variable => variablesCatalogo[variable.NoVariable].Posicion ?? variable.Posicion),
                        Filas = group
                            .OrderBy(variable => variablesCatalogo[variable.NoVariable].Posicion ?? variable.Posicion)
                            .Select(variable => new ReporteFilaModel
                            {
                                Variable = variablesCatalogo[variable.NoVariable].NomVariable ?? variable.Variable,
                                Posicion = variablesCatalogo[variable.NoVariable].Posicion ?? variable.Posicion,
                                Decimales = variablesCatalogo[variable.NoVariable].Decimales,
                                Valores = columnas.Select(columna => new ReporteCeldaModel
                                {
                                    ClaveEtapa = columna.Clave,
                                    Valor = variable.Etapas.FirstOrDefault(e => e.Clave == columna.Clave)?.Valor
                                }).ToList()
                            })
                            .ToList()
                    })
                    .ToList();
            }

            return new ReportePerfilModel
            {
                CvePerfilN = id,
                Folio = row.Table.Columns.Contains("FolioR") ? row["FolioR"]?.ToString() ?? string.Empty : string.Empty,
                Cliente = GetClienteReporte(row),
                Referencia = referencia,
                FechaEmision = DateTime.Now,
                Columnas = columnas,
                Categorias = categorias
            };
        }

        private ResponseModel DeserializeResponse(string responseJson)
        {
            JToken token = JToken.Parse(responseJson);

            if (token.Type == JTokenType.Object)
            {
                return token.ToObject<ResponseModel>()
                    ?? throw new Exception("No fue posible leer el Response del perfil.");
            }

            if (token.Type == JTokenType.Array)
            {
                JArray array = (JArray)token;
                if (!array.Any())
                {
                    return new ResponseModel();
                }

                JToken firstItem = array.First!;
                if (firstItem["NoVariable"] != null || firstItem["Etapas"] != null)
                {
                    List<ResponseDataModel>? variables = array.ToObject<List<ResponseDataModel>>();
                    if (variables == null)
                        throw new Exception("No fue posible leer el arreglo de variables del perfil.");

                    return new ResponseModel
                    {
                        Variables = variables
                    };
                }

                if (firstItem["Variable"] != null && firstItem["Etapa"] != null)
                {
                    List<LegacyCapturaModel>? captura = array.ToObject<List<LegacyCapturaModel>>();
                    if (captura == null)
                        throw new Exception("No fue posible leer el arreglo legacy del perfil.");

                    return new ResponseModel
                    {
                        Variables = captura
                            .GroupBy(item => item.Variable)
                            .OrderBy(group => group.Key)
                            .Select(group => new ResponseDataModel
                            {
                                NoVariable = group.Key,
                                Variable = group.FirstOrDefault()?.Descripcion ?? string.Empty,
                                Posicion = 0,
                                MostrarCliente = group.FirstOrDefault()?.Mostrar ?? string.Empty,
                                Etapas = group
                                    .OrderBy(item => item.Etapa)
                                    .Select(item => new EtapaResModel(item.Etapa, item.Ajuste))
                                    .ToList()
                            })
                            .ToList()
                    };
                }
            }

            throw new Exception("El contenido de Response2 no tiene un formato JSON valido para el reporte.");
        }

        private string GetNomReferencia(int cveReferencia)
        {
            string sql = $"SELECT * FROM CatOptimizerC_Referencias WHERE CveReferencia = {cveReferencia}";
            DataTable dtReferencia = Database.execQuery(sql);
            if (dtReferencia == null || dtReferencia.Rows.Count == 0)
                return cveReferencia.ToString();

            return dtReferencia.Rows[0]["NomReferencia"]?.ToString() ?? cveReferencia.ToString();
        }

        private string GetClienteReporte(DataRow row)
        {
            string nombrePerfil = row.Table.Columns.Contains("NomClientePerfil")
                ? (row["NomClientePerfil"]?.ToString() ?? string.Empty).Trim()
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(nombrePerfil))
                return nombrePerfil;

            string alias = row.Table.Columns.Contains("NomClienteReporte")
                ? (row["NomClienteReporte"]?.ToString() ?? string.Empty).Trim()
                : string.Empty;

            string nombreAlterno = row.Table.Columns.Contains("NomClienteA")
                ? (row["NomClienteA"]?.ToString() ?? string.Empty).Trim()
                : string.Empty;

            string razonSocial = row.Table.Columns.Contains("NomCliente")
                ? (row["NomCliente"]?.ToString() ?? string.Empty).Trim()
                : string.Empty;

            string nombreCorto = new[] { alias, nombreAlterno }
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(nombreCorto) && string.IsNullOrWhiteSpace(razonSocial))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(razonSocial))
                return nombreCorto;

            if (string.IsNullOrWhiteSpace(nombreCorto))
                return razonSocial;

            if (string.Equals(nombreCorto, razonSocial, StringComparison.OrdinalIgnoreCase))
                return nombreCorto;

            return $"{nombreCorto} ({razonSocial})";
        }

        private Dictionary<int, VariableReporteConfig> GetVariablesCatalogo()
        {
            bool hasCategoria = TableHasColumn("CatOptimizerC_Variables", "CveCategoria");
            string sql = hasCategoria
                ? @"SELECT V.*, CV.NomCategoria, CV.Posicion AS PosicionCategoria
FROM CatOptimizerC_Variables V
LEFT JOIN CatOptimizer_Categorias_Variables CV ON V.CveCategoria = CV.CveCategoria"
                : "SELECT V.* FROM CatOptimizerC_Variables V";
            DataTable dtVariables = Database.execQuery(sql);
            if (dtVariables == null || dtVariables.Rows.Count == 0)
                throw new Exception("No se encontro el catalogo de variables.");

            return dtVariables.AsEnumerable().ToDictionary(
                row => Convert.ToInt32(row["CveVariable"]),
                row => new VariableReporteConfig
                {
                    CveVariable = Convert.ToInt32(row["CveVariable"]),
                    NomVariable = row["NomVariable"]?.ToString(),
                    Posicion = row["Posicion"] == DBNull.Value ? null : Convert.ToInt32(row["Posicion"]),
                    Decimales = row.Table.Columns.Contains("Decimales") && row["Decimales"] != DBNull.Value ? Convert.ToInt32(row["Decimales"]) : null,
                    CveCategoria = hasCategoria && row.Table.Columns.Contains("CveCategoria") && row["CveCategoria"] != DBNull.Value ? Convert.ToInt32(row["CveCategoria"]) : null,
                    NomCategoria = row.Table.Columns.Contains("NomCategoria") ? row["NomCategoria"]?.ToString() : null,
                    PosicionCategoria = row.Table.Columns.Contains("PosicionCategoria") && row["PosicionCategoria"] != DBNull.Value ? Convert.ToInt32(row["PosicionCategoria"]) : null,
                    MostrarCliente = (row["MostrarCliente"]?.ToString() ?? string.Empty).Trim(),
                    ReporteInterno = (row["ReporteInterno"]?.ToString() ?? string.Empty).Trim(),
                    ReporteExterno = (row["ReporteExterno"]?.ToString() ?? string.Empty).Trim()
                });
        }

        private static bool TableHasColumn(string tableName, string columnName)
        {
            try
            {
                string sql = $@"SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = '{tableName}'
  AND COLUMN_NAME = '{columnName}'";

                DataTable dt = Database.execQuery(sql);
                return dt != null && dt.Rows.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private Dictionary<int, string> GetEtapasCatalogo(long cvePerfilN)
        {
            string sqlCatalogo = "SELECT * FROM CatOptimizerC_Etapas";
            DataTable dtEtapasCatalogo = Database.execQuery(sqlCatalogo);
            if (dtEtapasCatalogo == null || dtEtapasCatalogo.Rows.Count == 0)
                throw new Exception("No se encontro el catalogo de etapas.");

            Dictionary<int, string> etapas = dtEtapasCatalogo.AsEnumerable().ToDictionary(
                row => Convert.ToInt32(row["CveEtapa"]),
                row => row["NomEtapa"]?.ToString() ?? string.Empty);

            string sqlPerfil = $"SELECT * FROM OptimizerC_PerfilN_Etapas WHERE CvePerfilN = {cvePerfilN}";
            DataTable dtEtapasPerfil = Database.execQuery(sqlPerfil);

            if (dtEtapasPerfil == null || dtEtapasPerfil.Rows.Count == 0)
                return etapas;

            foreach (DataRow row in dtEtapasPerfil.Rows)
            {
                int cveEtapa = Convert.ToInt32(row["CveEtapa"]);
                string nomEtapa = row["NomEtapa"]?.ToString()?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(nomEtapa))
                    continue;

                etapas[cveEtapa] = nomEtapa;
            }

            return etapas;
        }

        private byte[] GenerateExcelBytes(ReportePerfilModel reporte)
        {
            string templatePath = GetDesignPath("Nuptimizer_Cerdos-PerfilNutricional.xlsx");
            if (!System.IO.File.Exists(templatePath))
                throw new Exception("No se encontro la plantilla base de Excel.");

            using XLWorkbook workbook = new XLWorkbook(templatePath);
            IXLWorksheet worksheet = workbook.Worksheet(1);

            FillExcelHeader(worksheet, reporte);
            FillExcelColumns(worksheet, reporte);
            FillExcelRows(worksheet, reporte);

            using MemoryStream stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private byte[] GeneratePdfBytes(ReportePerfilModel reporte)
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
            pdf.PdfDocumentOptions.PdfCompressionLevel = PdfCompressionLevel.Best;
            pdf.PdfDocumentOptions.JpegCompressionEnabled = true;
            pdf.PdfDocumentOptions.JpegCompressionLevel = 5;

            pdf.PdfDocumentOptions.ShowHeader = false;

            pdf.PdfDocumentOptions.ShowFooter = true;
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
            

            try
            {
                
                return pdf.GetPdfBytesFromHtmlString(BuildBodyHtml(reporte));

            }
            finally
            {
                /*if (System.IO.File.Exists(tempHtmlPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempHtmlPath);
                    }
                    catch
                    {
                        // No bloqueamos la descarga si el archivo temporal no se puede limpiar.
                    }
                }*/
            }
        }

        private void FillExcelHeader(IXLWorksheet worksheet, ReportePerfilModel reporte)
        {
            int lastColumn = Math.Max(reporte.Columnas.Count + 1, 9);
            int rightLogoStartColumn = Math.Max(lastColumn - 2, 6);
            int lastTextColumn = Math.Max(2, rightLogoStartColumn - 1);

            //UnmergeHeaderRangeIfNeeded(worksheet, 1, 1, lastColumn);
            //UnmergeHeaderRangeIfNeeded(worksheet, 2, 1, lastColumn);
            //UnmergeHeaderRangeIfNeeded(worksheet, 3, 1, lastColumn);

            worksheet.Range(1, 2, 2, 6).Clear(XLClearOptions.Contents);

            worksheet.Range(1, 2, 1, 6).Merge();
            worksheet.Range(2, 2, 2, 6).Merge();

            ApplyExcelHeaderBandStyle(worksheet, lastColumn);
            ApplyExcelSpacerRowStyle(worksheet, lastColumn);

            worksheet.Cell(1, 2).Value = "PERFIL NUTRICIONAL";
            ApplyExcelHeaderTitleStyle(worksheet.Cell(1, 2));
            ApplyExcelHeaderDetail(worksheet.Cell(2, 2), reporte);

            worksheet.Row(1).Height = Math.Max(worksheet.Row(1).Height, 34d);
            worksheet.Row(2).Height = Math.Max(worksheet.Row(2).Height, 40d);
            worksheet.Row(3).Height = Math.Max(worksheet.Row(3).Height, 10d);
            worksheet.Column(1).Width = Math.Max(worksheet.Column(1).Width, 10d);

        }
        private void FillExcelColumns(IXLWorksheet worksheet, ReportePerfilModel reporte)
        {
            worksheet.Cell(ExcelHeaderRow, 1).Value = string.Empty;

            int lastColumn = Math.Max(reporte.Columnas.Count + 1, 7);
            for (int columnIndex = 2; columnIndex <= lastColumn; columnIndex++)
            {
                worksheet.Cell(ExcelHeaderRow, columnIndex).Clear(XLClearOptions.Contents);
            }

            ApplyStageHeaderStyle(worksheet.Cell(ExcelHeaderRow, 1), ExcelDarkBlue);

            for (int index = 0; index < reporte.Columnas.Count; index++)
            {
                int columnIndex = index + 2;
                worksheet.Cell(ExcelHeaderRow, columnIndex).Value = reporte.Columnas[index].Nombre;
                ApplyStageHeaderStyle(
                    worksheet.Cell(ExcelHeaderRow, columnIndex), ExcelDarkBlue);
            }

            for (int columnIndex = reporte.Columnas.Count + 2; columnIndex <= lastColumn; columnIndex++)
            {
                ApplyStageHeaderStyle(worksheet.Cell(ExcelHeaderRow, columnIndex), ExcelDarkBlue);
            }
        }

        private void FillExcelRows(IXLWorksheet worksheet, ReportePerfilModel reporte)
        {
            int totalRows = GetTotalExcelBodyRows(reporte);
            EnsureExcelBodyCapacity(worksheet, totalRows);

            int lastColumn = Math.Max(reporte.Columnas.Count + 1, 7);
            int lastRowToClear = Math.Max(ExcelTemplateBodyEndRow, ExcelBodyStartRow + totalRows - 1);
            for (int rowIndex = ExcelBodyStartRow; rowIndex <= lastRowToClear; rowIndex++)
            {
                foreach (IXLRange mergedRange in worksheet.MergedRanges
                    .Where(range => range.FirstRow().RowNumber() == rowIndex && range.LastRow().RowNumber() == rowIndex)
                    .ToList())
                {
                    mergedRange.Unmerge();
                }

                for (int columnIndex = 1; columnIndex <= lastColumn; columnIndex++)
                {
                    worksheet.Cell(rowIndex, columnIndex).Clear(XLClearOptions.Contents);
                }
            }

            int currentRow = ExcelBodyStartRow;
            bool useAlternateRow = false;
            foreach (ReporteCategoriaModel categoria in reporte.Categorias
                .OrderBy(c => c.SinCategoria ? 0 : 1)
                .ThenBy(c => c.Posicion)
                .ThenBy(c => c.CveCategoria))
            {
                // Si la categoria no tiene nombre y es 'SinCategoria', no pintar la fila de encabezado
                if (!(categoria.SinCategoria && string.IsNullOrWhiteSpace(categoria.Nombre)))
                {
                    worksheet.Range(currentRow, 1, currentRow, lastColumn).Merge();
                    worksheet.Cell(currentRow, 1).Value = categoria.Nombre;
                    ApplyCategoryRowStyle(worksheet.Range(currentRow, 1, currentRow, lastColumn));
                    currentRow++;
                }

                foreach (ReporteFilaModel fila in categoria.Filas.OrderBy(f => f.Posicion))
                {
                    worksheet.Cell(currentRow, 1).Value = fila.Variable;
                    ApplyRowHeaderStyle(worksheet.Cell(currentRow, 1), useAlternateRow);

                    for (int columnOffset = 0; columnOffset < fila.Valores.Count; columnOffset++)
                    {
                        int columnIndex = columnOffset + 2;
                        worksheet.Cell(currentRow, columnIndex).Value = FormatCellValue(fila.Valores[columnOffset].Valor, fila.Decimales);
                        ApplyBodyCellStyle(worksheet.Cell(currentRow, columnIndex), useAlternateRow);
                    }

                    for (int columnIndex = fila.Valores.Count + 2; columnIndex <= lastColumn; columnIndex++)
                    {
                        ApplyBodyCellStyle(worksheet.Cell(currentRow, columnIndex), useAlternateRow);
                    }

                    useAlternateRow = !useAlternateRow;
                    currentRow++;
                }
            }
        }

        private static int GetTotalExcelBodyRows(ReportePerfilModel reporte)
        {
            return reporte.Categorias.Sum(categoria => 1 + categoria.Filas.Count);
        }

        private static void EnsureExcelBodyCapacity(IXLWorksheet worksheet, int rowCount)
        {
            int currentCapacity = ExcelTemplateBodyEndRow - ExcelBodyStartRow + 1;
            if (rowCount <= currentCapacity)
                return;

            int additionalRows = rowCount - currentCapacity;
            int insertAtRow = ExcelTemplateBodyEndRow;
            worksheet.Row(insertAtRow).InsertRowsBelow(additionalRows);

            IXLRange styleSource = worksheet.Range(insertAtRow, 1, insertAtRow, 7);
            for (int offset = 1; offset <= additionalRows; offset++)
            {
                IXLRange target = worksheet.Range(insertAtRow + offset, 1, insertAtRow + offset, 7);
                styleSource.CopyTo(target);
                foreach (IXLCell cell in target.Cells())
                {
                    cell.Clear(XLClearOptions.Contents);
                }
            }
        }

        private static void ApplyTopHeaderStyle(IXLRange range, bool titleCell, double? fontSize = null, bool bold = true)
        {
            range.Style.Fill.BackgroundColor = titleCell ? ExcelDarkBlue : XLColor.White;
            range.Style.Font.FontColor = titleCell ? XLColor.White : XLColor.FromHtml("#2d4998");
            range.Style.Font.Bold = bold;
            if (fontSize.HasValue)
            {
                range.Style.Font.FontSize = fontSize.Value;
            }
            range.Style.Alignment.WrapText = true;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Alignment.Horizontal = titleCell ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Center;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = ExcelGridBlue;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorderColor = ExcelGridBlue;
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
            cell.Style.Font.FontSize = 24d;
            cell.Style.Font.Bold = false;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
        }

        private static void ApplyExcelHeaderDetail(IXLCell cell, ReportePerfilModel reporte)
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


        private static void ApplyStageHeaderStyle(IXLCell cell, XLColor backgroundColor)
        {
            cell.Style.Fill.BackgroundColor = backgroundColor;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Font.Bold = false;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.White;
            cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.InsideBorderColor = XLColor.White;
        }

        private static void ApplyRowHeaderStyle(IXLCell cell, bool useAlternateRow)
        {
            cell.Style.Fill.BackgroundColor = useAlternateRow ? ExcelRowHeaderBlue : XLColor.White;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.FromHtml("#111827");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = ExcelGridBlue;
            cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.InsideBorderColor = ExcelGridBlue;
        }

        private static void ApplyCategoryRowStyle(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#dce9f5");
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = XLColor.FromHtml("#1f2937");
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = ExcelGridBlue;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorderColor = ExcelGridBlue;
        }

        private static void ApplyBodyCellStyle(IXLCell cell, bool useAlternateRow)
        {
            cell.Style.Fill.BackgroundColor = useAlternateRow ? ExcelRowHeaderBlue : XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = ExcelGridBlue;
            cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.InsideBorderColor = ExcelGridBlue;
        }

        private static void UnmergeHeaderRangeIfNeeded(IXLWorksheet worksheet, int row, int startColumn, int endColumn)
        {
            foreach (IXLRange mergedRange in worksheet.MergedRanges
                .Where(range => range.FirstRow().RowNumber() == row
                    && range.LastRow().RowNumber() == row
                    && range.FirstColumn().ColumnNumber() >= startColumn
                    && range.LastColumn().ColumnNumber() <= endColumn)
                .ToList())
            {
                mergedRange.Unmerge();
            }
        }

        private string BuildBodyHtml(ReportePerfilModel reporte)
        {
            string template = GetTemplate("perfil_nutricional_body.html");
            string columnHeaders = string.Join(Environment.NewLine, reporte.Columnas.Select(columna =>
                $"<td style=\"border: solid 1px #cbd5e1; padding: 2px 6px; background-color:{GetStageHeaderHtmlColor(reporte.Columnas.IndexOf(columna))};\" align=\"center\">" +
                $"<Label style=\"font-family:Helvetica;font-size:8pt;font-weight:normal;color:#ffffff;\">{EscapeHtml(columna.Nombre)}</Label>" +
                "</td>"));
            string rows = BuildHtmlRows(reporte);
            
            return template
                .Replace("@@Titulo", EscapeHtml("PERFIL NUTRICIONAL"))
                .Replace("@@FolioReferencia", EscapeHtml(BuildFolioReferencia(reporte)))
                .Replace("@@ClienteHeader", EscapeHtml(reporte.Cliente))
                .Replace("@@LeftLogo", EscapeHtml(GetPdfImageUri(Path.Combine(Directory.GetCurrentDirectory(), "Diseno", "Icono-PerfilNutricional.svg"))))
                .Replace("@@RightLogo", EscapeHtml(GetPdfImageUri(Path.Combine(Directory.GetCurrentDirectory(), "Diseno", "Logo_Nuptimizer.svg"))))
                .Replace("@@FechaEmision", reporte.FechaEmision.ToString("dd/MM/yyyy"))
                .Replace("@@ColumnHeaders", columnHeaders)
                .Replace("@@TableRows", rows);
            
            
        }

        private static string GetStageHeaderHtmlColor(int index)
        {
            return "#0b2e57";
        }

        private static string BuildHtmlRows(ReportePerfilModel reporte)
        {
            StringBuilder builder = new StringBuilder();
            bool useAlternateRow = false;

            foreach (ReporteCategoriaModel categoria in reporte.Categorias
                .OrderBy(c => c.SinCategoria ? 0 : 1)
                .ThenBy(c => c.Posicion)
                .ThenBy(c => c.CveCategoria))
            {
                // Omitir encabezado si la categoria es 'SinCategoria' y no tiene nombre (caso versionReporte = 3)
                if (!(categoria.SinCategoria && string.IsNullOrWhiteSpace(categoria.Nombre)))
                {
                    builder.Append("<tr>");
                    builder.Append($"<td colspan=\"{reporte.Columnas.Count + 1}\" style=\"border: solid 1px #d6deed; background-color: #dce9f5; padding: 4px 8px;\">");
                    builder.Append($"<Label style=\"font-family:Helvetica;font-size:8pt;font-weight:bold;color:#1f2937;\">{EscapeHtml(categoria.Nombre)}</Label>");
                    builder.Append("</td>");
                    builder.Append("</tr>");
                }

                foreach (ReporteFilaModel fila in categoria.Filas.OrderBy(f => f.Posicion))
                {
                    string rowBackgroundColor = useAlternateRow ? "#eef2f8" : "#ffffff";

                    builder.Append("<tr>");
                    builder.Append($"<td style=\"border: solid 1px #d6deed; background-color: {rowBackgroundColor}; padding: 2px 8px;\">");
                    builder.Append($"<Label style=\"font-family:Helvetica;font-size:7.5pt;font-weight:bold;color:#111827;\">{EscapeHtml(fila.Variable)}</Label>");
                    builder.Append("</td>");

                    foreach (ReporteCeldaModel celda in fila.Valores)
                    {
                        builder.Append($"<td style=\"border: solid 1px #d6deed; background-color: {rowBackgroundColor}; padding: 2px 6px;\" align=\"center\">");
                        builder.Append($"<Label style=\"font-family:Helvetica;font-size:7.5pt;font-weight:normal;color:#111827;\">{EscapeHtml(FormatCellValue(celda.Valor, fila.Decimales))}</Label>");
                        builder.Append("</td>");
                    }

                    builder.Append("</tr>");
                    useAlternateRow = !useAlternateRow;
                }
            }

            return builder.ToString();
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

        private static string GetTemplate(string templateName)
        {
            string path = GetTemplatePath(templateName);
            if (!System.IO.File.Exists(path))
                throw new Exception($"No se encontro la plantilla {templateName}.");

            return System.IO.File.ReadAllText(path, Encoding.UTF8);
        }

        private static string GetTemplatePath(string templateName)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Views", "Template", templateName);
        }

        private static string GetDesignPath(string fileName)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Diseno", fileName);
        }

        private static int NormalizeVersionReporte(int versionReporte)
        {
            if (versionReporte is < 1 or > 3)
                throw new Exception("La versionReporte debe ser 1, 2 o 3.");

            return versionReporte;
        }

        private static bool IsVariableVisible(VariableReporteConfig config, int versionReporte)
        {
            return versionReporte switch
            {
                1 => HasEnabledFlag(config.MostrarCliente),
                2 => HasEnabledFlag(config.ReporteInterno),
                3 => HasEnabledFlag(config.ReporteExterno),
                _ => false
            };
        }

        private static bool HasEnabledFlag(string? value)
        {
            return string.Equals((value ?? string.Empty).Trim(), "S", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatCellValue(double? value, int? decimales = null)
        {
            if (!value.HasValue)
                return string.Empty;

            double finalValue = decimales.HasValue
                ? Math.Round(value.Value, decimales.Value)
                : value.Value;

            return finalValue.ToString(CultureInfo.InvariantCulture);
        }

        private static string EscapeHtml(string? text)
        {
            return System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
        }

        private class LegacyCapturaModel
        {
            public int Etapa { get; set; }
            public int Variable { get; set; }
            public string Descripcion { get; set; } = string.Empty;
            public double Ajuste { get; set; }
            public string Mostrar { get; set; } = string.Empty;
            public string ReporteInterno { get; set; } = string.Empty;
            public string ReporteExterno { get; set; } = string.Empty;
        }

        private class ReportePerfilModel
        {
            public long CvePerfilN { get; set; }
            public string Folio { get; set; } = string.Empty;
            public string Cliente { get; set; } = string.Empty;
            public string Referencia { get; set; } = string.Empty;
            public DateTime FechaEmision { get; set; }
            public List<ReporteEtapaModel> Columnas { get; set; } = new List<ReporteEtapaModel>();
            public List<ReporteCategoriaModel> Categorias { get; set; } = new List<ReporteCategoriaModel>();
        }

        private class ReporteCategoriaModel
        {
            public bool SinCategoria { get; set; }
            public int CveCategoria { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public int Posicion { get; set; }
            public List<ReporteFilaModel> Filas { get; set; } = new List<ReporteFilaModel>();
        }

        private class ReporteEtapaModel
        {
            public int Clave { get; set; }
            public string Nombre { get; set; } = string.Empty;
        }

        private class ReporteFilaModel
        {
            public string Variable { get; set; } = string.Empty;
            public int Posicion { get; set; }
            public int? Decimales { get; set; }
            public List<ReporteCeldaModel> Valores { get; set; } = new List<ReporteCeldaModel>();
        }

        private class ReporteCeldaModel
        {
            public int ClaveEtapa { get; set; }
            public double? Valor { get; set; }
        }

        private class VariableReporteConfig
        {
            public int CveVariable { get; set; }
            public string? NomVariable { get; set; }
            public int? Posicion { get; set; }
            public int? Decimales { get; set; }
            public int? CveCategoria { get; set; }
            public string? NomCategoria { get; set; }
            public int? PosicionCategoria { get; set; }
            public string? MostrarCliente { get; set; }
            public string? ReporteInterno { get; set; }
            public string? ReporteExterno { get; set; }
        }

        private static string BuildFolioReferencia(ReportePerfilModel reporte)
        {
            if (!string.IsNullOrWhiteSpace(reporte.Folio))
            {
                return $"FOLIO: {reporte.Folio} | {reporte.Referencia}";
            }

            return reporte.Referencia;
        }

        private static string BuildExcelHeaderDetail(ReportePerfilModel reporte)
        {
            List<string> lineas = new List<string>
            {
                BuildFolioReferencia(reporte)
            };

            if (!string.IsNullOrWhiteSpace(reporte.Cliente))
            {
                lineas.Add(reporte.Cliente);
            }

            return string.Join(Environment.NewLine, lineas);
        }
    }
}







