using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data;
using System.Globalization;
using System.Text;
using WSOptimizer7.App_Data;
using WSOptimizer7.Config;
using WSOptimizer7.Models;
using WSOptimizer7.Services;

namespace WSOptimizer7.Controllers
{
    public class FormatV2Controller : Controller
    {
        private const int EstatusPendiente = 1;
        private const int EstatusEnviado = 2;

        private static readonly Dictionary<int, string> EtapaCodigos = new Dictionary<int, string>
        {
            { 1, "IN" },
            { 2, "CR" },
            { 3, "DE" },
            { 4, "F1" },
            { 5, "F2" },
            { 6, "FR" }
        };

        private DataTable dtVar = new DataTable();
        private Dictionary<string, Dictionary<string, object?>> dictVar = new Dictionary<string, Dictionary<string, object?>>();
        private Dictionary<string, string> dictEtapas = new Dictionary<string, string>();
        private Dictionary<int, FormulaEtapaInfo> dictFormulas = new Dictionary<int, FormulaEtapaInfo>();
        private readonly IEmailService emailService;
        private readonly IEmailTemplateRenderer emailTemplateRenderer;

        public FormatV2Controller(IEmailService emailService, IEmailTemplateRenderer emailTemplateRenderer)
        {
            this.emailService = emailService;
            this.emailTemplateRenderer = emailTemplateRenderer;
        }

        [HttpPost]
        [Route("api/template")]
        public async Task<IActionResult> GetOptimizerModelN([FromBody] TemplateRequestModel objReq)
        {
            Guid idOperacion = Guid.NewGuid();

            try
            {
                ValidarRequestBase(objReq);

                InicializarCatalogos();

                FormulaOperationResult formulaResult = GuardarFormulasSeleccionadas(objReq, idOperacion);
                dictFormulas = LoadFormulas(objReq.CvePerfilN);

                HashSet<int>? etapasSeleccionadas = ParseEtapas(objReq.Etapas);
                DataTable dtRef = Database.execQuery($"SELECT * FROM OptimizerC_PerfilN_Resultado WHERE CvePerfilN = {objReq.CvePerfilN}");

                if (dtRef == null || dtRef.Rows.Count == 0)
                    return BadRequest("No se encontraron datos para el perfil solicitado.");

                List<TemplateSP> lista = ConvertirDataTable(dtRef, etapasSeleccionadas);
                List<string> lineas = lista.Select(GenerarLinea).ToList();

                string basePath = Path.Combine(Directory.GetCurrentDirectory(), "Archivos");
                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string fileName = $"{objReq.CvePerfilN}_{timestamp}.EXP";
                string fullPath = Path.Combine(basePath, fileName);

                using (var writer = new StreamWriter(fullPath, false, new UTF8Encoding(false)))
                {
                    foreach (string linea in lineas)
                        writer.WriteLine(linea);
                }


                bool correoEnviado = false;
                if (GetConfigBool("FormatEmail:Enviar", false))
                {
                    try
                    {
                        await EnviarCorreoFormat(objReq, fullPath);
                        correoEnviado = true;
                        ActualizarEstatusFormulas(objReq.CvePerfilN, GetEtapasParaEstatus(etapasSeleccionadas), EstatusEnviado, objReq.UsuAct, idOperacion, "ENVIO_CORREO", "Archivo EXP enviado por correo.");
                    }
                    catch (Exception exCorreo)
                    {
                        RegistrarEventoFormulas(objReq.CvePerfilN, GetEtapasParaEstatus(etapasSeleccionadas), objReq.UsuAct, idOperacion, "ERROR_ENVIO", "Error al enviar correo: " + exCorreo.Message);
                        return BadRequest(new
                        {
                            mensaje = "Archivo generado, pero ocurriÃ³ un error al enviar el correo.",
                            archivo = fileName,
                            ruta = fullPath,
                            errorCorreo = exCorreo.Message,
                            formulas = formulaResult.Formulas
                        });
                    }
                }

                return Ok(new
                {
                    mensaje = "Archivo generado correctamente.",
                    archivo = fileName,
                    ruta = fullPath,
                    correoEnviado,
                    idOperacion,
                    formulas = formulaResult.Formulas
                });
            }
            catch (Exception ex)
            {
                return BadRequest("Error procesando la solicitud: " + ex);
            }
        }

        [HttpPost]
        [Route("api/template/formulas")]
        public IActionResult GuardarFormulas([FromBody] TemplateRequestModel objReq)
        {
            try
            {
                ValidarRequestBase(objReq);

                if (objReq.Etapas == null || objReq.Etapas.Count == 0)
                    return BadRequest("Debe indicar al menos una etapa para guardar formulas.");

                FormulaOperationResult result = GuardarFormulasSeleccionadas(objReq, Guid.NewGuid());
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest("Error guardando formulas: " + ex);
            }
        }

        [HttpPost]
        [Route("api/template/allix-exp")]
        public IActionResult GenerarAllixExp([FromBody] TemplateRequestModel objReq)
        {
            Guid idOperacion = Guid.NewGuid();

            try
            {
                ValidarRequestBase(objReq);

                if (objReq.Etapas == null || objReq.Etapas.Count == 0)
                    return BadRequest("Debe indicar al menos una etapa para generar archivos Allix EXP.");

                InicializarCatalogos();

                FormulaOperationResult formulaResult = GuardarFormulasSeleccionadas(objReq, idOperacion);
                dictFormulas = LoadFormulas(objReq.CvePerfilN);

                HashSet<int> etapasSeleccionadas = ParseEtapas(objReq.Etapas) ?? new HashSet<int>();
                DataTable dtRef = Database.execQuery($"SELECT * FROM OptimizerC_PerfilN_Resultado WHERE CvePerfilN = {objReq.CvePerfilN}");

                if (dtRef == null || dtRef.Rows.Count == 0)
                    return BadRequest("No se encontraron datos para el perfil solicitado.");

                Dictionary<int, Dictionary<string, decimal>> valoresPorEtapa = GetValoresAllixPorEtapa(dtRef, etapasSeleccionadas);
                List<AllixExpFileResponse> archivos = GenerarArchivosAllixExp(objReq.CvePerfilN, etapasSeleccionadas, valoresPorEtapa);

                return Ok(new
                {
                    mensaje = "Archivos Allix EXP generados correctamente.",
                    formato = "SGLMIX",
                    plantilla = GetFullPath(GetConfigValue("AllixExp:TemplatePath", "p.exp")),
                    idOperacion,
                    archivos,
                    formulas = formulaResult.Formulas
                });
            }
            catch (Exception ex)
            {
                return BadRequest("Error generando archivos Allix EXP: " + ex);
            }
        }

