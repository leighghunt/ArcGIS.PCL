﻿namespace ArcGIS.ServiceModel
{
    using ArcGIS.ServiceModel.Operation;
    using ArcGIS.ServiceModel.Operation.Admin;
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// ArcGIS Server token provider
    /// </summary>
    public class TokenProvider : ITokenProvider, IDisposable
    {
        HttpClient _httpClient;
        protected GenerateToken TokenRequest;
        Token _token;
        PublicKeyResponse _publicKey;
        protected bool CanAccessPublicKeyEndpoint = true;

        /// <summary>
        /// Create a token provider to authenticate against ArcGIS Server
        /// </summary>
        /// <param name="rootUrl">Made up of scheme://host:port/site</param>
        /// <param name="username">ArcGIS Server user name</param>
        /// <param name="password">ArcGIS Server user password</param>
        /// <param name="serializer">Used to (de)serialize requests and responses</param>
        /// <param name="referer">Referer url to use for the token generation</param>
        /// <param name="cryptoProvider">Used to encrypt the token reuqest. If not set it will use the default from CryptoProviderFactory</param>
        public TokenProvider(string rootUrl, string username, string password, ISerializer serializer = null, string referer = "", ICryptoProvider cryptoProvider = null)
        {
            if (string.IsNullOrWhiteSpace(rootUrl)) throw new ArgumentNullException("rootUrl", "rootUrl is null.");
            Guard.AgainstNullArgument("username", username);
            Guard.AgainstNullArgument("password", password);

            Serializer = serializer ?? SerializerFactory.Get();
            if (Serializer == null) throw new ArgumentNullException("serializer", "Serializer has not been set.");
            RootUrl = rootUrl.AsRootUrl();
            CryptoProvider = cryptoProvider ?? CryptoProviderFactory.Get();
            _httpClient = HttpClientFactory.Get();
            TokenRequest = new GenerateToken(username, password) { Referer = referer };
            UserName = username;
        }

        ~TokenProvider()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_httpClient != null)
                {
                    _httpClient.Dispose();
                    _httpClient = null;
                }
                _token = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public ICryptoProvider CryptoProvider { get; private set; }

        public string RootUrl { get; private set; }

        public string UserName { get; private set; }

        public ISerializer Serializer { get; private set; }

        void CheckRefererHeader()
        {
            if (_httpClient == null || string.IsNullOrWhiteSpace(TokenRequest.Referer)) return;

            Uri referer;
            bool validReferrerUrl = Uri.TryCreate(TokenRequest.Referer, UriKind.Absolute, out referer);
            if (!validReferrerUrl)
                throw new HttpRequestException(string.Format("Not a valid url for referrer: {0}", TokenRequest.Referer));
            _httpClient.DefaultRequestHeaders.Referrer = referer;
        }

        //Token _token;
        /// <summary>
        /// Generates a token using the username and password set for this provider.
        /// </summary>
        /// <returns>The generated token or null if not applicable</returns>
        /// <remarks>This sets the Token property for the provider. It will be auto appended to 
        /// any requests sent through the gateway used by this provider.</remarks>
        public async Task<Token> CheckGenerateToken(CancellationToken ct)
        {
            if (TokenRequest == null) return null;

            if (_token != null && !_token.IsExpired) return _token;

            _token = null; // reset the Token
            _publicKey = null;

            CheckRefererHeader();

            var url = TokenRequest.BuildAbsoluteUrl(RootUrl).Split('?').FirstOrDefault();
            Uri uri;
            bool validUrl = Uri.TryCreate(url, UriKind.Absolute, out uri);
            if (!validUrl)
                throw new HttpRequestException(string.Format("Not a valid url: {0}", url));

            if (CryptoProvider != null && _publicKey == null && CanAccessPublicKeyEndpoint)
            {
                var publicKey = new PublicKey();
                var encryptionInfoEndpoint = publicKey.BuildAbsoluteUrl(RootUrl) + PortalGateway.AsRequestQueryString(Serializer, publicKey);

                string publicKeyResultString = null;
                try
                {
                    _httpClient.CancelPendingRequests();
                    HttpResponseMessage response = await _httpClient.GetAsync(encryptionInfoEndpoint, ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    publicKeyResultString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return null;
                }
                catch (HttpRequestException)
                {
                    CanAccessPublicKeyEndpoint = false;
                }

                if (ct.IsCancellationRequested) return null;

                if (CanAccessPublicKeyEndpoint)
                {
                    _publicKey = Serializer.AsPortalResponse<PublicKeyResponse>(publicKeyResultString);
                    if (_publicKey.Error != null) throw new InvalidOperationException(_publicKey.Error.ToString());

                    TokenRequest = CryptoProvider.Encrypt(TokenRequest, _publicKey.Exponent, _publicKey.Modulus);
                }
            }

            if (ct.IsCancellationRequested) return null;
            HttpContent content = new FormUrlEncodedContent(Serializer.AsDictionary(TokenRequest));

            _httpClient.CancelPendingRequests();

            string resultString = string.Empty;
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsync(uri, content, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                resultString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return null;
            }

            var result = Serializer.AsPortalResponse<Token>(resultString);

            if (result.Error != null) throw new InvalidOperationException(result.Error.ToString());

            if (!string.IsNullOrWhiteSpace(TokenRequest.Referer)) result.Referer = TokenRequest.Referer;

            _token = result;
            return _token;
        }
    }
}
