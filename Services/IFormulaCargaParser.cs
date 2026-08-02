using WSOptimizer7.Models;

namespace WSOptimizer7.Services
{
    public interface IFormulaCargaParser
    {
        List<FormulaCargaEtapa> Parse(string contenido);
    }
}
