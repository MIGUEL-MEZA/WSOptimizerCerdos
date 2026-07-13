namespace WSOptimizer7.Models
{
    public class EmailTestRequestModel
    {
        public List<string> Destinatarios { get; set; } = new List<string>();
        public string DestinatariosTexto { get; set; } = "";
        public List<string> Cc { get; set; } = new List<string>();
        public string CcTexto { get; set; } = "";
        public List<string> Bcc { get; set; } = new List<string>();
        public string BccTexto { get; set; } = "";
        public string Asunto { get; set; } = "";
        public string CuerpoHtml { get; set; } = "";
    }
}
