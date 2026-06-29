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

    public class ResponseOptModel
    {
        public List<CatalogoModel> LstProducto { get; set; } = new List<CatalogoModel>();
        public double ValorObjetivo { get; set; }
        public  List<OptimizerModel> Optimizer { get; set; }
    }

}
