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

    public class RequestModel
    {
        public int CvePerfilN { get; set; }
        public string UsuAct { get; set; }
        public int Referencia { get; set; }
        public double Temperatura { get; set; }
        public double Espacio { get; set; }
        public double PPMRAC { get; set; }
        public List<EtapaModel> EtapasModel { get; set; }
    }

}
