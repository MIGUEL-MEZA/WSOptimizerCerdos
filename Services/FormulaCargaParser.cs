using System.Globalization;
using System.Text;
using WSOptimizer7.Models;

namespace WSOptimizer7.Services
{
    public class FormulaCargaParser : IFormulaCargaParser
    {
        public List<FormulaCargaEtapa> Parse(string contenido)
        {
            if (string.IsNullOrWhiteSpace(contenido))
                throw new FormulaBusinessException(400, "El archivo de respuesta esta vacio.");

            var formulas = new List<FormulaCargaEtapa>();
            var porCodigo = new Dictionary<string, FormulaCargaEtapa>(StringComparer.OrdinalIgnoreCase);
            int lineaNumero = 0;

            foreach (string lineaOriginal in contenido.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                lineaNumero++;
                if (string.IsNullOrWhiteSpace(lineaOriginal))
                    continue;

                List<string> campos = ParseCsvLine(lineaOriginal);
                if (campos.Count < 2 || !int.TryParse(campos[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tipo))
                    throw new FormulaBusinessException(400, $"La linea {lineaNumero} no contiene un tipo de registro valido.");

                string codigo = campos[1].Trim();
                if (string.IsNullOrWhiteSpace(codigo))
                    throw new FormulaBusinessException(400, $"La linea {lineaNumero} no contiene codigo de formula.");

                if (tipo == 1)
                {
                    if (porCodigo.ContainsKey(codigo))
                        throw new FormulaBusinessException(305, $"El archivo contiene mas de un encabezado para la formula {codigo}.");

                    var formula = new FormulaCargaEtapa
                    {
                        CodFormulaCarga = codigo,
                        Nombre = Get(campos, 2),
                        DatosGenerales = new FormulaDatosGenerales
                        {
                            Cantidad = ParseDecimal(Get(campos, 3)),
                            Fecha = Get(campos, 4),
                            Costo = ParseDecimal(Get(campos, 5)),
                            CamposOriginales = campos.Skip(2).ToList()
                        }
                    };
                    formulas.Add(formula);
                    porCodigo[codigo] = formula;
                    continue;
                }

                if (!porCodigo.TryGetValue(codigo, out FormulaCargaEtapa? actual))
                    throw new FormulaBusinessException(400, $"La linea {lineaNumero} pertenece a {codigo}, pero no existe su registro general tipo 1.");

                if (tipo == 3)
                {
                    actual.Nutrientes.Add(new FormulaNutriente
                    {
                        Orden = actual.Nutrientes.Count + 1,
                        Actual = ParseDecimal(Get(campos, 2)),
                        Descripcion = Get(campos, 3),
                        CamposOriginales = campos.Skip(2).ToList()
                    });
                }
                else if (tipo == 4)
                {
                    decimal? porcentaje = ParseDecimal(Get(campos, 2));
                    decimal? cantidad = actual.DatosGenerales.Cantidad;
                    actual.MateriasPrimas.Add(new FormulaMateriaPrima
                    {
                        Orden = actual.MateriasPrimas.Count + 1,
                        Porcentaje = porcentaje,
                        Kilogramos = porcentaje.HasValue && cantidad.HasValue
                            ? decimal.Round(porcentaje.Value * cantidad.Value / 100m, 6)
                            : null,
                        RmCode = Get(campos, 3),
                        Descripcion = Get(campos, 4),
                        CamposOriginales = campos.Skip(2).ToList()
                    });
                }
                else
                {
                    string llave = $"tipo_{tipo}";
                    if (!actual.SeccionesAdicionales.TryGetValue(llave, out object? existente) || existente is not List<List<string>> filas)
                    {
                        filas = new List<List<string>>();
                        actual.SeccionesAdicionales[llave] = filas;
                    }
                    filas.Add(campos.Skip(2).ToList());
                }
            }

            if (formulas.Count == 0)
                throw new FormulaBusinessException(400, "El archivo no contiene registros generales tipo 1.");

            return formulas;
        }

        private static string Get(IReadOnlyList<string> campos, int indice)
        {
            return indice < campos.Count ? campos[indice].Trim() : "";
        }

        private static decimal? ParseDecimal(string valor)
        {
            if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal resultado))
                return resultado;
            return null;
        }

        private static List<string> ParseCsvLine(string linea)
        {
            var campos = new List<string>();
            var actual = new StringBuilder();
            bool entreComillas = false;

            for (int i = 0; i < linea.Length; i++)
            {
                char caracter = linea[i];
                if (caracter == '"')
                {
                    if (entreComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                    {
                        actual.Append('"');
                        i++;
                    }
                    else
                    {
                        entreComillas = !entreComillas;
                    }
                }
                else if (caracter == ',' && !entreComillas)
                {
                    campos.Add(actual.ToString());
                    actual.Clear();
                }
                else
                {
                    actual.Append(caracter);
                }
            }

            if (entreComillas)
                throw new FormulaBusinessException(400, "El archivo contiene una linea CSV con comillas sin cerrar.");

            campos.Add(actual.ToString());
            return campos;
        }
    }

    public class FormulaBusinessException : Exception
    {
        public FormulaBusinessException(int code, string message, object? data = null) : base(message)
        {
            Code = code;
            DataPayload = data;
        }

        public int Code { get; }
        public object? DataPayload { get; }
    }
}
