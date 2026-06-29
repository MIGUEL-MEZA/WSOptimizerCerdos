using Newtonsoft.Json;

namespace WSOptimizer7.Models
{
    public class EtapaResModel
    {
        public int Clave { get; set; }
        public double Valor { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? ValorReferencia { get; set; }

        public EtapaResModel(int clave, double valor)
        {
            this.Clave = clave;
            this.Valor = valor;
        }
    }
}
