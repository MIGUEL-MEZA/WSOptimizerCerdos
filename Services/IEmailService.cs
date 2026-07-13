namespace WSOptimizer7.Services
{
    public interface IEmailService
    {
        Task SendAsync(EmailMessage message);
    }
}
