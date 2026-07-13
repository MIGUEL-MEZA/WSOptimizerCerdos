namespace WSOptimizer7.Services
{
    public interface IEmailTemplateRenderer
    {
        string RenderFromFile(string templatePath, IDictionary<string, string> values);
    }
}
