using Newtonsoft.Json;
using WSOptimizer7.Models;

namespace WSOptimizer7.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.VisualBasic;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using WSOptimizer7.App_Data;
    using WSOptimizer7.Config;
    using WSOptimizer7.Models;

    //[Route("[controller]")]

    public class FormatController : Controller
    {

        public DataTable dtVar;
        public DataTable dtEtapa;
        Dictionary<string, Dictionary<string, object>> dictVar;
        Dictionary<string, string> dictEtapas;

        [HttpPost]
        [Route("api/template")]
        public IActionResult GetOptimizerModelN([FromBody] TemplateRequestModel objReq)
        {
            try
            {
                if (objReq == null)
                    return BadRequest("El cuerpo de la solicitud no es válido.");
                if (objReq.CvePerfilN <= 0)
                    return BadRequest("El parámetro CvePerfilN no es válido.");

                HashSet<int>? etapasSeleccionadas = ParseEtapas(objReq.Etapas);


                // 1. Consulta SQL
                string strSQLRef = $"SELECT * FROM OptimizerC_PerfilN_Resultado WHERE CvePerfilN = {objReq.CvePerfilN}";
                DataTable dtRef = Database.execQuery(strSQLRef);

                if (dtRef == null || dtRef.Rows.Count == 0)
                    return BadRequest("No se encontraron datos");

                string strSQLVar = $"SELECT * FROM CatOptimizerC_Variables";
                dtVar = Database.execQuery(strSQLVar);
                
                if (dtVar.Rows.Count > 0) { 
                dictVar = dtVar.AsEnumerable()
    .ToDictionary(
           r => r["CveVariable"]?.ToString() ?? "",
        r => dtVar.Columns.Cast<DataColumn>()
                .ToDictionary(
                    c => c.ColumnName,
                   c => r[c] is DBNull ? null : r[c]
                )
    );
                }


                string strSQLEtapas = $"SELECT * FROM CatOptimizerC_Etapas";
                dtEtapa = Database.execQuery(strSQLEtapas);
                dictEtapas= dtEtapa.AsEnumerable()
        .ToDictionary(
               r => r["CveEtapa"]?.ToString() ?? "",
            r => r.Field<string>("CodigoFormat")
        );

                ApplyCodigosNuevos(objReq.Etapas);





                // 2. Convertir DataTable → List<TemplateSP>
                var lista = ConvertirDataTable(dtRef, etapasSeleccionadas);

                // 3. Generar líneas del archivo
                var lineas = lista.Select(x => GenerarLinea(x)).ToList();

                // 4. Generar ruta del archivo
                string basePath = Path.Combine(Directory.GetCurrentDirectory(), "Archivos");
                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string fileName = $"{objReq.CvePerfilN}_{timestamp}.EXP";
                string fullPath = Path.Combine(basePath, fileName);

                // 5. Guardar archivo .EXP
                using (var writer = new StreamWriter(fullPath, false, Encoding.UTF8))
                {
                    foreach (var linea in lineas)
                        writer.WriteLine(linea);
                }

                // 6. Respuesta
                return Ok(new
                {
                    mensaje = "Archivo generado correctamente.",
                    archivo = fileName,
                    ruta = fullPath,
                    etapas = objReq.Etapas
                });
            }
            catch (Exception ex)
            {
                return BadRequest("Error procesando la solicitud: " + ex.ToString());
            }
        }

        public string GetNomFormat(DataTable dtVar, string cveVariable)
        {
            return dtVar.AsEnumerable()
                        .Where(r => r.Field<string>("CveVariable") == cveVariable)
                        .Select(r => r.Field<string>("NomFormat"))
                        .FirstOrDefault();
        }


        public string GenerarLinea(TemplateSP item)
        {
            int flag1 = item.UsaValor1 ? 0 : 1;
            decimal valor1 = item.UsaValor1 ? item.Valor1 : 0.0m;

            int flag2 = item.UsaValor2 ? 0 : 1;
            decimal valor2 = item.UsaValor2 ? item.Valor2 : 0.0m;

            return string.Format(
                "3,\"{0}\",{1},{2}, {3},{4},\"{5}\",",
                item.CodigoEtapa,
                flag1,
                valor1.ToString("0.0", CultureInfo.InvariantCulture),
                flag2,
                valor2.ToString("0.0", CultureInfo.InvariantCulture),
                item.Descripcion
            );
        }

        public List<TemplateSP> ConvertirDataTable(DataTable dt, HashSet<int>? etapasSeleccionadas = null)
        {
            var lista = new List<TemplateSP>();

            if (dt == null || dt.Rows.Count == 0)
                return lista;

            if (dt.Rows.Count > 0)
            {

                String response = dt.Rows[0]["Response"]?.ToString();
                ResponseModel objResp = JsonConvert.DeserializeObject<ResponseModel>(response);
                //variables que aplican=> 
                List<ResponseDataModel> variables = objResp.Variables.FindAll(p => p.MostrarCliente.Equals("S") && p.NoVariable > 2);

                variables.ForEach(p =>
                {
                    p.Etapas
                    .Where(r => etapasSeleccionadas == null || etapasSeleccionadas.Contains(r.Clave))
                    .ToList()
                    .ForEach(r =>
                {

                    var obj = new TemplateSP
                    {
                        Posicion = p.Posicion,
                        CodigoEtapa = dictEtapas[r.Clave.ToString()].ToString(),
                        Valor1 = (decimal)r.Valor,
                        UsaValor1 = true,
                        Valor2 = 0m,
                        UsaValor2 = false,

                        Descripcion = dictVar[p.NoVariable.ToString()]["NomFormat"].ToString()
                    };
                    //row["Valor2"] != DBNull.Value ? Convert.ToDecimal(row["Valor2"]) : 0m,
                    //row["UsaValor2"] != DBNull.Value && Convert.ToInt32(row["UsaValor2"]) == 1,

                    lista.Add(obj);

                }

                );
                });


            }
            var listaOrdenada = lista
    .OrderBy(r => r.CodigoEtapa)
    .ThenBy(r => r.Posicion)
    .ToList();


            return listaOrdenada;
        }

        private static HashSet<int>? ParseEtapas(List<TemplateEtapaRequestModel>? etapas)
        {
            if (etapas == null || etapas.Count == 0)
                return null;

            var etapasSeleccionadas = new HashSet<int>();
            foreach (TemplateEtapaRequestModel etapaReq in etapas)
            {
                if (etapaReq.Clave <= 0)
                    throw new Exception($"El valor de etapa '{etapaReq.Clave}' no es válido.");

                etapasSeleccionadas.Add(etapaReq.Clave);
            }

            return etapasSeleccionadas;
        }

        private void ApplyCodigosNuevos(List<TemplateEtapaRequestModel>? etapas)
        {
            if (etapas == null || etapas.Count == 0)
                return;

            foreach (TemplateEtapaRequestModel etapa in etapas
                .Where(p => p.IsCodigoNuevo)
                .GroupBy(p => p.Clave)
                .Select(g => g.First()))
            {
                string claveEtapa = etapa.Clave.ToString();
                if (!dictEtapas.ContainsKey(claveEtapa))
                    throw new Exception($"No existe CodigoFormat para la etapa {etapa.Clave}.");

                dictEtapas[claveEtapa] = GetAndUpdateCodigoNuevo();
            }
        }

        private string GetAndUpdateCodigoNuevo()
        {
            string strSQL = "SELECT * FROM Config_Parametros WHERE CvePlataforma = 3 AND CveParametro = 1";
            DataTable dtParametro = Database.execQuery(strSQL);
            if (dtParametro == null || dtParametro.Rows.Count == 0)
                throw new Exception("No se encontró la configuración de código nuevo en Config_Parametros.");

            string valorActualTexto = dtParametro.Rows[0]["Valor"]?.ToString()?.Trim() ?? "";
            if (!long.TryParse(valorActualTexto, out long valorActual))
                throw new Exception("El valor configurado en Config_Parametros no es numérico.");

            long nuevoValor = valorActual + 1;
            string updateSQL = $"UPDATE Config_Parametros SET Valor = '{nuevoValor}' WHERE CvePlataforma = 3 AND CveParametro = 1";
            Database.execNonQuery(updateSQL);

            return nuevoValor.ToString();
        }

    }
}