        private static void ValidarRequestBase(TemplateRequestModel? objReq)
        {
            if (objReq == null)
                throw new Exception("El cuerpo de la solicitud no es vÃ¡lido.");

            if (objReq.CvePerfilN <= 0)
                throw new Exception("El parÃ¡metro CvePerfilN no es vÃ¡lido.");
        }

        private void InicializarCatalogos()
        {
            dtVar = Database.execQuery("SELECT * FROM CatOptimizerC_Variables");
            dictVar = dtVar.AsEnumerable()
                .ToDictionary(
                    r => r["CveVariable"]?.ToString() ?? "",
                    r => dtVar.Columns.Cast<DataColumn>()
                        .ToDictionary(
                            c => c.ColumnName,
                            c => r[c] is DBNull ? null : r[c],
                            StringComparer.OrdinalIgnoreCase
                        ),
                    StringComparer.OrdinalIgnoreCase
                );

            DataTable dtEtapa = Database.execQuery("SELECT * FROM CatOptimizerC_Etapas");
            dictEtapas = new Dictionary<string, string>();

            foreach (DataRow row in dtEtapa.Rows)
            {
                string cveEtapa = GetString(row, "CveEtapa");
                if (string.IsNullOrWhiteSpace(cveEtapa))
                    continue;

                string codigoFormat = GetString(row, "CodigoFormat");
                if (!string.IsNullOrWhiteSpace(codigoFormat))
                    dictEtapas[cveEtapa] = codigoFormat;
            }
        }

        private FormulaOperationResult GuardarFormulasSeleccionadas(TemplateRequestModel objReq, Guid idOperacion)
        {
            List<int> etapasSeleccionadas = GetRequestedStageKeys(objReq.Etapas);
            var result = new FormulaOperationResult
            {
                Ok = true,
                CvePerfilN = objReq.CvePerfilN,
                IdOperacion = idOperacion
            };

            if (etapasSeleccionadas.Count == 0)
                return result;

            string codCliente = GetCodCliente(objReq.CvePerfilN);
            if (string.IsNullOrWhiteSpace(codCliente))
                throw new Exception("No se encontrÃ³ CodCliente para el perfil solicitado.");

            Dictionary<int, TemplateEtapaRequestModel> requestEtapas = objReq.Etapas
                .GroupBy(GetRequestCveEtapa)
                .ToDictionary(g => g.Key, g => g.First());

            Dictionary<int, FormulaEtapaInfo> etapasPerfil = LoadPerfilEtapas(objReq.CvePerfilN, etapasSeleccionadas);
            Dictionary<int, FormulaEtapaInfo> formulasActuales = LoadFormulas(objReq.CvePerfilN);
            HashSet<string> codigosUsados = LoadCodFormulasUsados();
            HashSet<string> formulaColumns = LoadTableColumns("OptimizerC_PerfilN_Formulas");
            HashSet<string> logColumns = LoadTableColumns("OptimizerC_PerfilN_Formulas_Log");

            foreach (int cveEtapa in etapasSeleccionadas.OrderBy(p => p))
            {
                if (!etapasPerfil.TryGetValue(cveEtapa, out FormulaEtapaInfo? etapaPerfil))
                    throw new Exception($"No existe la etapa {cveEtapa} para el perfil {objReq.CvePerfilN} en OptimizerC_PerfilN_Etapas.");

                requestEtapas.TryGetValue(cveEtapa, out TemplateEtapaRequestModel? etapaReq);
                formulasActuales.TryGetValue(cveEtapa, out FormulaEtapaInfo? formulaActual);

                string codFormula = formulaActual?.CodFormula?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(codFormula))
                    codFormula = GenerarCodFormula(codCliente, etapaPerfil, codigosUsados);
                else
                    codigosUsados.Add(codFormula);

                string codFormulaCarga = formulaActual?.CodFormulaCarga?.Trim() ?? "";
                int cveEstatus = etapaReq?.CveEstatus ?? formulaActual?.CveEstatus ?? EstatusPendiente;

                FormulaEtapaInfo formula = new FormulaEtapaInfo
                {
                    CvePerfilN = objReq.CvePerfilN,
                    CveEtapa = cveEtapa,
                    CveEtapaFlujo = etapaPerfil.CveEtapaFlujo,
                    NomEtapa = etapaPerfil.NomEtapa,
                    CveAccion = etapaReq?.CveAccion ?? formulaActual?.CveAccion ?? 1,
                    Nota = etapaReq?.Nota ?? formulaActual?.Nota ?? "",
                    CodFormula = codFormula,
                    CodFormulaCarga = codFormulaCarga,
                    CveEstatus = cveEstatus,
                    UsuAct = objReq.UsuAct
                };

                bool isInsert = formulaActual == null;
                UpsertFormula(formula, formulaColumns, isInsert);
                InsertFormulaLog(formula, logColumns, isInsert ? "ALTA" : "ACTUALIZACION", idOperacion);

                result.Formulas.Add(ToFormulaResponse(formula));
            }

            return result;
        }

        private string GetCodCliente(long cvePerfilN)
        {
            DataTable dtPerfil = Database.execQuery($"SELECT TOP 1 CodCliente FROM OptimizerC_PerfilN WHERE CvePerfilN = {cvePerfilN}");
            if (dtPerfil == null || dtPerfil.Rows.Count == 0)
                return "";

            return GetString(dtPerfil.Rows[0], "CodCliente");
        }

