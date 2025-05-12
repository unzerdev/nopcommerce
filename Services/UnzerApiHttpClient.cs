using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using Microsoft.Net.Http.Headers;
using Nop.Core;
using Nop.Services.Logging;
using Unzer.Plugin.Payments.Unzer.Models.Api;

namespace Unzer.Plugin.Payments.Unzer.Services
{
    public class UnzerApiHttpClient
    {
        private readonly HttpClient _httpClient;
        private UnzerPaymentSettings _unzerPaymentSettings;
        private readonly ILogger _logger;
        private string _credentials;

        public UnzerApiHttpClient(HttpClient httpClient, UnzerPaymentSettings unzerPaymentSettings, ILogger logger)
        {
            _httpClient = httpClient;
            _unzerPaymentSettings = unzerPaymentSettings;
            _logger = logger;

            InitializeClient();
        }

        public UnzerPaymentSettings UnzerPaymentSettings
        {
            set
            {
                _unzerPaymentSettings = value;
                InitializeClient();
            }
        }

        private void InitializeClient()
        {
            if (_unzerPaymentSettings.UnzerApiBaseUrl != null && !string.IsNullOrEmpty(_unzerPaymentSettings.UnzerApiKey))
            {
                _credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:", _unzerPaymentSettings.UnzerApiKey)));

                var baseUrl = _unzerPaymentSettings.UnzerApiBaseUrl.EndsWith("/") ? _unzerPaymentSettings.UnzerApiBaseUrl : $"{_unzerPaymentSettings.UnzerApiBaseUrl}/";

                _httpClient.BaseAddress = new Uri(baseUrl);
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, MimeTypes.ApplicationJson);
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", string.Format("Basic {0}", _credentials));
            }
        }

        public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request) where TRequest : IUnzerApiRequest where TResponse : UnzerApiResponse
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            options.Converters.Add(new JsonStringEnumConverter()); // you can add multiple converters

            //prepare request parameters
            var requestString = JsonSerializer.Serialize(request, options);
            var requestContent = new StringContent(requestString, Encoding.UTF8, MimeTypes.ApplicationJson);

            var requestMessage = new HttpRequestMessage(new HttpMethod(request.Method), new Uri(new Uri(request.BaseUrl), request.Path))
            {
                Content = requestContent
            };

            //execute request and get result
            var httpResponse = await _httpClient.SendAsync(requestMessage);
            var responseString = await httpResponse.Content.ReadAsStringAsync();            

            var result = JsonSerializer.Deserialize<TResponse>(responseString ?? string.Empty);

            if(_unzerPaymentSettings.LogCallbackPostData)
                await _logger.InformationAsync($"UnzerApiHttpClient.RequestAsync Request content sent: {requestString}");

            if (!httpResponse.IsSuccessStatusCode)
            { 
                result.HttpStatusCode = httpResponse.StatusCode;
                result.IsError = true;
                result.ErrorResponse = JsonSerializer.Deserialize<UnzerApiErrorResponse>(responseString ?? string.Empty);

                await _logger.InformationAsync($"UnzerApiHttpClient.RequestAsync Failed with Respones content: {responseString}");

                return result;
            }
            
            result.IsSuccess = true;

            return result;
        }
    }
}
