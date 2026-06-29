namespace WSOptimizer7.Models
{
    using System;

    public class ResultadoOptimizerModel
    {
     
        public int CveParametro { get; set; }
        public short Seleccionado { get; set; } = 2;
        public double Costo { get; set; }
        public double Cda { get; set; }
        public double Presupuesto { get; set; }
        public double Gdp { get; set; }
        public double Ca { get; set; }
        public double DuracionTotal { get; set; }
        public double PrecioVenta { get; set; }
        public double PesoVenta { get; set; }
        public double EdadVenta { get; set; }
        public double KilosProducidos { get; set; }
        public double CostoPonderado { get; set; }
        public double CostoTotalAlimento { get; set; }
        public double CostoKiloProducido { get; set; }
        public double Utilidad { get; set; }
        public double Roi{ get; set; }
        public List<EtapaResModel> Presupuestos { get; set; } = new List<EtapaResModel>();
    }

}
