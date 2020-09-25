﻿using BankingSDK.BE.Belfius.Contexts;
using BankingSDK.BE.Belfius.Models;
using BankingSDK.BE.Belfius.Models.Requests;
using BankingSDK.Base.BerlinGroup.Contexts;
using BankingSDK.Common;
using BankingSDK.Common.Contexts;
using BankingSDK.Common.Enums;
using BankingSDK.Common.Exceptions;
using BankingSDK.Common.Interfaces;
using BankingSDK.Common.Interfaces.Contexts;
using BankingSDK.Common.Models;
using BankingSDK.Common.Models.Data;
using BankingSDK.Common.Models.Request;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using static System.Net.WebRequestMethods;

namespace BankingSDK.BE.Belfius
{
    public class BeBelfiusConnector : SdkBaseConnector, IBankingConnector
    {
        private BelfiusUserContext _userContextLocal => (BelfiusUserContext)_userContext;
        private readonly Uri _sandboxUrl = new Uri("https://sandbox.api.belfius.be:8443");
        private readonly Uri _productionUrl = new Uri("https://psd2.b2b.belfius.be:8443");

        private Uri apiUrl => SdkApiSettings.IsSandbox ? _sandboxUrl : _productionUrl;
        private string basePath => SdkApiSettings.IsSandbox ? "/sandbox/psd2" : "";

        public string UserContext
        {
            get
            {
                return JsonConvert.SerializeObject(_userContext);
            }
            set
            {
                UserContextChanged = false;
                _userContext = JsonConvert.DeserializeObject<BelfiusUserContext>(value);
            }
        }

        public BeBelfiusConnector(BankSettings settings):base(settings, ConnectorType.BE_BELFIUS)
        {
        }

        #region User
        public async Task<BankingResult<IUserContext>> RegisterUserAsync(string userId)
        {
            _userContext = new BelfiusUserContext
            {
                UserId = userId
            };

            UserContextChanged = false;
            return new BankingResult<IUserContext>(ResultStatus.DONE, null, _userContext, JsonConvert.SerializeObject(_userContext));
        }
        #endregion

        #region Accounts
        public RequestAccountsAccessOption GetRequestAccountsAccessOption()
        {
            return RequestAccountsAccessOption.SingleAccount;
        }

        public async Task<BankingResult<List<Account>>> GetAccountsAsync()
        {
            try
            {
                var data = _userContextLocal.Accounts.Select(x => new Account
                {
                    Id = x.Id,
                    Currency = x.Currency,
                    Iban = x.Iban,
                    Description = x.Description,
                    BalancesConsent = new ConsentInfo
                    {
                        ConsentId = x.Token.RefreshToken,
                        ValidUntil = x.Token.ValidUntil
                    },
                    TransactionsConsent = new ConsentInfo
                    {
                        ConsentId = x.Token.RefreshToken,
                        ValidUntil = x.Token.ValidUntil
                    }
                }).ToList();

                return new BankingResult<List<Account>>(ResultStatus.DONE, "", data, JsonConvert.SerializeObject(data));
            }
            catch (ApiCallException e) { throw e; }
            catch (ApiUnauthorizedException e) { throw e; }
            catch (PagerException e) { throw e; }
            catch (SdkUnauthorizedException e) { throw e ; }
            catch (Exception e)
            {
                await LogAsync(apiUrl, 500, Http.Get, e.ToString());
                throw e;
            }
        }

        private async Task<HttpResponseMessage> GetAccountAsync(string token, string accountId, string redirectUri)
        {
            var client = GetClient();
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.belfius.api+json; version=1");
            client.DefaultRequestHeaders.Add("Accept-Language", "en");
            // TODO how do we get Redirect-URI here
            client.DefaultRequestHeaders.Add("Redirect-URI", redirectUri);
            client.DefaultRequestHeaders.Add("Authorization", $"{token}");
            var url = basePath + $"/accounts/{accountId}";

            return await client.GetAsync(url);
        }

