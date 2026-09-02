using Microsoft.AspNetCore.Http;

namespace WSOptimizer7.Models
{
    public class ApiResult<T>
    {
        public int Code { get; set; }
        public string Message { get; set; } = "";
        public T? Data { get; set; }
        public string TraceId { get; set; } = "";
    }

    public class FormulaCargaRequest
    {
        public IFormFile? Archivo { get; set; }
        public string UsuAct { get; set; } = "";
    }

    public class FormulaCargaDocumento
    {
        public int Version { get; set; } = 1;
        public int ProcesoActual { get; set; }
        public List<FormulaCargaProceso> Procesos { get; set; } = new();
    }

    public class FormulaCargaProceso
    {
        public int NumeroProceso { get; set; }
        public DateTime FechaCarga { get; set; }
        public string? UsuarioCarga { get; set; }
        public int? EstatusFinalPerfil { get; set; }
        public List<FormulaCargaEtapa> Etapas { get; set; } = new();
    }

    public class FormulaCargaEtapa
    {
        public int CveEtapa { get; set; }
        public string CodFormulaEnviada { get; set; } = "";
        public string CodFormulaCarga { get; set; } = "";
        public string Nombre { get; set; } = "";
        public FormulaDatosGenerales DatosGenerales { get; set; } = new();
        public List<FormulaMateriaPrima> MateriasPrimas { get; set; } = new();
        public List<FormulaNutriente> Nutrientes { get; set; } = new();
        public Dictionary<string, object?> SeccionesAdicionales { get; set; } = new();
    }

    public class FormulaDatosGenerales
    {
        public decimal? Cantidad { get; set; }
        public string Fecha { get; set; } = "";
        public decimal? Costo { get; set; }
        public List<string> CamposOriginales { get; set; } = new();
    }

    public class FormulaMateriaPrima
    {
        public int Orden { get; set; }
        public string RmCode { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public decimal? Porcentaje { get; set; }
        public decimal? Kilogramos { get; set; }
        public List<string> CamposOriginales { get; set; } = new();
        public Dictionary<string, object?> CamposAdicionales { get; set; } = new();
    }

    public class FormulaNutriente
    {
        public int Orden { get; set; }
        public string Descripcion { get; set; } = "";
        public decimal? Actual { get; set; }
        public string? Unidad { get; set; }
        public List<string> CamposOriginales { get; set; } = new();
        public Dictionary<string, object?> CamposAdicionales { get; set; } = new();
    }

    public class FormulaPerfilResumen
    {
        public long IdPerfil { get; set; }
        public string CodCliente { get; set; } = "";
        public string Cliente { get; set; } = "";
        public string Folio { get; set; } = "";
        public string Titulo { get; set; } = "";
        public int? Estatus { get; set; }
        public DateTime? Fecha { get; set; }
        public string? Usuario { get; set; }
        public int CantidadFormulas { get; set; }
    }

    public class FormulaReporteDetalle
    {
        public long IdPerfil { get; set; }
        public string CodCliente { get; set; } = "";
        public string Cliente { get; set; } = "";
        public string Folio { get; set; } = "";
        public string Titulo { get; set; } = "";
        public int NumeroProceso { get; set; }
        public List<FormulaCargaEtapa> Formulas { get; set; } = new();
    }

}
