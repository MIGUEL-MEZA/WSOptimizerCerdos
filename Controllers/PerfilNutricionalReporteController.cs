using System.Data;
using System.Drawing;
using System.Globalization;
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

    public class PerfilNutricionalReporteController : Controller
    {
        private const int ExcelHeaderRow = 3;
        private const int ExcelBodyStartRow = 4;
        private const int ExcelTemplateBodyEndRow = 20;

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

            string sqlPerfil = @"SELECT OPNR.*, OPN.*, C.*, C.NomClienteA AS NomClienteReporte FROM OptimizerC_PerfilN_Resultado OPNR
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
            ResponseModel response = JsonConvert.DeserializeObject<ResponseModel>(responseJson)
                ?? throw new Exception("No fue posible leer el Response del perfil.");

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

            List<ReporteFilaModel> filas = variablesVisibles.Select(variable => new ReporteFilaModel
            {
                Variable = variablesCatalogo[variable.NoVariable].NomVariable ?? variable.Variable,
                Posicion = variablesCatalogo[variable.NoVariable].Posicion ?? variable.Posicion,
                Valores = columnas.Select(columna => new ReporteCeldaModel
                {
                    ClaveEtapa = columna.Clave,
                    Valor = variable.Etapas.FirstOrDefault(e => e.Clave == columna.Clave)?.Valor
                }).ToList()
            }).ToList();

            return new ReportePerfilModel
            {
                CvePerfilN = id,
                Cliente = row["NomClienteReporte"]?.ToString() ?? row["NomClienteA"]?.ToString() ?? "",
                Referencia = referencia,
                FechaEmision = DateTime.Now,
                Columnas = columnas,
                Filas = filas
            };
        }

        private string GetNomReferencia(int cveReferencia)
        {
            string sql = $"SELECT * FROM CatOptimizerC_Referencias WHERE CveReferencia = {cveReferencia}";
            DataTable dtReferencia = Database.execQuery(sql);
            if (dtReferencia == null || dtReferencia.Rows.Count == 0)
                return cveReferencia.ToString();

            return dtReferencia.Rows[0]["NomReferencia"]?.ToString() ?? cveReferencia.ToString();
        }

        private Dictionary<int, VariableReporteConfig> GetVariablesCatalogo()
        {
            string sql = "SELECT * FROM CatOptimizerC_Variables";
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
                    MostrarCliente = row["MostrarCliente"]?.ToString(),
                    ReporteInterno = row["ReporteInterno"]?.ToString(),
                    ReporteExterno = row["ReporteExterno"]?.ToString()
                });
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

            pdf.PdfDocumentOptions.ShowHeader = true;
            pdf.PdfHeaderOptions.DrawHeaderLine = false;
            pdf.PdfHeaderOptions.HtmlToPdfArea = new HtmlToPdfArea(
                BuildHeaderHtml(reporte),
                GetTemplatePath("perfil_nutricional_header.html"));
            pdf.PdfHeaderOptions.HeaderHeight = 50;
            pdf.PdfHeaderOptions.HeaderTextColor = Color.Black;
            pdf.PdfHeaderOptions.HeaderTextFontType = PdfFontType.Helvetica;
            pdf.PdfHeaderOptions.HeaderTextFontSize = 9;

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
            worksheet.Cell("A1").Value = "PERFIL NUTRICIONAL";
            worksheet.Cell("C1").Value = $"CLIENTE{Environment.NewLine}{reporte.Cliente}";
            worksheet.Cell("E1").Value = $"REFERENCIA{Environment.NewLine}{reporte.Referencia}";
            worksheet.Cell("G1").Value = $"FECHA EMISION:{Environment.NewLine}{reporte.FechaEmision:dd/MM/yyyy}";

            worksheet.Cell("A1").Style.Alignment.WrapText = true;
            worksheet.Cell("C1").Style.Alignment.WrapText = true;
            worksheet.Cell("E1").Style.Alignment.WrapText = true;
            worksheet.Cell("G1").Style.Alignment.WrapText = true;
        }

        private void FillExcelColumns(IXLWorksheet worksheet, ReportePerfilModel reporte)
        {
            worksheet.Cell(ExcelHeaderRow, 1).Value = "ETAPA";

            int existingColumnCount = Math.Max(reporte.Columnas.Count + 1, 7);
            for (int columnIndex = 2; columnIndex <= existingColumnCount; columnIndex++)
            {
                worksheet.Cell(ExcelHeaderRow, columnIndex).Clear(XLClearOptions.Contents);
            }

            for (int index = 0; index < reporte.Columnas.Count; index++)
            {
                worksheet.Cell(ExcelHeaderRow, index + 2).Value = reporte.Columnas[index].Nombre;
            }
        }

        private void FillExcelRows(IXLWorksheet worksheet, ReportePerfilModel reporte)
        {
            EnsureExcelBodyCapacity(worksheet, reporte.Filas.Count);

            int lastColumn = Math.Max(reporte.Columnas.Count + 1, 7);
            int lastRowToClear = Math.Max(ExcelTemplateBodyEndRow, ExcelBodyStartRow + reporte.Filas.Count - 1);
            for (int rowIndex = ExcelBodyStartRow; rowIndex <= lastRowToClear; rowIndex++)
            {
                for (int columnIndex = 1; columnIndex <= lastColumn; columnIndex++)
                {
                    worksheet.Cell(rowIndex, columnIndex).Clear(XLClearOptions.Contents);
                }
            }

            for (int rowOffset = 0; rowOffset < reporte.Filas.Count; rowOffset++)
            {
                ReporteFilaModel fila = reporte.Filas[rowOffset];
                int rowIndex = ExcelBodyStartRow + rowOffset;
                worksheet.Cell(rowIndex, 1).Value = fila.Variable;

                for (int columnOffset = 0; columnOffset < fila.Valores.Count; columnOffset++)
                {
                    worksheet.Cell(rowIndex, columnOffset + 2).Value = FormatCellValue(fila.Valores[columnOffset].Valor);
                }
            }
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

        private string BuildHeaderHtml(ReportePerfilModel reporte)
        {
            return GetTemplate("perfil_nutricional_header.html")
                .Replace("@@FechaEmision", reporte.FechaEmision.ToString("dd/MM/yyyy"));
        }

        private string BuildBodyHtml(ReportePerfilModel reporte)
        {
            string template = GetTemplate("perfil_nutricional_body.html");
            string[] stageColors =
            {
                "#6084d7",
                "#2d4998",
                "#6f8fe4",
                "#2c458e",
                "#6b88df",
                "#27418b"
            };
            string columnHeaders = string.Join(Environment.NewLine, reporte.Columnas.Select(columna =>
                $"<td style=\"border: solid 1px #cbd5e1; padding: 2px 6px; background-color:{stageColors[Math.Min(reporte.Columnas.IndexOf(columna), stageColors.Length - 1)]};\" align=\"center\">" +
                $"<Label style=\"font-family:Helvetica;font-size:8pt;font-weight:normal;color:#ffffff;\">{EscapeHtml(columna.Nombre)}</Label>" +
                "</td>"));
            string rows = string.Join(Environment.NewLine, reporte.Filas.OrderBy(f => f.Posicion).Select(fila =>
                "<tr>" +
                "<td style=\"border: solid 1px #d6deed; background-color: #eef2f8; padding: 2px 8px;\">" +
                $"<Label style=\"font-family:Helvetica;font-size:7.5pt;font-weight:bold;color:#111827;\">{EscapeHtml(fila.Variable)}</Label>" +
                "</td>" +
                string.Join(string.Empty, fila.Valores.Select(celda =>
                    "<td style=\"border: solid 1px #d6deed; padding: 2px 6px;\" align=\"center\">" +
                    $"<Label style=\"font-family:Helvetica;font-size:7.5pt;font-weight:normal;color:#111827;\">{EscapeHtml(FormatCellValue(celda.Valor))}</Label>" +
                    "</td>")) +
                "</tr>"));
            // Obtén la ruta física del archivo en el servidor
            
            return template
                .Replace("@@Cliente", EscapeHtml(reporte.Cliente))
                .Replace("@@Referencia", EscapeHtml(reporte.Referencia))
                .Replace("@@FechaEmision", reporte.FechaEmision.ToString("dd/MM/yyyy"))
                .Replace("@@ColumnHeaders", columnHeaders)
                .Replace("@@TableRows", rows);
            
            
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
                1 => string.Equals(config.MostrarCliente, "S", StringComparison.OrdinalIgnoreCase),
                2 => string.Equals(config.ReporteInterno, "S", StringComparison.OrdinalIgnoreCase),
                3 => string.Equals(config.ReporteExterno, "S", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }


        private static string FormatCellValue(double? value)
        {
            if (!value.HasValue)
                return string.Empty;

            return value.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string EscapeHtml(string? text)
        {
            return System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
        }

        private class ReportePerfilModel
        {
            public long CvePerfilN { get; set; }
            public string Cliente { get; set; } = string.Empty;
            public string Referencia { get; set; } = string.Empty;
            public DateTime FechaEmision { get; set; }
            public List<ReporteEtapaModel> Columnas { get; set; } = new List<ReporteEtapaModel>();
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
            public string? MostrarCliente { get; set; }
            public string? ReporteInterno { get; set; }
            public string? ReporteExterno { get; set; }
        }
    }
}
