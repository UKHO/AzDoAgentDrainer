using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AzureVmAgentsService
{
    public class SimpleLoggingHandler : DelegatingHandler
    {
        private readonly ILogger<SimpleLoggingHandler> _logger;

        public SimpleLoggingHandler(ILogger<SimpleLoggingHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sending request");

            var response = await base.SendAsync(request, cancellationToken);

            _logger.LogInformation("Request completed {statusCode} {address} {content}", response.StatusCode, response.RequestMessage.RequestUri, await response.Content.ReadAsStringAsync());

            return response;
        }
    }
}
