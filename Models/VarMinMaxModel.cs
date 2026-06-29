namespace WSOptimizer7.Models
{
    public class VarMinMaxModel
    {
        public VarMinMaxModel(double valorMinimo, double valorMaximo, double edadMinAnteriorFinal, double edadMaxAnteriorFinal)
        {
            this.ValorMinimo = valorMinimo;
            this.ValorMaximo = valorMaximo;
            this.EdadMinAnteriorFinal = edadMinAnteriorFinal;
            this.EdadMaxAnteriorFinal = edadMaxAnteriorFinal;
        }

        public double ValorMinimo { get; set; }
        public double ValorMaximo { get; set; }
        public double EdadMinAnteriorFinal { get; set; }
        public double EdadMaxAnteriorFinal { get; set; }
    }

}