        private Dictionary<int, FormulaEtapaInfo> LoadPerfilEtapas(long cvePerfilN, List<int> etapas)
        {
            string filtroEtapas = string.Join(",", etapas.Select(p => p.ToString(CultureInfo.InvariantCulture)));
            string sql = "SELECT CvePerfilN, CveEtapa, CveEtapaFlujo, NomEtapa " +
                         "FROM OptimizerC_PerfilN_Etapas " +
                         $"WHERE CvePerfilN = {cvePerfilN} AND CveEtapa IN ({filtroEtapas}) " +
                         "ORDER BY CveEtapa";

            DataTable dtEtapas = Database.execQuery(sql);
            var result = new Dictionary<int, FormulaEtapaInfo>();

            foreach (DataRow row in dtEtapas.Rows)
            {
                int cveEtapa = Convert.ToInt32(row["CveEtapa"]);
                result[cveEtapa] = new FormulaEtapaInfo
                {
                    CvePerfilN = cvePerfilN,
                    CveEtapa = cveEtapa,
                    CveEtapaFlujo = GetNullableInt(row, "CveEtapaFlujo"),
                    NomEtapa = GetString(row, "NomEtapa")
                };
            }

            return result;
        }

        private Dictionary<int, FormulaEtapaInfo> LoadFormulas(long cvePerfilN)
        {
            string sql = $"SELECT * FROM OptimizerC_PerfilN_Formulas WHERE CvePerfilN = {cvePerfilN}";
            DataTable dtFormulas = Database.execQuery(sql);
            var formulas = new Dictionary<int, FormulaEtapaInfo>();

            if (dtFormulas == null || dtFormulas.Rows.Count == 0)
                return formulas;

            foreach (DataRow row in dtFormulas.Rows)
            {
                int cveEtapa = Convert.ToInt32(row["CveEtapa"]);
                formulas[cveEtapa] = new FormulaEtapaInfo
                {
                    CvePerfilN = cvePerfilN,
                    CveEtapa = cveEtapa,
                    CveEtapaFlujo = GetNullableInt(row, "CveEtapaFlujo"),
                    NomEtapa = GetString(row, "NomEtapa"),
                    CveAccion = GetNullableInt(row, "CveAccion"),
                    Nota = GetString(row, "Nota"),
                    CodFormula = GetString(row, "CodFormula"),
                    CodFormulaCarga = GetString(row, "CodFormulaCarga"),
                    CveEstatus = GetNullableInt(row, "CveEstatus")
                };
            }

            return formulas;
        }

        private HashSet<string> LoadCodFormulasUsados()
        {
            var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string sql in new[]
            {
                "SELECT CodFormula FROM OptimizerC_PerfilN_Formulas WHERE CodFormula IS NOT NULL AND LTRIM(RTRIM(CodFormula)) <> ''",
                "SELECT CodFormula FROM OptimizerC_PerfilN_Formulas_Log WHERE CodFormula IS NOT NULL AND LTRIM(RTRIM(CodFormula)) <> ''"
            })
            {
                DataTable dtCodigos = Database.execQuery(sql);
                foreach (DataRow row in dtCodigos.Rows)
                {
                    string codigo = GetString(row, "CodFormula").Trim();
                    if (!string.IsNullOrWhiteSpace(codigo))
                        codigos.Add(codigo);
                }
            }

