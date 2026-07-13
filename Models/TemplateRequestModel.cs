namespace WSOptimizer7.Models
{
    public class TemplateRequestModel
    {
        public long CvePerfilN { get; set; }
        public List<TemplateEtapaRequestModel> Etapas { get; set; } = new List<TemplateEtapaRequestModel>();
        public string UsuAct { get; set; } = "";
    }

    public class TemplateEtapaRequestModel
    {
        public int CveEtapa { get; set; }
        public int? CveAccion { get; set; }
        public int? CveEstatus { get; set; }
        public string Nota { get; set; } = "";
    }
}
