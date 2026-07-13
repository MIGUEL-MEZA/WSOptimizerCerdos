namespace WSOptimizer7.Services
{
    public class EmailMessage
    {
        public List<string> To { get; set; } = new List<string>();
        public List<string> Cc { get; set; } = new List<string>();
        public List<string> Bcc { get; set; } = new List<string>();
        public string Subject { get; set; } = "";
        public string HtmlBody { get; set; } = "";
        public List<string> AttachmentPaths { get; set; } = new List<string>();
    }
}
