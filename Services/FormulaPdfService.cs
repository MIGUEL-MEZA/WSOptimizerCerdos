using System.Drawing;
using System.Net;
using System.Text;
using ExpertPdf.HtmlToPdf;
using WSOptimizer7.Models;

namespace WSOptimizer7.Services
{
    public class FormulaPdfService : IFormulaPdfService
    {
        private readonly IConfiguration configuration;
        private readonly IFormulaReportRenderer renderer;

        public FormulaPdfService(IConfiguration configuration, IFormulaReportRenderer renderer)
        {
            this.configuration = configuration;
            this.renderer = renderer;
        }

        public byte[] Generate(FormulaReporteDetalle reporte, IEnumerable<FormulaCargaEtapa> formulas)
        {
            List<FormulaCargaEtapa> lista = formulas.ToList();
            if (lista.Count == 0)
                throw new FormulaBusinessException(200, "No existen formulas para generar el PDF.");

            string licenseKey = configuration["ExpertPdf:LicenseKey"]
                ?? throw new InvalidOperationException("No se encontro la licencia de ExpertPdf en la configuracion.");

            var pdf = new PdfConverter { LicenseKey = licenseKey };
            pdf.PdfDocumentOptions.EmbedFonts = true;
            pdf.PdfDocumentOptions.GenerateSelectablePdf = true;
            pdf.PdfDocumentOptions.PdfPageSize = PdfPageSize.Letter;
            pdf.PdfDocumentOptions.PdfPageOrientation = PDFPageOrientation.Landscape;
            pdf.PdfDocumentOptions.FitWidth = true;
            pdf.PdfDocumentOptions.FitHeight = false;
            pdf.PdfDocumentOptions.TopMargin = 5;
            pdf.PdfDocumentOptions.BottomMargin = 5;
            pdf.PdfDocumentOptions.LeftMargin = 10;
            pdf.PdfDocumentOptions.RightMargin = 10;
            pdf.PdfDocumentOptions.ShowHeader = true;
            pdf.PdfDocumentOptions.ShowFooter = true;

            pdf.PdfHeaderOptions.DrawHeaderLine = false;
            pdf.PdfHeaderOptions.HtmlToPdfArea = new HtmlToPdfArea(
                BuildHeaderHtml(reporte),
                GetTemplatePath("perfil_nutricional_header.html"));
            pdf.PdfHeaderOptions.HeaderHeight = 70;

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

            var html = new StringBuilder("<!doctype html><html><head><meta charset='utf-8'></head><body>");
            for (int i = 0; i < lista.Count; i++)
            {
                if (i > 0)
                    html.Append("<div style='page-break-before:always'></div>");
                html.Append(renderer.RenderBody(reporte, lista[i]));
            }
            html.Append("</body></html>");
            return pdf.GetPdfBytesFromHtmlString(html.ToString());
        }

        private static string BuildHeaderHtml(FormulaReporteDetalle reporte)
        {
            return "<!doctype html><html><body style='font-family:Arial;margin:0'>" +
                   "<table style='width:950px'><tr>" +
                   "<td style='width:35%'><img src='../../Diseno/Logo_Nuptimizer.svg' width='220'></td>" +
                   $"<td style='text-align:right;color:#30426d'><strong>FORMULAS</strong><br>{WebUtility.HtmlEncode(reporte.CodCliente)} - {WebUtility.HtmlEncode(reporte.Cliente)}<br>Perfil {reporte.IdPerfil}</td>" +
                   "</tr></table></body></html>";
        }

        private static string BuildFooterHtml()
        {
            string path = GetTemplatePath("perfil_nutricional_footer.html");
            string template = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "<html><body></body></html>";
            return template.Replace("@@PieTexto", "Nuptimizer - Reporte de formulas");
        }

        private static string GetTemplatePath(string templateName)
        {
            string current = Path.Combine(Directory.GetCurrentDirectory(), "Views", "Template", templateName);
            return File.Exists(current) ? current : Path.Combine(AppContext.BaseDirectory, "Views", "Template", templateName);
        }
    }
}
