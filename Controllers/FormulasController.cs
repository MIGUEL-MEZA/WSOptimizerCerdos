using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WSOptimizer7.App_Data;
using WSOptimizer7.Models;
using WSOptimizer7.Services;

namespace WSOptimizer7.Controllers
{
    [ApiController]
    [Route("api/formulas")]
    public class FormulasController : ControllerBase
    {
        private const int FormulaInicio = 1;
        private const int FormulaEnviada = 2;
        private const int FormulaAprobada = 3;
        private const int FormulaRechazada = 4;
        private const int FormulaSolicitudCambios = 5;

        private readonly IFormulaCargaParser cargaParser;
        private readonly IFormulaReportRenderer reportRenderer;
        private readonly IFormulaPdfService pdfService;
        private readonly IEmailService emailService;
        private readonly IConfiguration configuration;

        public FormulasController(
            IFormulaCargaParser cargaParser,
            IFormulaReportRenderer reportRenderer,
            IFormulaPdfService pdfService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            this.cargaParser = cargaParser;
            this.reportRenderer = reportRenderer;
            this.pdfService = pdfService;
            this.emailService = emailService;
            this.configuration = configuration;
        }

        [HttpGet("cliente/{codCliente}")]
        public IActionResult GetPendientesCliente(string codCliente, [FromQuery] int estatus = FormulaEnviada)
        {
            try
            {
                string cliente = NormalizeRequired(codCliente, "CodCliente");
                string sql = "SELECT p.CvePerfilN, p.CodCliente, COALESCE(NULLIF(c.NomClienteA, ''), c.NomCliente, '') AS NomCliente, p.FolioR, p.Titulo, p.CveEstatus, p.FecAct, p.UsuAct, " +
                             "COUNT(f.CveEtapa) AS CantidadFormulas " +
                             "FROM OptimizerC_PerfilN p " +
                             "INNER JOIN OptimizerC_PerfilN_Formulas f ON f.CvePerfilN = p.CvePerfilN " +
                             "LEFT JOIN Clientes c ON c.CodCliente = p.CodCliente " +
                             $"WHERE p.CodCliente = {SqlLiteral(cliente)} AND f.CveEstatus = {estatus} " +
                             "GROUP BY p.CvePerfilN, p.CodCliente, c.NomClienteA, c.NomCliente, p.FolioR, p.Titulo, p.CveEstatus, p.FecAct, p.UsuAct " +
                             "ORDER BY p.FecAct DESC";
                List<FormulaPerfilResumen> perfiles = MapResumen(Database.execQuery(sql));
                return OkResult("Consulta realizada correctamente.", new { codCliente = cliente, perfiles });
            }
            catch (FormulaBusinessException ex)
            {
                return BusinessError(ex);
            }
            catch (Exception ex)
            {
                return SystemError(ex);
            }
        }

