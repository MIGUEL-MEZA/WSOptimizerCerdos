namespace WSOptimizer7.Models
{
    using System;

    public class ProductoModel : ICloneable
    {
        public int CveProducto { get; set; }
        public string NomProducto { get; set; }
        public int Posicion { get; set; }
        public double CA { get; set; }
        public double Costo { get; set; }
        public double Gdp { get; set; }
        public double Ractopamina { get; set; }
        public double EM { get; set; }
        public double EN { get; set; }
        public double SID { get; set; }
        public string IsEtapa { get; set; }
        public double Presupuesto { get; set; }
        public double PesoFinal { get; set; }
        public double DuracionMin { get; set; } = 0;
        public double DuracionMax { get; set; } = 0;

        public object Clone()
        {
            ProductoModel prodNew = null;
            prodNew = new ProductoModel();
            prodNew.CveProducto = this.CveProducto;
            prodNew.NomProducto = this.NomProducto;
            prodNew.Posicion = this.Posicion;
            prodNew.CA = this.CA;
            prodNew.Costo = this.Costo;
            prodNew.Gdp = this.Gdp;
            prodNew.Ractopamina = this.Ractopamina;
            prodNew.EM = this.EM;
            prodNew.EN = this.EN;
            prodNew.SID = this.SID;
            prodNew.IsEtapa = this.IsEtapa;
            prodNew.Presupuesto = this.Presupuesto;
            prodNew.PesoFinal = this.PesoFinal;
            prodNew.DuracionMin = this.DuracionMin;
            prodNew.DuracionMax = this.DuracionMax;
            return prodNew;
        }
    }

}
