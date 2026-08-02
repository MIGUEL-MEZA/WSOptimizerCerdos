using WSOptimizer7.Models;

namespace WSOptimizer7.Services
{
    public interface IFormulaPdfService
    {
        byte[] Generate(FormulaReporteDetalle reporte, IEnumerable<FormulaCargaEtapa> formulas);
    }
}