        [HttpPost("perfil/{idPerfil:long}/carga")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Cargar(long idPerfil, [FromForm] FormulaCargaRequest request)
        {
            try
            {
                if (idPerfil <= 0)
                    throw new FormulaBusinessException(100, "El perfil no es valido.");
                if (request.Archivo == null || request.Archivo.Length == 0)
                    throw new FormulaBusinessException(100, "Debe adjuntar un archivo .exp o .txt.");

                string extension = Path.GetExtension(request.Archivo.FileName);
                if (!extension.Equals(".exp", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    throw new FormulaBusinessException(400, "El archivo debe tener extension .exp o .txt.");

                string contenido;
                using (var reader = new StreamReader(request.Archivo.OpenReadStream(), Encoding.UTF8, true))
                    contenido = await reader.ReadToEndAsync();

                List<FormulaCargaEtapa> cargadas = cargaParser.Parse(contenido);
                DataTable dtEsperadas = Database.execQuery(
                    "SELECT CveEtapa, NomEtapa, CodFormula FROM OptimizerC_PerfilN_Formulas " +
                    $"WHERE CvePerfilN = {idPerfil} AND CveEstatus = {FormulaEnviada}");
                if (dtEsperadas.Rows.Count == 0)
                    throw new FormulaBusinessException(301, "El perfil no tiene formulas enviadas pendientes de carga.");

                var esperadas = dtEsperadas.AsEnumerable()
                    .ToDictionary(r => GetString(r, "CodFormula").Trim(), r => r, StringComparer.OrdinalIgnoreCase);
                HashSet<string> recibidas = cargadas.Select(p => p.CodFormulaCarga.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                List<string> noReconocidas = recibidas.Except(esperadas.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
                List<string> faltantes = esperadas.Keys.Except(recibidas, StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
                if (noReconocidas.Count > 0 || faltantes.Count > 0)
                    throw new FormulaBusinessException(302, "Las formulas del archivo no corresponden exactamente a las formulas enviadas.", new { formulasNoReconocidas = noReconocidas, formulasFaltantes = faltantes, formulasEsperadas = esperadas.Keys.OrderBy(p => p) });

                foreach (FormulaCargaEtapa etapa in cargadas)
                {
                    DataRow row = esperadas[etapa.CodFormulaCarga.Trim()];
                    etapa.CveEtapa = Convert.ToInt32(row["CveEtapa"], CultureInfo.InvariantCulture);
                    etapa.CodFormulaEnviada = GetString(row, "CodFormula");
                    if (string.IsNullOrWhiteSpace(etapa.Nombre))
                        etapa.Nombre = GetString(row, "NomEtapa");
                }

                string? usuario = NormalizaUsuario(request.UsuAct);
                FormulaCargaDocumento documento = LoadDocumento(idPerfil) ?? new FormulaCargaDocumento();
                int siguienteProceso = documento.Procesos.Count == 0 ? 1 : documento.Procesos.Max(p => p.NumeroProceso) + 1;
                documento.Version = Math.Max(documento.Version, 1);
                documento.ProcesoActual = siguienteProceso;
                documento.Procesos.Add(new FormulaCargaProceso
                {
                    NumeroProceso = siguienteProceso,
                    FechaCarga = DateTime.Now,
                    UsuarioCarga = usuario,
                    Etapas = cargadas
                });

                string json = JsonConvert.SerializeObject(documento, Formatting.None);
                var statements = new List<string>
                {
                    "IF EXISTS (SELECT 1 FROM OptimizerC_PerfilN_Formulas_Carga WHERE CvePerfilN = " + idPerfil + ") " +
                    "UPDATE OptimizerC_PerfilN_Formulas_Carga SET ContenidoJson = " + SqlUnicodeLiteral(json) + ", FecAct = GETDATE(), UsuAct = " + SqlNullableLiteral(usuario) + " WHERE CvePerfilN = " + idPerfil + " " +
                    "ELSE INSERT INTO OptimizerC_PerfilN_Formulas_Carga (CvePerfilN, ContenidoJson, FecAlta, UsuAlta, FecAct, UsuAct) VALUES (" + idPerfil + ", " + SqlUnicodeLiteral(json) + ", GETDATE(), " + SqlNullableLiteral(usuario) + ", GETDATE(), " + SqlNullableLiteral(usuario) + ")"
                };
                statements.AddRange(cargadas.Select(p =>
                    "UPDATE OptimizerC_PerfilN_Formulas SET CodFormulaCarga = " + SqlLiteral(p.CodFormulaCarga) + ", FecAct = GETDATE(), UsuAct = " + SqlNullableLiteral(usuario) +
                    " WHERE CvePerfilN = " + idPerfil + " AND CveEtapa = " + p.CveEtapa));
                ExecuteTransaction(statements);

                return OkResult("Archivo cargado correctamente.", new { idPerfil, numeroProceso = siguienteProceso, formulas = cargadas.Select(p => new { p.CveEtapa, p.CodFormulaEnviada, p.CodFormulaCarga, p.Nombre }) });
            }
            catch (FormulaBusinessException ex)
            {
                return BusinessError(ex);
            }
            catch (Exception ex)
            {
                return SystemError(ex);
            }
        }

        [HttpGet("reportes")]
        public IActionResult GetReportes(
            [FromQuery] string? codCliente,
            [FromQuery] long? idPerfil,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin,
            [FromQuery] string? usuario,
            [FromQuery] int? estatus,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanoPagina = 25)
        {
            try
            {
                pagina = Math.Max(1, pagina);
                tamanoPagina = Math.Clamp(tamanoPagina, 1, 200);
                var filtros = new List<string> { "EXISTS (SELECT 1 FROM OptimizerC_PerfilN_Formulas_Carga c WHERE c.CvePerfilN = p.CvePerfilN)" };
                if (!string.IsNullOrWhiteSpace(codCliente)) filtros.Add("p.CodCliente = " + SqlLiteral(codCliente));
                if (idPerfil.HasValue) filtros.Add("p.CvePerfilN = " + idPerfil.Value);
                if (fechaInicio.HasValue) filtros.Add("p.FecAct >= " + SqlLiteral(fechaInicio.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                if (fechaFin.HasValue) filtros.Add("p.FecAct < DATEADD(day, 1, " + SqlLiteral(fechaFin.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) + ")");
                if (!string.IsNullOrWhiteSpace(usuario)) filtros.Add("p.UsuAct = " + SqlLiteral(usuario));
                if (estatus.HasValue) filtros.Add("p.CveEstatus = " + estatus.Value);

                int offset = (pagina - 1) * tamanoPagina;
                string where = string.Join(" AND ", filtros);
                DataTable dtTotal = Database.execQuery("SELECT COUNT(*) Total FROM OptimizerC_PerfilN p WHERE " + where);
                int total = Convert.ToInt32(dtTotal.Rows[0]["Total"], CultureInfo.InvariantCulture);
                string sql = "SELECT p.CvePerfilN, p.CodCliente, COALESCE(NULLIF(c.NomClienteA, ''), c.NomCliente, '') AS NomCliente, p.FolioR, p.Titulo, p.CveEstatus, p.FecAct, p.UsuAct, " +
                             "(SELECT COUNT(*) FROM OptimizerC_PerfilN_Formulas f WHERE f.CvePerfilN = p.CvePerfilN) CantidadFormulas " +
                             "FROM OptimizerC_PerfilN p LEFT JOIN Clientes c ON c.CodCliente = p.CodCliente WHERE " + where +
                             $" ORDER BY p.FecAct DESC OFFSET {offset} ROWS FETCH NEXT {tamanoPagina} ROWS ONLY";
                return OkResult("Consulta realizada correctamente.", new { pagina, tamanoPagina, total, registros = MapResumen(Database.execQuery(sql)) });
            }
            catch (Exception ex)
            {
                return SystemError(ex);
            }
        }

        [HttpGet("perfil/{idPerfil:long}/reporte")]
        public IActionResult GetReporte(long idPerfil, [FromQuery] int? numeroProceso = null)
        {
            try
            {
                return OkResult("Reporte obtenido correctamente.", LoadReporte(idPerfil, numeroProceso));
            }
            catch (FormulaBusinessException ex)
            {
                return BusinessError(ex);
            }
            catch (Exception ex)
            {
                return SystemError(ex);
            }
        }

        [HttpGet("perfil/{idPerfil:long}/formula/{codFormula}/html")]
        public IActionResult GetHtml(long idPerfil, string codFormula, [FromQuery] int? numeroProceso = null)
        {
            try
            {
                FormulaReporteDetalle reporte = LoadReporte(idPerfil, numeroProceso);
                FormulaCargaEtapa formula = FindFormula(reporte, codFormula);
                return Content(reportRenderer.RenderBody(reporte, formula), "text/html", Encoding.UTF8);
            }
            catch (FormulaBusinessException ex)
            {
                return BusinessError(ex);
            }
            catch (Exception ex)
            {
                return SystemError(ex);
            }
        }

        [HttpGet("perfil/{idPerfil:long}/formula/{codFormula}/pdf")]
        public IActionResult GetFormulaPdf(long idPerfil, string codFormula, [FromQuery] int? numeroProceso = null)
        {
            try
            {
                FormulaReporteDetalle reporte = LoadReporte(idPerfil, numeroProceso);
                FormulaCargaEtapa formula = FindFormula(reporte, codFormula);
                byte[] pdf = pdfService.Generate(reporte, new[] { formula });
                return File(pdf, "application/pdf", $"{idPerfil}_{SafeFileName(formula.CodFormulaCarga)}.pdf");
            }
            catch (FormulaBusinessException ex)
            {
                return BusinessError(ex);
            }
            catch (Exception ex)
            {
                return SystemError(ex);
            }
        }

        [HttpGet("perfil/{idPerfil:long}/pdf")]
        public IActionResult GetPerfilPdf(long idPerfil, [FromQuery] int? numeroProceso = null)
        {
            try
            {
                FormulaReporteDetalle reporte = LoadReporte(idPerfil, numeroProceso);
                byte[] pdf = pdfService.Generate(reporte, reporte.Formulas);
                return File(pdf, "application/pdf", $"{idPerfil}_formulas.pdf");
            }
            catch (FormulaBusinessException ex)
            {
                return BusinessError(ex);
            }
            catch (Exception ex)
            {
                return SystemError(ex);
            }
        }

        [HttpPost("dictamen")]
        public async Task<IActionResult> GuardarDictamen([FromBody] TemplateRequestModel request)
        {
            try
            {
                if (request == null || request.CvePerfilN <= 0)
                    throw new FormulaBusinessException(100, "CvePerfilN es obligatorio.");
                if (request.Etapas == null || request.Etapas.Count == 0)
                    throw new FormulaBusinessException(100, "Debe indicar al menos una etapa.");

                long idPerfil = request.CvePerfilN;

                DataTable dtActuales = Database.execQuery($"SELECT * FROM OptimizerC_PerfilN_Formulas WHERE CvePerfilN = {idPerfil}");
                if (dtActuales.Rows.Count == 0)
                    throw new FormulaBusinessException(200, "El perfil no tiene formulas registradas.");

                ValidateExactStages(dtActuales.AsEnumerable().Select(p => Convert.ToInt32(p["CveEtapa"])), request.Etapas.Select(p => p.CveEtapa));
                foreach (TemplateEtapaRequestModel etapa in request.Etapas)
                {
                    if (etapa.CveEstatus is not (FormulaAprobada or FormulaRechazada or FormulaSolicitudCambios))
                        throw new FormulaBusinessException(301, $"El estatus de la etapa {etapa.CveEtapa} no es valido para dictamen.");
                    if (etapa.CveEstatus is FormulaRechazada or FormulaSolicitudCambios && string.IsNullOrWhiteSpace(etapa.Nota))
                        throw new FormulaBusinessException(304, $"La etapa {etapa.CveEtapa} requiere comentario.");
                }

                int perfilEstatus = request.Etapas.All(p => p.CveEstatus == FormulaAprobada)
                    ? 4
                    : request.Etapas.All(p => p.CveEstatus == FormulaRechazada) ? 5 : 3;
                Guid operacion = Guid.NewGuid();
                string? usuario = NormalizaUsuario(request.UsuAct);
                var statements = new List<string>();
                foreach (TemplateEtapaRequestModel etapa in request.Etapas)
                {
                    DataRow actual = dtActuales.AsEnumerable().Single(p => Convert.ToInt32(p["CveEtapa"]) == etapa.CveEtapa);
                    statements.Add("UPDATE OptimizerC_PerfilN_Formulas SET CveEstatus = " + etapa.CveEstatus + ", Nota = " + SqlNullableLiteral(etapa.Nota) + ", FecAct = GETDATE(), UsuAct = " + SqlNullableLiteral(usuario) + $" WHERE CvePerfilN = {idPerfil} AND CveEtapa = {etapa.CveEtapa}");
                    statements.Add(BuildLogInsert(actual, etapa.CveEstatus, etapa.Nota, usuario, operacion, "DICTAMEN"));
                }
                statements.Add($"UPDATE OptimizerC_PerfilN SET CveEstatus = {perfilEstatus}, FecAct = GETDATE(), UsuAct = {SqlNullableLiteral(usuario)} WHERE CvePerfilN = {idPerfil}");
                ExecuteTransaction(statements);

                bool correoEnviado = false;
                string? errorCorreo = null;
                try
                {
                    List<string> destinatarios = GetConfiguredRecipients();
                    if (destinatarios.Count > 0)
                    {
                        await emailService.SendAsync(new EmailMessage
                        {
                            To = destinatarios,
                            Subject = $"Dictamen de formulas - Perfil {idPerfil}",
                            HtmlBody = BuildDictamenEmail(idPerfil, request.Etapas)
                        });
                        correoEnviado = true;
                    }
                }
                catch (Exception exCorreo)
                {
                    errorCorreo = exCorreo.Message;
                }

                return OkResult(errorCorreo == null ? "Dictamen guardado correctamente." : "El dictamen fue guardado, pero el correo no pudo enviarse.", new { idPerfil, estatusPerfil = perfilEstatus, idOperacion = operacion, correoEnviado, errorCorreo });
            }
            catch (FormulaBusinessException ex)
            {
                return BusinessError(ex);
            }
            catch (Exception ex)
            {
                return SystemError(ex);
            }
        }

        [HttpPost("reproceso")]
        public IActionResult IniciarReproceso([FromBody] TemplateRequestModel request)
        {
            try
            {
                if (request == null || request.CvePerfilN <= 0)
                    throw new FormulaBusinessException(100, "CvePerfilN es obligatorio.");
                if (request.Etapas == null || request.Etapas.Count == 0)
                    throw new FormulaBusinessException(100, "Debe indicar al menos una etapa.");

                long idPerfil = request.CvePerfilN;
                DataTable dtActuales = Database.execQuery($"SELECT * FROM OptimizerC_PerfilN_Formulas WHERE CvePerfilN = {idPerfil}");
                if (dtActuales.Rows.Count == 0)
                    throw new FormulaBusinessException(200, "El perfil no tiene formulas para reprocesar.");
                ValidateExactStages(dtActuales.AsEnumerable().Select(p => Convert.ToInt32(p["CveEtapa"])), request.Etapas.Select(p => p.CveEtapa));
                if (request.Etapas.Any(p => p.CveAccion is not (1 or 2)))
                    throw new FormulaBusinessException(100, "Cada etapa debe indicar cveAccion 1 o 2.");

                Guid operacion = Guid.NewGuid();
                string? usuario = NormalizaUsuario(request.UsuAct);
                var statements = new List<string>();
                foreach (TemplateEtapaRequestModel etapa in request.Etapas)
                {
                    DataRow actual = dtActuales.AsEnumerable().Single(p => Convert.ToInt32(p["CveEtapa"]) == etapa.CveEtapa);
                    statements.Add(BuildLogInsert(actual, GetNullableInt(actual, "CveEstatus"), GetString(actual, "Nota"), usuario, operacion, "INICIO_REPROCESO"));
                    string codigo = etapa.CveAccion == 1 ? "NULL" : "CodFormula";
                    statements.Add("UPDATE OptimizerC_PerfilN_Formulas SET CveAccion = " + etapa.CveAccion + ", CveEstatus = 1, Nota = " + SqlNullableLiteral(etapa.Nota) + ", CodFormula = " + codigo + ", CodFormulaCarga = NULL, FecAct = GETDATE(), UsuAct = " + SqlNullableLiteral(usuario) + $" WHERE CvePerfilN = {idPerfil} AND CveEtapa = {etapa.CveEtapa}");
                }
                statements.Add($"UPDATE OptimizerC_PerfilN SET CveEstatus = 1, FecAct = GETDATE(), UsuAct = {SqlNullableLiteral(usuario)} WHERE CvePerfilN = {idPerfil}");
                ExecuteTransaction(statements);
                return OkResult("El perfil quedo listo para reproceso.", new { idPerfil, estatusPerfil = 1, idOperacion = operacion });
            }
            catch (FormulaBusinessException ex)
            {
                return BusinessError(ex);
            }
            catch (Exception ex)
            {
                return SystemError(ex);
            }
        }

        private FormulaReporteDetalle LoadReporte(long idPerfil, int? numeroProceso)
        {
            DataTable dtPerfil = Database.execQuery(
                "SELECT TOP 1 p.*, COALESCE(NULLIF(c.NomClienteA, ''), c.NomCliente, '') AS NomCliente " +
                "FROM OptimizerC_PerfilN p LEFT JOIN Clientes c ON c.CodCliente = p.CodCliente " +
                $"WHERE p.CvePerfilN = {idPerfil}");
            if (dtPerfil.Rows.Count == 0)
                throw new FormulaBusinessException(200, "No existe el perfil solicitado.");
            FormulaCargaDocumento documento = LoadDocumento(idPerfil) ?? throw new FormulaBusinessException(200, "El perfil no tiene informacion cargada.");
            int numero = numeroProceso ?? documento.ProcesoActual;
            FormulaCargaProceso proceso = documento.Procesos.FirstOrDefault(p => p.NumeroProceso == numero)
                ?? throw new FormulaBusinessException(200, $"No existe el proceso {numero} para el perfil.");
            DataRow perfil = dtPerfil.Rows[0];
            return new FormulaReporteDetalle
            {
                IdPerfil = idPerfil,
                CodCliente = GetString(perfil, "CodCliente"),
                Cliente = GetString(perfil, "NomCliente"),
                Folio = GetString(perfil, "FolioR"),
                Titulo = GetString(perfil, "Titulo"),
                NumeroProceso = numero,
                Formulas = proceso.Etapas
            };
        }

        private static FormulaCargaEtapa FindFormula(FormulaReporteDetalle reporte, string codFormula)
        {
            return reporte.Formulas.FirstOrDefault(p => p.CodFormulaCarga.Equals(codFormula, StringComparison.OrdinalIgnoreCase) || p.CodFormulaEnviada.Equals(codFormula, StringComparison.OrdinalIgnoreCase))
                ?? throw new FormulaBusinessException(200, "No existe la formula solicitada dentro del reporte.");
        }

        private static FormulaCargaDocumento? LoadDocumento(long idPerfil)
        {
            DataTable dt = Database.execQuery($"SELECT TOP 1 ContenidoJson FROM OptimizerC_PerfilN_Formulas_Carga WHERE CvePerfilN = {idPerfil}");
            if (dt.Rows.Count == 0)
                return null;
            return JsonConvert.DeserializeObject<FormulaCargaDocumento>(GetString(dt.Rows[0], "ContenidoJson"));
        }

        private static List<FormulaPerfilResumen> MapResumen(DataTable dt)
        {
            return dt.AsEnumerable().Select(row => new FormulaPerfilResumen
            {
                IdPerfil = Convert.ToInt64(row["CvePerfilN"], CultureInfo.InvariantCulture),
                CodCliente = GetString(row, "CodCliente"),
                Cliente = GetString(row, "NomCliente"),
                Folio = GetString(row, "FolioR"),
                Titulo = GetString(row, "Titulo"),
                Estatus = GetNullableInt(row, "CveEstatus"),
                Fecha = GetNullableDate(row, "FecAct"),
                Usuario = GetString(row, "UsuAct"),
                CantidadFormulas = GetNullableInt(row, "CantidadFormulas") ?? 0
            }).ToList();
        }

        private static void ValidateExactStages(IEnumerable<int> esperadas, IEnumerable<int> recibidas)
        {
            List<int> expected = esperadas.Distinct().OrderBy(p => p).ToList();
            List<int> receivedRaw = recibidas.ToList();
            if (receivedRaw.Count != receivedRaw.Distinct().Count())
                throw new FormulaBusinessException(100, "El request contiene etapas duplicadas.");
            List<int> received = receivedRaw.OrderBy(p => p).ToList();
            List<int> faltantes = expected.Except(received).ToList();
            List<int> adicionales = received.Except(expected).ToList();
            if (faltantes.Count > 0)
                throw new FormulaBusinessException(307, "No se recibieron todas las etapas registradas.", new { etapasFaltantes = faltantes, etapasEsperadas = expected, etapasRecibidas = received });
            if (adicionales.Count > 0)
                throw new FormulaBusinessException(308, "El request contiene etapas no registradas.", new { etapasNoRegistradas = adicionales, etapasEsperadas = expected });
        }

        private static string BuildLogInsert(DataRow actual, int? estatus, string nota, string? usuario, Guid operacion, string movimiento)
        {
            return "INSERT INTO OptimizerC_PerfilN_Formulas_Log (CvePerfilN, CveEtapa, CveEtapaFlujo, NomEtapa, CveAccion, Nota, CodFormula, CodFormulaCarga, CveEstatus, TipoMovimiento, IdOperacion, FecAct, UsuAct) VALUES (" +
                   Convert.ToInt64(actual["CvePerfilN"], CultureInfo.InvariantCulture) + "," + Convert.ToInt32(actual["CveEtapa"], CultureInfo.InvariantCulture) + "," + SqlInt(GetNullableInt(actual, "CveEtapaFlujo")) + "," + SqlNullableLiteral(GetString(actual, "NomEtapa")) + "," + SqlInt(GetNullableInt(actual, "CveAccion")) + "," + SqlNullableLiteral(nota) + "," + SqlNullableLiteral(GetString(actual, "CodFormula")) + "," + SqlNullableLiteral(GetString(actual, "CodFormulaCarga")) + "," + SqlInt(estatus) + "," + SqlLiteral(movimiento) + "," + SqlLiteral(operacion.ToString()) + ",GETDATE()," + SqlNullableLiteral(usuario) + ")";
        }

        private static void ExecuteTransaction(IEnumerable<string> statements)
        {
            string body = string.Join(";", statements.Where(p => !string.IsNullOrWhiteSpace(p)));
            string sql = "SET XACT_ABORT ON; BEGIN TRY BEGIN TRAN; " + body + "; COMMIT; END TRY BEGIN CATCH IF @@TRANCOUNT > 0 ROLLBACK; THROW; END CATCH";
            Database.execNonQuery(sql);
        }

        private List<string> GetConfiguredRecipients()
        {
            return configuration.GetSection("FormatEmail:Destinatarios").GetChildren()
                .Select(p => p.Value).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string BuildDictamenEmail(long idPerfil, IEnumerable<TemplateEtapaRequestModel> etapas)
        {
            var html = new StringBuilder($"<h2>Dictamen del perfil {idPerfil}</h2><table border='1' cellpadding='6' cellspacing='0'><tr><th>Etapa</th><th>Estatus</th><th>Comentario</th></tr>");
            foreach (TemplateEtapaRequestModel etapa in etapas)
                html.Append($"<tr><td>{etapa.CveEtapa}</td><td>{etapa.CveEstatus}</td><td>{WebUtility.HtmlEncode(etapa.Nota)}</td></tr>");
            html.Append("</table>");
            return html.ToString();
        }

        private IActionResult OkResult<T>(string message, T data) => Ok(new ApiResult<T> { Code = 0, Message = message, Data = data, TraceId = HttpContext.TraceIdentifier });
        private IActionResult BusinessError(FormulaBusinessException ex)
        {
            int status = ex.Code == 200 ? StatusCodes.Status404NotFound : ex.Code is 300 or 301 or 305 ? StatusCodes.Status409Conflict : ex.Code >= 302 ? StatusCodes.Status422UnprocessableEntity : StatusCodes.Status400BadRequest;
            return StatusCode(status, new ApiResult<object> { Code = ex.Code, Message = ex.Message, Data = ex.DataPayload, TraceId = HttpContext.TraceIdentifier });
        }
        private IActionResult SystemError(Exception ex) => StatusCode(500, new ApiResult<object> { Code = 9000, Message = "Ocurrio un error interno procesando la solicitud.", Data = new { error = ex.Message }, TraceId = HttpContext.TraceIdentifier });

        private static string NormalizeRequired(string value, string name)
        {
            string result = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(result)) throw new FormulaBusinessException(100, $"{name} es obligatorio.");
            return result;
        }
        // El usuario es alfanumerico (columnas UsuAct/UsuAlta varchar), no debe convertirse a numero
        private static string? NormalizaUsuario(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static string SqlLiteral(string? value) => "'" + (value ?? "").Trim().Replace("'", "''") + "'";
        private static string SqlUnicodeLiteral(string? value) => "N'" + (value ?? "").Replace("'", "''") + "'";
        private static string SqlNullableLiteral(string? value) => string.IsNullOrWhiteSpace(value) ? "NULL" : SqlUnicodeLiteral(value);
        private static string SqlLong(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "NULL";
        private static string SqlInt(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "NULL";
        private static string SafeFileName(string value) => string.Concat((value ?? "formula").Where(p => !Path.GetInvalidFileNameChars().Contains(p)));
        private static string GetString(DataRow row, string column) => row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? row[column]?.ToString() ?? "" : "";
        private static int? GetNullableInt(DataRow row, string column) => int.TryParse(GetString(row, column), out int value) ? value : null;
        private static long? GetNullableLong(DataRow row, string column) => long.TryParse(GetString(row, column), out long value) ? value : null;
        private static DateTime? GetNullableDate(DataRow row, string column) => DateTime.TryParse(GetString(row, column), out DateTime value) ? value : null;
    }
}
