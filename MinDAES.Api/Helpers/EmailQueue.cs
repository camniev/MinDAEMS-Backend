namespace MinDAES.Api.Helpers
{
    using System.Threading.Channels;

    // A small in-memory outbox so sending invitation emails never blocks (or breaks) enrolling
    // applicants. Gmail allows only a few sends per second per mailbox, so a batch of 100
    // applicants sent inside the HTTP request would be slow and could time out.
    //
    // ---- Program.cs ----
    //   builder.Services.AddSingleton<EmailQueue>();
    //   builder.Services.AddHostedService<EmailBackgroundService>();
    // (IEmailSender is already registered as a singleton from GmailEmailSender.cs)
    //
    // Limitation: the queue lives in memory, so mail still waiting when the app restarts is
    // lost. The Resend button on the Applicants screen is the safety net.

    public record EmailJob(string To, string Subject, string BodyHtml);

    public class EmailQueue
    {
        private readonly Channel<EmailJob> _channel = Channel.CreateUnbounded<EmailJob>();

        public ValueTask EnqueueAsync(EmailJob job) => _channel.Writer.WriteAsync(job);

        public IAsyncEnumerable<EmailJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
    }

    public class EmailBackgroundService : BackgroundService
    {
        private const int MaxAttempts = 3;

        private readonly EmailQueue _queue;
        private readonly IEmailSender _sender;
        private readonly ILogger<EmailBackgroundService> _logger;

        public EmailBackgroundService(EmailQueue queue, IEmailSender sender, ILogger<EmailBackgroundService> logger)
        {
            _queue = queue;
            _sender = sender;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                for (var attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    try
                    {
                        await _sender.SendAsync(job.To, job.Subject, job.BodyHtml, stoppingToken);
                        break;
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        if (attempt == MaxAttempts)
                        {
                            // Don't log the body: it contains the applicant's code.
                            _logger.LogError(ex, "Giving up sending an email to {To} after {Attempts} attempts.", job.To, MaxAttempts);
                        }
                        else
                        {
                            _logger.LogWarning(ex, "Sending an email to {To} failed (attempt {Attempt}); retrying.", job.To, attempt);
                            await Task.Delay(TimeSpan.FromSeconds(attempt * 5), stoppingToken);
                        }
                    }
                }

                // Stay under Gmail's per-second send limit for one mailbox.
                await Task.Delay(400, stoppingToken);
            }
        }
    }
}
