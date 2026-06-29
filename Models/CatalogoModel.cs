namespace WSOptimizer7.Models
{
    public class CatalogoModel
    {
        public string Clave { get; set; }
        public string Valor { get; set; }

        public CatalogoModel(string clave, string valor)
        {
            this.Clave = clave;
            this.Valor = valor;
        }
    }

}
