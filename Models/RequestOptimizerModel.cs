namespace WSOptimizer7.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Security;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualBasic;

    public class RequestOptimizerModel : ICloneable
    {
        public int CvePlan{ get; set; }
        public string UsuAct { get; set; }
        public int CveReferencia { get; set; }
        public int CveParametro { get; set; }
        public double PrecioVenta { get; set; }
        public double Desperdicio { get; set; }
        public double PesoPromedio { get; set; }
        public double EdadDestete { get; set; }
        public double EdadSalida { get; set; }
        public double EdadVenta { get; set; }
        public double DiasRactopamina { get; set; }
        public int CveEstado { get; set; }
        public double Temperatura { get; set; }
        public double MetrosCerdos { get; set; }
        

        public List<ProductoModel> Productos { get; set; }
        public bool IsOptimizar { get; set; } = false;
        public double EdadFinalTmp { get; set; }
        public double EdadInicialTmp { get; set; }
        public double PesoInicialTmp { get; set; }

        public object Clone()
        {
            RequestOptimizerModel requestNew = null;
            requestNew = new RequestOptimizerModel();
            // cargar los valores de las variables miembro 
            // en el objeto nuevo 
            requestNew.EdadSalida = this.EdadSalida;
            requestNew.EdadVenta = this.EdadVenta;
            requestNew.EdadFinalTmp = this.EdadFinalTmp;
            requestNew.CveEstado = this.CveEstado;
            requestNew.CveParametro = this.CveParametro;
            requestNew.CvePlan= this.CvePlan;
            requestNew.CveReferencia = this.CveReferencia;
            requestNew.Desperdicio = this.Desperdicio;
            requestNew.DiasRactopamina = this.DiasRactopamina;
            requestNew.EdadDestete = this.EdadDestete;
            requestNew.EdadInicialTmp = this.EdadInicialTmp;
            requestNew.IsOptimizar = this.IsOptimizar;
            requestNew.PesoInicialTmp = this.PesoInicialTmp;
            requestNew.PesoPromedio = this.PesoPromedio;
            requestNew.PrecioVenta = this.PrecioVenta;
            requestNew.MetrosCerdos = this.MetrosCerdos;
            requestNew.Productos = this.Productos.Select(p => (ProductoModel) p.Clone()).ToList();
            requestNew.Temperatura = this.Temperatura;
            requestNew.UsuAct = this.UsuAct;
            // devolver la referencia del objeto creado
            return requestNew;
        }
    }

}
