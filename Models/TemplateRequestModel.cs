namespace WSOptimizer7.Models
{
    public class TemplateRequestModel
    {
        public long CvePerfilN { get; set; }
        public List<TemplateEtapaRequestModel> Etapas { get; set; } = new List<TemplateEtapaRequestModel>();
    }

    public class TemplateEtapaRequestModel
    {
        public int Clave { get; set; }
        public bool IsCodigoNuevo { get; set; } = false;
    }
}
