namespace WSOptimizer7.Models
{
    using System;

    public class OptimizerModel
    {
        public OptimizerModel(string nombre, short orden, double valor)
        {
            this.Nombre = nombre;
            this.Orden = orden;
            this.Valor = valor;
        }

        public string Nombre { get; set; }
        public Int16 Orden { get; set; }
        public double Valor { get; set; }
    }

}
