using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NetCoreApp
{
    public static class HttpFactory
    {
        public static HttpClient Client { get; }

        static HttpFactory()
        {
            Client = new HttpClient(new HttpClientHandler());
        }
    }

    public class FileServerProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ClientSettings _settings;
        private static readonly HttpMethod _httpMethod = new HttpMethod("GET");

        public FileServerProxyMiddleware(RequestDelegate next, IOptions<ClientSettings> settings)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _settings = settings.Value;

            if (string.IsNullOrEmpty(_settings.FileServerProxyAddress))
            {
                throw new ArgumentException($"Settings parameter must specify {nameof(_settings.FileServerProxyAddress)}.");
            }
        }

        public Task Invoke(HttpContext context)
        {
            var method = context.Request.Method;
            if (method == "GET")
            {
                var path = context.Request.Path;
                if (path.StartsWithSegments(Globals.FileServerProxyPath))
                {
                    return HandleFileServerHttpRequest(context);
                }
            }

            return _next(context);
        }

        private async Task HandleFileServerHttpRequest(HttpContext context)
        {
            var requestMessage = new HttpRequestMessage();
            var requestMethod = context.Request.Method;

            // Copy the request headers
            foreach (var header in context.Request.Headers)
            {
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                {
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            var proxyServer = new Uri(_settings.FileServerProxyAddress);
            requestMessage.RequestUri = new Uri(proxyServer, $"{context.Request.Path}{context.Request.QueryString}");
            requestMessage.Method = new HttpMethod(requestMethod);
            var responseMessage = await HttpFactory.Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            var statusCode = (int)responseMessage.StatusCode;
            context.Response.StatusCode = statusCode;

            foreach (var header in responseMessage.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            // SendAsync removes chunking from the response. This removes the header so it doesn't expect a chunked response.
            context.Response.Headers.Remove("transfer-encoding");
            await responseMessage.Content.CopyToAsync(context.Response.Body);
        }
    }
}