            return codigos;
        }

        private static string GenerarCodFormula(string codCliente, FormulaEtapaInfo etapa, HashSet<string> codigosUsados)
        {
            string cliente = NormalizarCodCliente(codCliente);
            string codEtapa = GetCodigoEtapa(etapa);
            string prefijo = cliente + codEtapa;
            int consecutivo = GetSiguienteConsecutivo(prefijo, codigosUsados);
            string codFormula = prefijo + consecutivo.ToString("00", CultureInfo.InvariantCulture);

            if (codFormula.Length > 10)
                throw new Exception($"El cÃ³digo de fÃ³rmula generado excede 10 caracteres: {codFormula}");

            codigosUsados.Add(codFormula);
            return codFormula;
        }

        private static string NormalizarCodCliente(string codCliente)
        {
            string normalizado = new string((codCliente ?? "")
                .Where(char.IsLetterOrDigit)
                .ToArray());

            if (normalizado.Length > 6)
                normalizado = normalizado.Substring(0, 6);

            return normalizado;
        }

        private static string GetCodigoEtapa(FormulaEtapaInfo etapa)
        {
            int cveEtapaCatalogo = etapa.CveEtapaFlujo.GetValueOrDefault(etapa.CveEtapa);
            if (EtapaCodigos.TryGetValue(cveEtapaCatalogo, out string? codigo))
                return codigo;

            string nomEtapa = (etapa.NomEtapa ?? "").Trim().ToUpperInvariant();
            if (nomEtapa.StartsWith("INICIADOR"))
                return "IN";
            if (nomEtapa.StartsWith("CRECIMIENTO"))
                return "CR";
            if (nomEtapa.StartsWith("DESARROLLO"))
                return "DE";
            if (nomEtapa.StartsWith("FINALIZADOR RACTOPAMINA"))
                return "FR";
            if (nomEtapa.StartsWith("FINALIZADOR II"))
                return "F2";
            if (nomEtapa.StartsWith("FINALIZADOR I"))
                return "F1";

            throw new Exception($"No se pudo determinar el cÃ³digo de etapa para CveEtapa {etapa.CveEtapa}.");
        }

        private static int GetSiguienteConsecutivo(string prefijo, HashSet<string> codigosUsados)
        {
            int max = 0;

            foreach (string codigo in codigosUsados)
            {
                if (!codigo.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) || codigo.Length < prefijo.Length + 2)
                    continue;

                string sufijo = codigo.Substring(prefijo.Length, 2);
                if (int.TryParse(sufijo, out int consecutivo) && consecutivo > max)
                    max = consecutivo;
            }

            int siguiente = max + 1;
            if (siguiente > 99)
                throw new Exception($"Se excediÃ³ el consecutivo mÃ¡ximo para el prefijo {prefijo}.");

            return siguiente;
        }

        private void UpsertFormula(FormulaEtapaInfo formula, HashSet<string> columns, bool isInsert)
        {
            if (isInsert)
            {
                var fields = new List<string>
                {
                    "CvePerfilN",
                    "CveEtapa",
                    "CveEtapaFlujo",
                    "NomEtapa",
                    "CveAccion",
                    "Nota",
                    "CodFormula",
                    "CodFormulaCarga",
                    "FecAlta",
                    "UsuAlta",
                    "FecAct",
                    "UsuAct"
                };
                var values = new List<string>
                {
                    formula.CvePerfilN.ToString(CultureInfo.InvariantCulture),
                    formula.CveEtapa.ToString(CultureInfo.InvariantCulture),
                    SqlNullableInt(formula.CveEtapaFlujo),
                    SqlNullableLiteral(formula.NomEtapa),
                    SqlNullableInt(formula.CveAccion),
                    SqlNullableLiteral(formula.Nota),
                    SqlNullableLiteral(formula.CodFormula),
                    SqlNullableLiteral(formula.CodFormulaCarga),
                    "GETDATE()",
                    SqlNullableLong(ParseUsuAct(formula.UsuAct)),
                    "GETDATE()",
                    SqlNullableLong(ParseUsuAct(formula.UsuAct))
                };

                AddOptionalField(fields, values, columns, "CveEstatus", SqlNullableInt(formula.CveEstatus));

                string insert = "INSERT INTO OptimizerC_PerfilN_Formulas (" + string.Join(", ", fields) + ") VALUES (" + string.Join(", ", values) + ")";
                Database.execNonQuery(insert);
                return;
            }

            var sets = new List<string>
            {
                $"CveEtapaFlujo = {SqlNullableInt(formula.CveEtapaFlujo)}",
                $"NomEtapa = {SqlNullableLiteral(formula.NomEtapa)}",
                $"CveAccion = {SqlNullableInt(formula.CveAccion)}",
                $"Nota = {SqlNullableLiteral(formula.Nota)}",
                $"CodFormula = {SqlNullableLiteral(formula.CodFormula)}",
                $"CodFormulaCarga = {SqlNullableLiteral(formula.CodFormulaCarga)}",
                "FecAct = GETDATE()",
                $"UsuAct = {SqlNullableLong(ParseUsuAct(formula.UsuAct))}"
            };

            if (columns.Contains("CveEstatus"))
                sets.Add($"CveEstatus = {SqlNullableInt(formula.CveEstatus)}");

            string update = "UPDATE OptimizerC_PerfilN_Formulas SET " + string.Join(", ", sets) +
                            $" WHERE CvePerfilN = {formula.CvePerfilN} AND CveEtapa = {formula.CveEtapa}";

            Database.execNonQuery(update);
        }

        private void InsertFormulaLog(FormulaEtapaInfo formula, HashSet<string> columns, string tipoMovimiento, Guid idOperacion)
        {
            var fields = new List<string>
            {
                "CvePerfilN",
                "CveEtapa",
                "CveEtapaFlujo",
                "NomEtapa",
                "CveAccion",
                "Nota",
                "CodFormula",
                "CodFormulaCarga",
                "FecAct",
                "UsuAct"
            };
            var values = new List<string>
            {
                formula.CvePerfilN.ToString(CultureInfo.InvariantCulture),
                formula.CveEtapa.ToString(CultureInfo.InvariantCulture),
                SqlNullableInt(formula.CveEtapaFlujo),
                SqlNullableLiteral(formula.NomEtapa),
                SqlNullableInt(formula.CveAccion),
                SqlNullableLiteral(formula.Nota),
                SqlNullableLiteral(formula.CodFormula),
                SqlNullableLiteral(formula.CodFormulaCarga),
                "GETDATE()",
                SqlNullableLong(ParseUsuAct(formula.UsuAct))
            };

            AddOptionalField(fields, values, columns, "CveEstatus", SqlNullableInt(formula.CveEstatus));
            AddOptionalField(fields, values, columns, "TipoMovimiento", SqlNullableLiteral(tipoMovimiento));
            AddOptionalField(fields, values, columns, "IdOperacion", SqlNullableLiteral(idOperacion.ToString()));

            string insert = "INSERT INTO OptimizerC_PerfilN_Formulas_Log (" + string.Join(", ", fields) + ") VALUES (" + string.Join(", ", values) + ")";
            Database.execNonQuery(insert);
        }

        private List<TemplateSP> ConvertirDataTable(DataTable dt, HashSet<int>? etapasSeleccionadas = null)
        {
            var lista = new List<TemplateSP>();

            if (dt == null || dt.Rows.Count == 0)
                return lista;

            string response = GetString(dt.Rows[0], "Response2");
            if (string.IsNullOrWhiteSpace(response))
                response = GetString(dt.Rows[0], "Response");

            if (string.IsNullOrWhiteSpace(response))
                return lista;

            ResponseModel? objResp = JsonConvert.DeserializeObject<ResponseModel>(response);
            if (objResp?.Variables == null)
                return lista;

            List<ResponseDataModel> variables = objResp.Variables
                .Where(p => EsVariableEnvioFlujo(p.NoVariable))
                .ToList();

            AgregarEncabezadosTemplate(lista, variables, etapasSeleccionadas);

            foreach (ResponseDataModel variable in variables)
            {
                if (variable.Etapas == null)
                    continue;
                
                foreach (EtapaResModel etapa in variable.Etapas.Where(r => etapasSeleccionadas == null || etapasSeleccionadas.Contains(r.Clave)))
                {
                    decimal valor = (decimal)etapa.Valor;

                    decimal valorFormateado = variable.Posicion is 28 or 43
                        ? Math.Round(valor / 1000m, 6, MidpointRounding.AwayFromZero)
                        : Math.Round(valor, 2, MidpointRounding.AwayFromZero);

                    lista.Add(new TemplateSP
                    {
                        TipoRegistro = 3,
                        CveEtapa = etapa.Clave,
                        Posicion = variable.Posicion,
                        CodigoEtapa = GetCodigoFormulaArchivo(etapa.Clave),
                        Valor1 = valorFormateado,
                        UsaValor1 = true,
                        Valor2 = 0m,
                        UsaValor2 = false,
                        Descripcion = GetNomFormat(variable.NoVariable.ToString(CultureInfo.InvariantCulture))
                    });
                }
            }

            return lista
                .OrderBy(r => r.CveEtapa)
                .ThenBy(r => r.Posicion)
                .ToList();
        }

        private void AgregarEncabezadosTemplate(List<TemplateSP> lista, List<ResponseDataModel> variables, HashSet<int>? etapasSeleccionadas)
        {
            if (!GetConfigBool("FormatTemplate:IncluirEncabezados", true))
                return;

            HashSet<int> etapas = etapasSeleccionadas != null && etapasSeleccionadas.Count > 0
                ? new HashSet<int>(etapasSeleccionadas)
                : variables
                    .Where(p => p.Etapas != null)
                    .SelectMany(p => p.Etapas!)
                    .Select(p => p.Clave)
                    .ToHashSet();

            string fecha = GetConfigValue("FormatTemplate:FechaEncabezado", DateTime.Now.ToString("dd/MM/yy", CultureInfo.InvariantCulture));

            foreach (int cveEtapa in etapas.OrderBy(p => p))
            {
                lista.Add(new TemplateSP
                {
                    TipoRegistro = 1,
                    CveEtapa = cveEtapa,
                    Posicion = int.MinValue,
                    CodigoEtapa = GetCodigoFormulaArchivo(cveEtapa),
                    Valor1 = 0m,
                    UsaValor1 = true,
                    Valor2 = 0m,
                    UsaValor2 = false,
                    Descripcion = GetNombreFormulaArchivo(cveEtapa),
                    Fecha = fecha
                });
            }
        }

        private Dictionary<int, Dictionary<string, decimal>> GetValoresAllixPorEtapa(DataTable dt, HashSet<int> etapasSeleccionadas)
        {
            var result = etapasSeleccionadas.ToDictionary(
                etapa => etapa,
                _ => new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            );

            ResponseModel? objResp = GetResponsePerfil(dt);
            if (objResp?.Variables == null)
                return result;

            foreach (ResponseDataModel variable in objResp.Variables.Where(p => EsVariableEnvioFlujo(p.NoVariable)))
            {
                string codFormat = GetNomFormat(variable.NoVariable.ToString(CultureInfo.InvariantCulture)).Trim();
                if (string.IsNullOrWhiteSpace(codFormat) || variable.Etapas == null)
                    continue;

                foreach (EtapaResModel etapa in variable.Etapas.Where(p => etapasSeleccionadas.Contains(p.Clave)))
                {
                    if (!result.ContainsKey(etapa.Clave))
                        result[etapa.Clave] = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                    result[etapa.Clave][codFormat] = (decimal)etapa.Valor;
                }
            }

            return result;
        }

        private ResponseModel? GetResponsePerfil(DataTable dt)
        {
            if (dt == null || dt.Rows.Count == 0)
                return null;

            string response = GetString(dt.Rows[0], "Response2");
            if (string.IsNullOrWhiteSpace(response))
                response = GetString(dt.Rows[0], "Response");

            if (string.IsNullOrWhiteSpace(response))
                return null;

            return JsonConvert.DeserializeObject<ResponseModel>(response);
        }

        private List<AllixExpFileResponse> GenerarArchivosAllixExp(long cvePerfilN, HashSet<int> etapasSeleccionadas, Dictionary<int, Dictionary<string, decimal>> valoresPorEtapa)
        {
            string templatePath = GetFullPath(GetConfigValue("AllixExp:TemplatePath", "p.exp"));
            if (!System.IO.File.Exists(templatePath))
                throw new Exception($"No existe la plantilla Allix EXP: {templatePath}");

            string outputFolder = GetFullPath(GetConfigValue("AllixExp:OutputFolder", @"Archivos\Allix"));
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var archivos = new List<AllixExpFileResponse>();
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            foreach (int cveEtapa in etapasSeleccionadas.OrderBy(p => p))
            {
                if (!dictFormulas.TryGetValue(cveEtapa, out FormulaEtapaInfo? formula))
                    throw new Exception($"No existe formula registrada para la etapa {cveEtapa}.");

                List<List<string>> rows = LeerPlantillaAllixExp(templatePath);
                Dictionary<string, decimal> valores = valoresPorEtapa.TryGetValue(cveEtapa, out Dictionary<string, decimal>? etapaValores)
                    ? etapaValores
                    : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                string codigoFormula = GetCodigoFormulaArchivo(cveEtapa);
                ActualizarEncabezadoAllixExp(rows, codigoFormula, GetNombreFormulaAllix(formula));
                AllixExpApplyResult applyResult = AplicarValoresAllixExp(rows, valores);

                string fileName = $"{cvePerfilN}_{SanitizeFileName(codigoFormula)}_{timestamp}_ALLIX.EXP";
                string fullPath = Path.Combine(outputFolder, fileName);
                EscribirAllixExp(fullPath, rows);

                archivos.Add(new AllixExpFileResponse
                {
                    CveEtapa = cveEtapa,
                    CodigoFormula = codigoFormula,
                    NombreFormula = GetNombreFormulaAllix(formula),
                    Archivo = fileName,
                    Ruta = fullPath,
                    VariablesSolicitadas = valores.Count,
                    VariablesAplicadas = applyResult.Aplicadas,
                    VariablesNoEncontradas = applyResult.NoEncontradas
                });
            }

            return archivos;
        }

        private static List<List<string>> LeerPlantillaAllixExp(string templatePath)
        {
            return System.IO.File.ReadAllLines(templatePath, new UTF8Encoding(false))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(ParseCsvLine)
                .ToList();
        }

        private static List<string> ParseCsvLine(string line)
        {
            using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(line));
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;

            return parser.ReadFields()?.ToList() ?? new List<string>();
        }

        private static void ActualizarEncabezadoAllixExp(List<List<string>> rows, string codigoFormula, string nombreFormula)
        {
            DateTime now = DateTime.Now;
            SetSingleFieldRow(rows, 4, now.ToString("dd/MM/yy", CultureInfo.InvariantCulture));
            SetSingleFieldRow(rows, 5, now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            SetSingleFieldRow(rows, 8, codigoFormula);
            SetSingleFieldRow(rows, 9, nombreFormula);
            SetSingleFieldRow(rows, 21, now.ToString("dd/MM/yy", CultureInfo.InvariantCulture));
        }

        private static void SetSingleFieldRow(List<List<string>> rows, int rowIndex, string value)
        {
            if (rowIndex < 0 || rowIndex >= rows.Count)
                return;

            if (rows[rowIndex].Count == 0)
                rows[rowIndex].Add(value);
            else
                rows[rowIndex][0] = value;

            while (rows[rowIndex].Count < 2)
                rows[rowIndex].Add("");
        }

        private static AllixExpApplyResult AplicarValoresAllixExp(List<List<string>> rows, Dictionary<string, decimal> valores)
        {
            int bodyStart = 28;
            int bodyCount = GetAllixBodyCount(rows);
            int bodyEnd = Math.Min(rows.Count, bodyStart + bodyCount);
            var pendientes = new HashSet<string>(valores.Keys, StringComparer.OrdinalIgnoreCase);
            int aplicadas = 0;

            for (int i = bodyStart; i < bodyEnd; i++)
            {
                List<string> row = rows[i];
                if (row.Count < 6)
                    continue;

                string codFormat = row[5].Trim();
                if (valores.TryGetValue(codFormat, out decimal valor))
                {
                    AplicarValorAllix(row, valor);
                    pendientes.Remove(codFormat);
                    aplicadas++;
                    continue;
                }

                if (!DebeConservarValorPlantilla(codFormat))
                    LimpiarVariableAllix(row);
            }

            return new AllixExpApplyResult
            {
                Aplicadas = aplicadas,
                NoEncontradas = pendientes.OrderBy(p => p).ToList()
            };
        }

        private static int GetAllixBodyCount(List<List<string>> rows)
        {
            if (rows.Count <= 27 || rows[27].Count == 0 || !int.TryParse(rows[27][0], out int bodyCount))
                throw new Exception("La plantilla Allix EXP no contiene el conteo de variables esperado en la linea 28.");

            return bodyCount;
        }

        private static bool DebeConservarValorPlantilla(string codFormat)
        {
            return codFormat.Equals("[VOLUME]", StringComparison.OrdinalIgnoreCase);
        }

        private static void LimpiarVariableAllix(List<string> row)
        {
            EnsureAllixVariableFields(row);
            row[0] = "1";
            row[1] = "0.0";
            row[2] = "1";
            row[3] = "0.0";
            row[4] = "0";
        }

        private static void AplicarValorAllix(List<string> row, decimal valor)
        {
            EnsureAllixVariableFields(row);
            string valorFormat = FormatAllixDecimal(valor);
            row[0] = "0";
            row[1] = valorFormat;

            if (row[2].Equals("0", StringComparison.OrdinalIgnoreCase))
                row[3] = valorFormat;

            row[4] = "1";
        }

        private static void EnsureAllixVariableFields(List<string> row)
        {
            while (row.Count < 7)
                row.Add("");
        }

        private static void EscribirAllixExp(string fullPath, List<List<string>> rows)
        {
            string content = string.Join(Environment.NewLine, rows.Select(ToCsvLine)) + Environment.NewLine;
            System.IO.File.WriteAllText(fullPath, content, new UTF8Encoding(false));
        }

        private static string ToCsvLine(List<string> fields)
        {
            return string.Join(",", fields.Select(FormatCsvField));
        }

        private static string FormatCsvField(string value)
        {
            value ??= "";
            if (string.IsNullOrEmpty(value))
                return "";

            if (IsCsvNumeric(value))
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static bool IsCsvNumeric(string value)
        {
            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private static string FormatAllixDecimal(decimal value)
        {
            return value.ToString("0.##########", CultureInfo.InvariantCulture);
        }

        private static string GetNombreFormulaAllix(FormulaEtapaInfo formula)
        {
            if (!string.IsNullOrWhiteSpace(formula.NomEtapa))
                return formula.NomEtapa.Trim();

            return "ETAPA " + formula.CveEtapa.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetFullPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            return Path.Combine(Directory.GetCurrentDirectory(), path);
        }

        private static string SanitizeFileName(string value)
        {
            string result = value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');

            return string.IsNullOrWhiteSpace(result) ? "SIN_CODIGO" : result;
        }

        private string GetCodigoFormulaArchivo(int cveEtapa)
        {
            if (dictFormulas.TryGetValue(cveEtapa, out FormulaEtapaInfo? formula))
            {
                if (!string.IsNullOrWhiteSpace(formula.CodFormulaCarga))
                    return formula.CodFormulaCarga.Trim();

                if (!string.IsNullOrWhiteSpace(formula.CodFormula))
                    return formula.CodFormula.Trim();
            }

            string claveEtapa = cveEtapa.ToString(CultureInfo.InvariantCulture);
            if (dictEtapas.TryGetValue(claveEtapa, out string? codigoFormat) && !string.IsNullOrWhiteSpace(codigoFormat))
                return codigoFormat.Trim();

            throw new Exception($"No existe CodFormula/CodigoFormat para la etapa {cveEtapa}.");
        }

        private string GetNombreFormulaArchivo(int cveEtapa)
        {
            if (dictFormulas.TryGetValue(cveEtapa, out FormulaEtapaInfo? formula))
                return GetNombreFormulaAllix(formula);

            return "ETAPA " + cveEtapa.ToString(CultureInfo.InvariantCulture);
        }

        private string GetNomFormat(string cveVariable)
        {
            if (dictVar.TryGetValue(cveVariable, out Dictionary<string, object?>? variable) &&
                variable.TryGetValue("CodFormat", out object? nomFormat) &&
                nomFormat != null)
                return nomFormat.ToString() ?? "";

            return "";
        }

        private int GetTipoRegistroFormat(int cveVariable)
        {
            string claveVariable = cveVariable.ToString(CultureInfo.InvariantCulture);
            if (!dictVar.TryGetValue(claveVariable, out Dictionary<string, object?>? variable))
                return 3;

            foreach (string columnName in new[]
            {
                "TipoRegistro",
                "CveTipoRegistro",
                "TipoRenglon",
                "CveTipoRenglon",
                "TipoFila",
                "CveTipoFila",
                "TipoFormat",
                "CveTipoFormat"
            })
            {
                if (!variable.TryGetValue(columnName, out object? value) || value == null)
                    continue;

                if (int.TryParse(value.ToString(), out int tipoRegistro) && tipoRegistro > 0)
                    return tipoRegistro;
            }

            return 3;
        }

        private bool EsVariableEnvioFlujo(int cveVariable)
        {
            string claveVariable = cveVariable.ToString(CultureInfo.InvariantCulture);
            if (!dictVar.TryGetValue(claveVariable, out Dictionary<string, object?>? variable))
                return false;

            if (!variable.TryGetValue("EnvioFlujo", out object? envioFlujo) || envioFlujo == null)
                return false;

            string valor = envioFlujo.ToString()?.Trim() ?? "";
            return valor.Equals("S", StringComparison.OrdinalIgnoreCase) ||
                   valor.Equals("SI", StringComparison.OrdinalIgnoreCase) ||
                   valor.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                   valor.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
                   valor.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                   valor.Equals("1", StringComparison.OrdinalIgnoreCase);
        }

        private static string GenerarLinea(TemplateSP item)
        {
            if (item.TipoRegistro == 1)
            {
                string fecha = string.IsNullOrWhiteSpace(item.Fecha)
                    ? DateTime.Now.ToString("dd/MM/yy", CultureInfo.InvariantCulture)
                    : item.Fecha;

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "1,\"{0}\",\"{1}\",\"{2}\",{3},",
                    EscapeCsvValue(item.CodigoEtapa),
                    EscapeCsvValue(item.Descripcion),
                    EscapeCsvValue(fecha),
                    FormatAllixDecimal(item.Valor1)
                );
            }

            int flag1 = item.UsaValor1 ? 0 : 1;
            decimal valor1 = item.UsaValor1 ? item.Valor1 : 0.0m;

            int flag2 = item.UsaValor2 ? 0 : 1;
            decimal valor2 = item.UsaValor2 ? item.Valor2 : 0.0m;

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},\"{1}\",{2},{3},{4},{5},\"{6}\"",
                item.TipoRegistro,
                EscapeCsvValue(item.CodigoEtapa),
                flag1,
                FormatAllixDecimal(valor1),
                flag2,
                FormatAllixDecimal(valor2),
                EscapeCsvValue(item.Descripcion)
            );
        }

        private static string EscapeCsvValue(string? value)
        {
            return (value ?? "").Replace("\"", "\"\"");
        }

        private async Task EnviarCorreoFormat(TemplateRequestModel objReq, string fullPath)
        {
            List<string> destinatarios = GetDestinatariosCorreoFormat();
            string subject = GetConfigValue("FormatEmail:Asunto", $"Format Optimizer Cerdos - Perfil {objReq.CvePerfilN}");

            string templatePath = GetConfigValue("FormatEmail:TemplatePath", @"Templates\Email\format_optimizer.html");
            string body = emailTemplateRenderer.RenderFromFile(templatePath, new Dictionary<string, string>
            {
                { "CvePerfilN", objReq.CvePerfilN.ToString(CultureInfo.InvariantCulture) },
                { "NombreArchivo", Path.GetFileName(fullPath) },
                { "RutaArchivo", fullPath },
                { "FechaGeneracion", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) }
            });

            await emailService.SendAsync(new EmailMessage
            {
                To = destinatarios,
                Subject = subject,
                HtmlBody = body,
                AttachmentPaths = new List<string> { fullPath }
            });
        }

        private static List<string> GetDestinatariosCorreoFormat()
        {
            var destinatarios = AppSetConfig.AppSetting
                .GetSection("FormatEmail:Destinatarios")
                .GetChildren()
                .Select(p => p.Value)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim())
                .ToList();

            if (destinatarios.Count == 0)
            {
                string destinatariosTexto = AppSetConfig.AppSetting["FormatEmail:Destinatarios"] ?? "";
                destinatarios = destinatariosTexto
                    .Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
            }

            if (destinatarios.Count == 0)
                throw new Exception("No hay destinatarios configurados en appsettings.json: FormatEmail:Destinatarios.");

            return destinatarios;
        }

        private static string GetConfigValue(string key, string defaultValue)
        {
            string value = AppSetConfig.AppSetting[key] ?? "";
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static bool GetConfigBool(string key, bool defaultValue)
        {
            string value = AppSetConfig.AppSetting[key] ?? "";
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (bool.TryParse(value, out bool parsedBool))
                return parsedBool;

            if (int.TryParse(value, out int parsedInt))
                return parsedInt != 0;

            return value.Equals("S", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("SI", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("YES", StringComparison.OrdinalIgnoreCase);
        }

        private void ActualizarEstatusFormulas(long cvePerfilN, IEnumerable<int> etapas, int cveEstatus, string usuAct, Guid idOperacion, string tipoMovimiento, string nota)
        {
            HashSet<string> formulaColumns = LoadTableColumns("OptimizerC_PerfilN_Formulas");
            HashSet<string> logColumns = LoadTableColumns("OptimizerC_PerfilN_Formulas_Log");

            foreach (int cveEtapa in etapas.Distinct().OrderBy(p => p))
            {
                if (formulaColumns.Contains("CveEstatus"))
                {
                    string update = "UPDATE OptimizerC_PerfilN_Formulas SET " +
                                    $"CveEstatus = {cveEstatus}, FecAct = GETDATE(), UsuAct = {SqlNullableLong(ParseUsuAct(usuAct))} " +
                                    $"WHERE CvePerfilN = {cvePerfilN} AND CveEtapa = {cveEtapa}";
                    Database.execNonQuery(update);
                }

                FormulaEtapaInfo formula = dictFormulas.TryGetValue(cveEtapa, out FormulaEtapaInfo? actual)
                    ? actual
                    : new FormulaEtapaInfo { CvePerfilN = cvePerfilN, CveEtapa = cveEtapa };

                formula.CveEstatus = cveEstatus;
                formula.Nota = nota;
                formula.UsuAct = usuAct;
                InsertFormulaLog(formula, logColumns, tipoMovimiento, idOperacion);
            }
        }

        private void RegistrarEventoFormulas(long cvePerfilN, IEnumerable<int> etapas, string usuAct, Guid idOperacion, string tipoMovimiento, string nota)
        {
            HashSet<string> logColumns = LoadTableColumns("OptimizerC_PerfilN_Formulas_Log");

            foreach (int cveEtapa in etapas.Distinct().OrderBy(p => p))
            {
                FormulaEtapaInfo formula = dictFormulas.TryGetValue(cveEtapa, out FormulaEtapaInfo? actual)
                    ? actual
                    : new FormulaEtapaInfo { CvePerfilN = cvePerfilN, CveEtapa = cveEtapa };

                formula.Nota = nota;
                formula.UsuAct = usuAct;
                InsertFormulaLog(formula, logColumns, tipoMovimiento, idOperacion);
            }
        }

        private IEnumerable<int> GetEtapasParaEstatus(HashSet<int>? etapasSeleccionadas)
        {
            if (etapasSeleccionadas != null && etapasSeleccionadas.Count > 0)
                return etapasSeleccionadas;

            return dictFormulas.Keys;
        }

        private static HashSet<int>? ParseEtapas(List<TemplateEtapaRequestModel>? etapas)
        {
            List<int> keys = GetRequestedStageKeys(etapas);
            return keys.Count == 0 ? null : new HashSet<int>(keys);
        }

        private static List<int> GetRequestedStageKeys(List<TemplateEtapaRequestModel>? etapas)
        {
            if (etapas == null || etapas.Count == 0)
                return new List<int>();

            var result = new List<int>();
            foreach (TemplateEtapaRequestModel etapaReq in etapas)
            {
                int cveEtapa = GetRequestCveEtapa(etapaReq);
                if (cveEtapa <= 0)
                    throw new Exception($"El valor de etapa '{cveEtapa}' no es vÃ¡lido.");

                if (!result.Contains(cveEtapa))
                    result.Add(cveEtapa);
            }

            return result;
        }

        private static int GetRequestCveEtapa(TemplateEtapaRequestModel etapaReq)
        {
            return etapaReq.CveEtapa;
        }

        private static HashSet<string> LoadTableColumns(string tableName)
        {
            DataTable dtColumns = Database.execQuery($"SELECT TOP 0 * FROM {tableName}");
            return dtColumns.Columns
                .Cast<DataColumn>()
                .Select(p => p.ColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static void AddOptionalField(List<string> fields, List<string> values, HashSet<string> columns, string columnName, string value)
        {
            if (!columns.Contains(columnName))
                return;

            fields.Add(columnName);
            values.Add(value);
        }

        private static FormulaResponse ToFormulaResponse(FormulaEtapaInfo formula)
        {
            return new FormulaResponse
            {
                CveEtapa = formula.CveEtapa,
                CveEtapaFlujo = formula.CveEtapaFlujo,
                NomEtapa = formula.NomEtapa,
                CveAccion = formula.CveAccion,
                CveEstatus = formula.CveEstatus,
                Nota = formula.Nota,
                CodFormula = formula.CodFormula,
                CodFormulaCarga = formula.CodFormulaCarga
            };
        }

        private static string GetString(DataRow row, string columnName)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return "";

            return row[columnName]?.ToString() ?? "";
        }

        private static int? GetNullableInt(DataRow row, string columnName)
        {
            string value = GetString(row, columnName);
            if (int.TryParse(value, out int result))
                return result;

            return null;
        }

        private static long? ParseUsuAct(string usuAct)
        {
            string onlyDigits = new string((usuAct ?? "").Where(char.IsDigit).ToArray());
            if (long.TryParse(onlyDigits, out long value))
                return value;

            return null;
        }

        private static string SqlNullableLong(long? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
        }

        private static string SqlNullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
        }

        private static string SqlNullableLiteral(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NULL";

            return "'" + value.Trim().Replace("'", "''") + "'";
        }

        private class FormulaEtapaInfo
        {
            public long CvePerfilN { get; set; }
            public int CveEtapa { get; set; }
            public int? CveEtapaFlujo { get; set; }
            public string NomEtapa { get; set; } = "";
            public int? CveAccion { get; set; }
            public string Nota { get; set; } = "";
            public string CodFormula { get; set; } = "";
            public string CodFormulaCarga { get; set; } = "";
            public int? CveEstatus { get; set; }
            public string UsuAct { get; set; } = "";
        }

        private class FormulaOperationResult
        {
            public bool Ok { get; set; }
            public long CvePerfilN { get; set; }
            public Guid IdOperacion { get; set; }
            public List<FormulaResponse> Formulas { get; set; } = new List<FormulaResponse>();
        }

        private class FormulaResponse
        {
            public int CveEtapa { get; set; }
            public int? CveEtapaFlujo { get; set; }
            public string NomEtapa { get; set; } = "";
            public int? CveAccion { get; set; }
            public int? CveEstatus { get; set; }
            public string Nota { get; set; } = "";
            public string CodFormula { get; set; } = "";
            public string CodFormulaCarga { get; set; } = "";
        }

        private class AllixExpFileResponse
        {
            public int CveEtapa { get; set; }
            public string CodigoFormula { get; set; } = "";
            public string NombreFormula { get; set; } = "";
            public string Archivo { get; set; } = "";
            public string Ruta { get; set; } = "";
            public int VariablesSolicitadas { get; set; }
            public int VariablesAplicadas { get; set; }
            public List<string> VariablesNoEncontradas { get; set; } = new List<string>();
        }

        private class AllixExpApplyResult
        {
            public int Aplicadas { get; set; }
            public List<string> NoEncontradas { get; set; } = new List<string>();
        }
    }
}
