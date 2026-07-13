using System.Net;
using System.Text;

namespace WSOptimizer7.Services
{
    public class EmailTemplateRenderer : IEmailTemplateRenderer
    {
        public string RenderFromFile(string templatePath, IDictionary<string, string> values)
        {
            string resolvedPath = ResolveTemplatePath(templatePath);
            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException("No se encontro la plantilla HTML de correo.", resolvedPath);

            string html = File.ReadAllText(resolvedPath, Encoding.UTF8);
            foreach (KeyValuePair<string, string> value in values)
            {
                string placeholder = "{{" + value.Key + "}}";
                html = html.Replace(placeholder, WebUtility.HtmlEncode(value.Value ?? ""));
            }

            return html;
        }

        private static string ResolveTemplatePath(string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new InvalidOperationException("No se configuro la ruta de la plantilla HTML de correo.");

            if (Path.IsPathRooted(templatePath))
                return templatePath;

            string currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), templatePath);
            if (File.Exists(currentDirectoryPath))
                return currentDirectoryPath;

            return Path.Combine(AppContext.BaseDirectory, templatePath);
        }
    }
}
