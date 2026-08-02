using WSOptimizer7.Models;

namespace WSOptimizer7.Services
{
    public interface IFormulaReportRenderer
    {
        string RenderBody(FormulaReporteDetalle reporte, FormulaCargaEtapa formula);
    }
}
