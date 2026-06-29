namespace WSOptimizer7.Models
{
    public class EtapaModel
    {
        public int Clave { get; set; }
        public double PesoInicial { get; set; }
        public double PesoFinal { get; set; }
        public double? PorcGDP { get; set; }
        public double ENAlimento { get; set; }
        public bool IsRactopamina { get; set; } = false;
    }

}
