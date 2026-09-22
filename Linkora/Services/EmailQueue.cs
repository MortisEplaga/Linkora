using System.Threading.Channels;

namespace Linkora.Services
{
    public record EmailNotificationRequest(string ToEmail, string Subject, string BodyText);
    public interface IEmailQueue
    {
        void Enqueue(EmailNotificationRequest request);
        IAsyncEnumerable<EmailNotificationRequest> ReadAllAsync(CancellationToken ct);
    }
    public class EmailQueue : IEmailQueue
    {
        private readonly Channel<EmailNotificationRequest> _channel = Channel.CreateUnbounded<EmailNotificationRequest>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        public void Enqueue(EmailNotificationRequest request) => _channel.Writer.TryWrite(request);
        public IAsyncEnumerable<EmailNotificationRequest> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
    }
    public class EmailQueueBackgroundService : BackgroundService
    {
        private readonly IEmailQueue _queue;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<EmailQueueBackgroundService> _logger;
        public EmailQueueBackgroundService(IEmailQueue queue, IServiceScopeFactory serviceScopeFactory, ILogger<EmailQueueBackgroundService> logger)
        {
            _queue = queue;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EmailQueueBackgroundService started");

            await foreach (var request in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await emailService.SendNotificationEmailAsync(request.ToEmail, request.Subject, request.BodyText);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send queued notification email to {Email}", request.ToEmail);
                }
            }
        }
    }
}