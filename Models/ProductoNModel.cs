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

public class ProductoNModel
    {
        public Int16 CveProducto { get; set; }
        public string NomProducto { get; set; }
        public Int16 Posicion { get; set; }
        public double CA { get; set; }
        public double Costo { get; set; }
        public double Ractopamina { get; set; }
        public double EM { get; set; }
        public double EN { get; set; }
        public double SID { get; set; }
        public string IsEtapa { get; set; }
        public double Presupuesto { get; set; }
     //   public Decision PesoFinal { get; set; }
    }

}