        public async Task<BankingResult<string>> RequestAccountsAccessAsync(AccountsAccessRequest model)
        {
            try
            {
                Console.Write("Je vais ecrire " + model.RedirectUrl);
                Console.Write("Je vais ecrire " + model.FlowId);
                Console.Write("Je vais ecrire " + model.SingleAccount);
                Console.Write("Je vais ecrire " + model.PsuIp);
                Console.Write("Je vais ecrire " + model.FrequencyPerDay);




                var client = GetClient();

                client.DefaultRequestHeaders.Add("Accept", "application/vnd.belfius.api+json; version=2");
                client.DefaultRequestHeaders.Add("Accept-Language", "en");
                client.DefaultRequestHeaders.Add("Redirect-URI", $"{model.RedirectUrl}");
                client.DefaultRequestHeaders.Add("Code-Challenge-Method", "S256");

                // var codeVerifier = Guid.NewGuid().ToString().Replace("-", "");
                var codeVerifier = Guid.NewGuid().ToString() + "_" + Guid.NewGuid().ToString();
                string codeChallenge;
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    codeChallenge = Convert.ToBase64String(sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier)))
                                           .Split('=')[0].Replace("+", "-").Replace("/", "_");
                }
                client.DefaultRequestHeaders.Add("Code-Challenge", codeChallenge);
                var url = basePath + $"/consent-uris?scope=AIS&iban={model.SingleAccount}";
                Console.Write("Je vais ecrire Yass" + url);
                Console.Write("Je vais ecrire Code" + codeChallenge);

                var result = await client.GetAsync(url);

                string rawData = await result.Content.ReadAsStringAsync();
                var accountAccessResult = JsonConvert.DeserializeObject<BelfiusAccountAccessResponse[]>(rawData).FirstOrDefault();

                var flowContext = new FlowContext
                {
                    Id = model.FlowId,
                    ConnectorType = ConnectorType,
                    FlowType = FlowType.AccountsAccess,
                    CodeVerifier = codeVerifier,
                    RedirectUrl = model.RedirectUrl,
                    AccountAccessProperties = new AccountAccessProperties
                    {
                        SingleAccount = model.SingleAccount
                    }
                };

                if (!accountAccessResult.consent_uri.Contains("?"))
                {
                    accountAccessResult.consent_uri += "?";
                }
                else
                {
                    accountAccessResult.consent_uri += "&";
                }
                accountAccessResult.consent_uri += "state=" + HttpUtility.UrlEncode(model.FlowId);

