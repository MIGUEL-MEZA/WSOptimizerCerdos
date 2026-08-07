using System.Globalization;
using System.Net;
using System.Text;
using WSOptimizer7.Models;

namespace WSOptimizer7.Services
{
    public class FormulaReportRenderer : IFormulaReportRenderer
    {
        public string RenderBody(FormulaReporteDetalle reporte, FormulaCargaEtapa formula)
        {
            var html = new StringBuilder();
            html.Append("<section class='formula-report'>");
            html.Append("<style>");
            html.Append("body{font-family:Arial,Helvetica,sans-serif;color:#1f2937;font-size:12px}.formula-report{padding:8px 12px}");
            html.Append("h1{font-size:18px;color:#173d70;margin:0 0 8px}h2{font-size:14px;color:#173d70;margin:20px 0 8px}");
            html.Append(".meta{width:100%;border-collapse:collapse;margin-bottom:12px}.meta td{padding:3px 8px;border:0}");
            html.Append("table.data{width:100%;border-collapse:collapse}.data th{background:#eef3f8;color:#173d70;font-weight:bold}.data th,.data td{border:1px solid #d8e0ea;padding:5px 7px;text-align:left}.num{text-align:right!important}");
            html.Append("</style>");
            html.Append($"<h1>{E(formula.CodFormulaCarga)} - {E(formula.Nombre)}</h1>");
            html.Append("<table class='meta'>");
            AddMeta(html, "Cliente", $"{reporte.CodCliente} {reporte.Cliente}".Trim(), "Perfil", reporte.IdPerfil.ToString(CultureInfo.InvariantCulture));
            AddMeta(html, "Folio", reporte.Folio, "Titulo", reporte.Titulo);
            AddMeta(html, "Cantidad", F(formula.DatosGenerales.Cantidad), "Fecha", formula.DatosGenerales.Fecha);
            AddMeta(html, "Costo", F(formula.DatosGenerales.Costo), "Etapa", formula.Nombre);
            html.Append("</table>");

            html.Append("<h2>Materias primas</h2><table class='data'><thead><tr>");
            foreach (string encabezado in new[] { "RM code", "Descripcion", "%", "kgs"})
                html.Append($"<th>{E(encabezado)}</th>");
            html.Append("</tr></thead><tbody>");
            foreach (FormulaMateriaPrima materia in formula.MateriasPrimas.OrderBy(p => p.Orden))
            {
                html.Append("<tr>");
                html.Append($"<td>{E(materia.RmCode)}</td><td>{E(materia.Descripcion)}</td><td class='num'>{F(materia.Porcentaje)}</td><td class='num'>{F(materia.Kilogramos)}</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");

            html.Append("<h2>Nutrient analysis</h2><table class='data'><thead><tr>");
            foreach (string encabezado in new[] { "Description",  "Actual" })
                html.Append($"<th>{E(encabezado)}</th>");
            html.Append("</tr></thead><tbody>");
            foreach (FormulaNutriente nutriente in formula.Nutrientes.OrderBy(p => p.Orden))
            {
                html.Append($"<tr><td>{E(nutriente.Descripcion)}</td><td class='num'>{F(nutriente.Actual)}</td></tr>");
            }
            html.Append("</tbody></table></section>");
            return html.ToString();
        }

        private static void AddMeta(StringBuilder html, string label1, string value1, string label2, string value2)
        {
            html.Append($"<tr><td><strong>{E(label1)}:</strong> {E(value1)}</td><td><strong>{E(label2)}:</strong> {E(value2)}</td></tr>");
        }

        private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
        private static string F(decimal? value) => value?.ToString("0.######", CultureInfo.InvariantCulture) ?? "";
    }
}
