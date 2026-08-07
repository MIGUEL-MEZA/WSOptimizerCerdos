namespace WSOptimizer7.Services
{
    /// <summary>
    /// Wrapper para indicar que un string contiene HTML seguro que NO debe ser escapado
    /// </summary>
    public class HtmlSafeString
    {
        public string Value { get; }

        public HtmlSafeString(string value)
        {
            Value = value ?? string.Empty;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
