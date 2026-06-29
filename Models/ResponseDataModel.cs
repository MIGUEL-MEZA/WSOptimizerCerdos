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

    public class ResponseDataModel
    {
        public int NoVariable { get; set; }
        public string Variable { get; set; }
        public int Posicion { get; set; }
        public string MostrarCliente { get; set; }


        public List<EtapaResModel> Etapas { get; set; }
    }

}