                return new BankingResult<string>(ResultStatus.REDIRECT, url, accountAccessResult.consent_uri, rawData, flowContext: flowContext);
                }
                catch (ApiCallException e) { throw e; }
                catch (ApiUnauthorizedException e) { throw e; }
                catch (PagerException e) { throw e; }
                catch (SdkUnauthorizedException e) { throw e; }
                catch (Exception e)
                {
                Console.Write("Check ca :  " + apiUrl);
                await LogAsync(apiUrl, 500, Http.Get, e.ToString());
                throw e;
                
                }
        }

        public async Task<BankingResult<IUserContext>> RequestAccountsAccessFinalizeAsync(FlowContext flowContext, string queryString)
        {
            try
            {
                var query = HttpUtility.ParseQueryString(queryString);
                var error = query.Get("error");
                if (error != null)
                {
                    await LogAsync(apiUrl, 500, Http.Get, query.Get("error_description"));
                    throw new ApiCallException(query.Get("error_description"));
                }
                var code = query.Get("code");
                var auth = await GetToken(code, flowContext.CodeVerifier, flowContext.RedirectUrl);
                var result = await GetAccountAsync(auth.Token, auth.logical_id, flowContext.RedirectUrl);

                var account = JsonConvert.DeserializeObject<BelfiusAccount>(await result.Content.ReadAsStringAsync());
                bool fullAccess = flowContext.AccountAccessProperties.BalanceAccounts == null && flowContext.AccountAccessProperties.TransactionAccounts == null;

                var temp = _userContextLocal.Accounts.FirstOrDefault(x => x.Iban == flowContext.AccountAccessProperties.SingleAccount);
                if (temp != null)
                {
                    temp.Token = new UserToken
                    {
                        ValidUntil = DateTime.Now.AddDays(89),
                        Token = auth.Token,
                        TokenValidUntil = DateTime.Now.AddSeconds(auth.expires_in - 60),
                        RefreshToken = auth.refresh_token
                    };
                }
                else
                {
                    _userContextLocal.Accounts.Add(new Contexts.ConsentAccount
                    {
                        Id = auth.logical_id,
                        Iban = account.iban,
                        Currency = account.currency,
                        Description = account.type,
                        Token = new UserToken
                        {
                            ValidUntil = DateTime.Now.AddDays(89),
                            Token = auth.Token,
                            TokenValidUntil = DateTime.Now.AddSeconds(auth.expires_in - 60),
                            RefreshToken = auth.refresh_token
                        }
                    });
                }

                _userContextLocal.RedirectUri = flowContext.RedirectUrl;

                //cleanup
                _userContextLocal.Accounts.RemoveAll(x => x.Token.ValidUntil < DateTime.Now);
                
                return new BankingResult<IUserContext>(ResultStatus.DONE, null, _userContext, JsonConvert.SerializeObject(_userContext));
            }
            catch (ApiCallException e) { throw e; }
            catch (ApiUnauthorizedException e) { throw e; }
            catch (PagerException e) { throw e; }
            catch (SdkUnauthorizedException e) { throw e; }
            catch (Exception e)
            {

                await LogAsync(apiUrl, 500, Http.Get, e.ToString());
                throw e;
            }
        }

        public async Task<BankingResult<IUserContext>> RequestAccountsAccessFinalizeAsync(string flowContextJson, string queryString)
        {
            return await RequestAccountsAccessFinalizeAsync(JsonConvert.DeserializeObject<FlowContext>(flowContextJson), queryString);
        }

        public async Task<BankingResult<List<BankingAccount>>> DeleteConsentAsync(string consentId)
        {
            try
            {
                var client = GetClient();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var url = basePath + $"/berlingroup/v1/consents/{consentId}";
                var result = await client.DeleteAsync(url);

                var data = _userContextLocal.Accounts.Where(x => x.Token.RefreshToken == consentId).Select(x => new BankingAccount
                {
                    Iban = x.Iban,
                    Currency = x.Currency
                }).ToList();

                _userContextLocal.Accounts.RemoveAll(x => x.Token.RefreshToken == consentId);
                
                return new BankingResult<List<BankingAccount>>(ResultStatus.DONE, url, data, JsonConvert.SerializeObject(data));
            }
            catch (ApiCallException e) { throw e; }
            catch (ApiUnauthorizedException e) { throw e; }
            catch (PagerException e) { throw e; }
            catch (SdkUnauthorizedException e) { throw e; }
            catch (Exception e)
            {
                await LogAsync(apiUrl, 500, Http.Get, e.ToString());
                throw e;
            }
        }
        #endregion

        #region Balances
        public async Task<BankingResult<List<Balance>>> GetBalancesAsync(string accountId)
        {
            try
            {
                var account = _userContextLocal.Accounts.FirstOrDefault(x => x.Id == accountId) ?? throw new ApiCallException("Invalid accountId");
                var client = GetClient();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (account.Token.TokenValidUntil < DateTime.Now)
                {
                    await RefreshToken(account.Token);
                }

                var url = basePath + $"/accounts/{accountId}";
                var result = await GetAccountAsync(account.Token.Token, accountId, _userContextLocal.RedirectUri);

                string rawData = await result.Content.ReadAsStringAsync();
                var model = JsonConvert.DeserializeObject<BelfiusAccount>(rawData);

                var balence = new Balance
                {
                    BalanceAmount = new BalanceAmount
                    {
                        Amount = model.balance,
                        Currency = model.currency
                    }
                };

                return new BankingResult<List<Balance>>(ResultStatus.DONE, url, new List<Balance> { balence }, rawData);
            }
            catch (ApiCallException e) { throw e; }
            catch (ApiUnauthorizedException e) { throw e; }
            catch (PagerException e) { throw e; }
            catch (SdkUnauthorizedException e) { throw e; }
            catch (Exception e)
            {
                await LogAsync(apiUrl, 500, Http.Get, e.ToString());
                throw e;
            }
        }
        #endregion

        #region Transactions
        public async Task<BankingResult<List<Transaction>>> GetTransactionsAsync(string accountId, IPagerContext context = null)
        {
            try
            {
                var account = _userContextLocal.Accounts.FirstOrDefault(x => x.Id == accountId) ?? throw new ApiCallException("Invalid accountId");
                BelfiusPagerContext pagerContext = (context as BelfiusPagerContext) ?? new BelfiusPagerContext();

                var client = GetClient();
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.belfius.api+json; version=1");
                client.DefaultRequestHeaders.Add("Accept-Language", "en");
                // TODO how do we get Redirect-URI here
                client.DefaultRequestHeaders.Add("Redirect-URI", _userContextLocal.RedirectUri);
                if (account.Token.TokenValidUntil < DateTime.Now)
                {
                    await RefreshToken(account.Token);
                }
                client.DefaultRequestHeaders.Add("Authorization", account.Token.Token);
                var url = basePath + $"/accounts/{accountId}/transactions{pagerContext.GetRequestParams()}";
                var result = await client.GetAsync(url);

                string rawData = await result.Content.ReadAsStringAsync();
                var model = JsonConvert.DeserializeObject<BelfiusTransactions>(rawData);
                pagerContext.AddNextPageKey(model._embedded.next_page_key);
                //TODO create pager context
                //pagerContext.SetPage(pagerContext.GetNextPage());
                //pagerContext.SetPageTotal(model._embedded.transactions..PageTotal);

                var data = model._embedded.transactions.Select(x => new Transaction
                {
                    Id = x.transaction_ref,
                    Amount = x.amount,
                    CounterpartReference = x.counterparty_account,
                    Currency = x.currency,
                    ExecutionDate = x.execution_date,
                    Description = x.communication,
                    CounterpartName = x.counterparty_info
                }).ToList();

                // JGD Do we keep this trace?
                //await sdkApiConnector.Log((int)ConnectorType, _url, (int)watch.ElapsedMilliseconds, url, (int)result.StatusCode, Http.Get, requestedAt, _userContext.UserId, null/*model.account.iban*/);

                return new BankingResult<List<Transaction>>(ResultStatus.DONE, url, data, rawData, pagerContext);
            }
            catch (ApiCallException e) { throw e; }
            catch (ApiUnauthorizedException e) { throw e; }
            catch (PagerException e) { throw e; }
            catch (SdkUnauthorizedException e) { throw e; }
            catch (Exception e)
            {
                await LogAsync(apiUrl, 500, Http.Get, e.ToString());
                throw e;
            }
        }
        #endregion

        #region Payment
        public async Task<BankingResult<string>> CreatePaymentInitiationRequestAsync(PaymentInitiationRequest model)
        {
            var paymentRequest = new BelfiusPaymentRequest
            {
                remote_account = new RemoteAccount
                {
                    iban = model.Recipient.Iban,
                    name = model.Recipient.Name
                },
                origin_account = new OriginAccount
                {
                    iban = model.Debtor.Iban
                },
                amount = model.Amount,
                currency = model.Currency,
                payment_id = model.EndToEndId,
                execution_date = model.RequestedExecutionDate?.ToString("yyyy-MM-dd"),
                payment_treatment_type = "NORMAL"
            };

            var content = new StringContent(JsonConvert.SerializeObject(paymentRequest), Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");
            var client = GetClient();
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.belfius.api+json; version=1");
            client.DefaultRequestHeaders.Add("Accept-Language", "en");
            client.DefaultRequestHeaders.Add("Redirect-URI", model.RedirectUrl);
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer test");
            client.DefaultRequestHeaders.Add("Code-Challenge-Method", "S256");
            client.DefaultRequestHeaders.Add("Code-Challenge", "test");
            //TODO add signature
            client.DefaultRequestHeaders.Add("Signature", "test");
            var url = basePath + $"/payments/sepa-credit-transfers";
            var result = await client.PostAsync(url, content);

            var rawData = await result.Content.ReadAsStringAsync();

            return new BankingResult<string>(ResultStatus.REDIRECT, url, ""/*redirect*/, rawData, null/*flowContext: flowContext*/);
        }

        public async Task<BankingResult<PaymentStatus>> CreatePaymentInitiationRequestFinalizeAsync(FlowContext flowContext, string queryString)
        {
            var query = HttpUtility.ParseQueryString(queryString);
            var error = query.Get("error");
            if (error != null)
            {
                await LogAsync(apiUrl, 500, Http.Get, query.Get("error_description"));
                throw new ApiCallException(query.Get("error_description"));
            }

            var client = GetClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var url = basePath + $"/payments/sepa-credit-transfers/{flowContext.PaymentProperties.PaymentId}";
            var result = await client.GetAsync(url);

            var rawData = await result.Content.ReadAsStringAsync();
            //var paymentResult = JsonConvert.DeserializeObject<PaymentStatusDto>(rawData);

            var data = new PaymentStatus
            {
                //Amount = new BankingAccountInstructedAmount
                //{
                //    Amount = paymentResult.instructedAmount.amount,
                //    Currency = paymentResult.instructedAmount.currency
                //},
                //Creditor = new BankingAccount
                //{
                //    Iban = paymentResult.creditorAccount.iban,
                //    Currency = paymentResult.creditorAccount.currency
                //},
                //CreditorName = paymentResult.creditorName,
                //Debtor = new BankingAccount
                //{
                //    Iban = paymentResult.debtorAccount.iban,
                //    Currency = paymentResult.debtorAccount.currency
                //},
                //EndToEndIdentification = paymentResult.endToEndIdentification,
                //Status = paymentResult.transactionStatus
            };

            return new BankingResult<PaymentStatus>(ResultStatus.DONE, url, data, rawData);
        }

        public async Task<BankingResult<PaymentStatus>> CreatePaymentInitiationRequestFinalizeAsync(string flowContextJson, string queryString)
        {
            return await CreatePaymentInitiationRequestFinalizeAsync(JsonConvert.DeserializeObject<FlowContext>(flowContextJson), queryString);
        }
        #endregion

        #region Pager
        public IPagerContext RestorePagerContext(string json)
        {
            return JsonConvert.DeserializeObject<BelfiusPagerContext>(json);
        }

        public IPagerContext CreatePageContext(byte limit)
        {
            return new BelfiusPagerContext(limit);
        }
        #endregion

        #region Private

        private async Task<BelfiusAccessData> GetToken(string authorizationCode, string codeVerifier, string redirectUrl)
        {
            Console.Write("Je comprends petit a petit.");

            string codeChallenge;
            var codeVerifier2 = Guid.NewGuid();//.ToString().Replace("-", "");

            using (SHA256 sha256Hash = SHA256.Create())
            {
                codeChallenge = Convert.ToBase64String(sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier)))
                    .Split('=')[0].Replace("+", "-").Replace("/", "_");
            }

            var querystring = $"grant_type=authorization_code&code={authorizationCode}&redirect_uri={HttpUtility.UrlEncode(redirectUrl)}&code_verifier={codeVerifier}";
            var content = new StringContent($"grant_type=authorization_code&code={authorizationCode}&redirect_uri={HttpUtility.UrlEncode(redirectUrl)}&code_verifier={codeVerifier}",
                   Encoding.UTF8, "application/x-www-form-urlencoded");
            var client = GetClient();
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.belfius.api+json; version=1");
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.AppClientId}:{_settings.AppClientSecret}")));
            var result = await client.PostAsync(basePath + $"/token", content);

            if (!result.IsSuccessStatusCode)
            {
                throw new ApiUnauthorizedException(await result.Content.ReadAsStringAsync());
            }

            return JsonConvert.DeserializeObject<BelfiusAccessData>(await result.Content.ReadAsStringAsync());
        }

        private async Task RefreshToken(UserToken consent)
        {
            var content = new StringContent($"refresh_token={consent.RefreshToken}&grant_type=refresh_token",//&redirect_uri={redirectUrl}
                   Encoding.UTF8, "application/x-www-form-urlencoded");
            var client = GetClient();
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.belfius.api+json; version=1");
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.AppClientId}:{_settings.AppClientSecret}")));
            var result = await client.PostAsync(basePath + $"/token", content);

            if (!result.IsSuccessStatusCode)
            {
                throw new ApiUnauthorizedException(await result.Content.ReadAsStringAsync());
            }

            var auth = JsonConvert.DeserializeObject<BelfiusAccessData>(await result.Content.ReadAsStringAsync());
            consent.Token = auth.Token;
            consent.TokenValidUntil = DateTime.Now.AddSeconds(auth.expires_in - 60);
            UserContextChanged = true;
        }

        private SdkHttpClient GetClient()
        {
            Console.Write("Je suis dans getClient :  URL ::     "  +  apiUrl + "     " ); 
            SdkHttpClient sdkHttpClient = GetSdkClient(apiUrl);
            Console.Write("Je suis dans getClient2");

            sdkHttpClient.DefaultRequestHeaders.Add("Request-ID", Guid.NewGuid().ToString());
            Console.Write("Je suis dans getClient3");

            sdkHttpClient.DefaultRequestHeaders.Add("Client-ID", _settings.AppClientId);
            Console.Write("Je suis dans getClient4");


            return sdkHttpClient;
        }
        #endregion
    }
}
