using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace lp_platform_gateway
{
    public class ReverseProxyMiddleware
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<ReverseProxyMiddleware> _logger;
        private readonly ProxyConfiguration _proxyConfiguration;
        private readonly Uri _defaultUri;

        public ReverseProxyMiddleware(RequestDelegate nextMiddleware, IOptions<ProxyConfiguration> proxyConfig, ILogger<ReverseProxyMiddleware> logger)
        {
            _httpClient.Timeout = Timeout.InfiniteTimeSpan;
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _proxyConfiguration = proxyConfig.Value;
            _defaultUri = new Uri(string.Format(_proxyConfiguration.DefaultURL, _proxyConfiguration.Stage));
        }

        public async Task Invoke(HttpContext context)
        {
            context.Request.EnableBuffering();

            var targetUri = await BuildTargetUri(context.Request);

            if (targetUri != null)
            {
                var targetRequestMessage = CreateTargetMessage(context, targetUri);

                using (var responseMessage = await _httpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
                {
                    context.Response.StatusCode = (int)responseMessage.StatusCode;
                    CopyFromTargetResponseHeaders(context, responseMessage);
                    await responseMessage.Content.CopyToAsync(context.Response.Body);
                }
                return;
            }
            await _nextMiddleware(context);
        }

        private HttpRequestMessage CreateTargetMessage(HttpContext context, Uri targetUri)
        {
            var requestMessage = new HttpRequestMessage();
            CopyFromOriginalRequestContentAndHeaders(context, requestMessage);

            requestMessage.RequestUri = targetUri;
            requestMessage.Headers.Host = targetUri.Host;

            context.Request.Headers.TryGetValue("Authorization", out var token);
            if (!string.IsNullOrWhiteSpace(token))
                requestMessage.Headers.Add("Authorization", token.ToString());
            requestMessage.Method = GetMethod(context.Request.Method);

            return requestMessage;
        }

        private void CopyFromOriginalRequestContentAndHeaders(HttpContext context, HttpRequestMessage requestMessage)
        {
            var requestMethod = context.Request.Method;

            if (!HttpMethods.IsGet(requestMethod) &&
              !HttpMethods.IsHead(requestMethod) &&
              !HttpMethods.IsDelete(requestMethod) &&
              !HttpMethods.IsTrace(requestMethod))
            {
                var streamContent = new StreamContent(context.Request.Body);
                requestMessage.Content = streamContent;
            }

            foreach (var header in context.Request.Headers)
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        private void CopyFromTargetResponseHeaders(HttpContext context, HttpResponseMessage responseMessage)
        {
            foreach (var header in responseMessage.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            context.Response.Headers.Remove("transfer-encoding");
        }
        private static HttpMethod GetMethod(string method)
        {
            if (HttpMethods.IsDelete(method)) return HttpMethod.Delete;
            if (HttpMethods.IsGet(method)) return HttpMethod.Get;
            if (HttpMethods.IsHead(method)) return HttpMethod.Head;
            if (HttpMethods.IsOptions(method)) return HttpMethod.Options;
            if (HttpMethods.IsPost(method)) return HttpMethod.Post;
            if (HttpMethods.IsPut(method)) return HttpMethod.Put;
            if (HttpMethods.IsTrace(method)) return HttpMethod.Trace;
            return new HttpMethod(method);
        }

        private async Task<Uri> BuildTargetUri(HttpRequest request)
        {
            Uri targetUri = null;
            var bodyStr = await GetBodyContent(request);

            var query = _proxyConfiguration.RedirectQueries.Find(pc => bodyStr.Contains(pc.Query));
            if (query != null)
            {
                targetUri = new Uri(query.RedirectURL);
            }
            else
            {
                targetUri = _defaultUri;
            }

            _logger.LogInformation("Redirecting reques to {url}", targetUri.AbsoluteUri);
            return targetUri;
        }

        private async Task<string> GetBodyContent(HttpRequest request)
        {
            var bodyStr = string.Empty;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
            {
                bodyStr = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }

            return bodyStr;
        }
    }
}
