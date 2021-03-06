﻿using BankingSDK;
using BankingSDK.Common;
using BankingSDK.Common.Contexts;
using BankingSDK.Common.Enums;
using BankingSDK.Common.Interfaces;
using BankingSDK.Common.Interfaces.Contexts;
using BankingSDK.Common.Models;
using BankingSDK.Common.Models.Data;
using BankingSDK.Common.Models.Request;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace GettingStarted
{
    /// <summary>
    /// Getting Started Console App for BankingSDK
    /// 
    /// This project uses a local copy of BankingSDK.dll
    /// 
    /// This local copy will not be updated !!
    /// 
    /// If you intend to run this example, please download the latest dll version 
    /// from the BankingSDK developer portal and replace it in the folder BankingSDK
    /// </summary>
    class Program
    {
        const string NcaId = "PSDBE-NBB-0652642328"; 
        const string TlsCertificatePath = @"C:\Users\Yassine\Documents\StageEntrainement\BankingSDK - Copie\BankingSDK\Test\TestApp\example_eidas_client_tls.cer";
        const string TlsCertificatePassword = "password";
        const string SigningCertificatePath = @"C:\Users\Yassine\Documents\StageEntrainement\BankingSDK - Copie\BankingSDK\Test\TestApp\example_eidas_client_signing.cer";
        const string SigningCertificatePassword = "password2";

        const string BankingSDKCompanyKey = "1df53a2d-26aa-4ef1-9f3c-09a1092e5413";
        const string BankingSDKApplicationKey = "d7726cbf-e51f-4e0e-a3bb-132913ad8032";
        const string BankingSDKSecret = "W2ANVUVRQS7AKBFCZLKHGLWXCC6FRUGXAV3O3E33H7D1ZULX6ME6UKOKGNKSXPA6LK9YH1JUSR9JI58S0JIEPKCZHSIQ4KF27GPOUWPFDTODKECLP11OG5RBHJRZT20HMHLBPT7D4DB559PCP6BCS1VBZOHKF0VK3G1D2B564XUD4VFH4OJW1YZVVKLBLW8ER0CYIMKOBEH4GSGEI9Q1Q9SERGRSZ3UHZQVDTR9SD4UWQ4ODDH3MGGXSI7GC42ZH";
        const string BankingSDKTTPName = "EXTHAND";

        const string StoreFolderPath = @"C:\Users\Yassine\Documents\StageEntrainement\BankingSDK - Copie\BankingSDK\Test\GettingStarted\BankingSDK";
        const string AppClientId = "62fdf373-22da-4e03-b4b8-9d0a3f839cf7";
        const string AppClientSecret = "b764173f83ac273f33d9f2f8a8994621e064f8a2048cf0f16ea33cf082300e0b82788b6c957583a159d11db5b12bf450";

        const string RedirectURL = "https://developer.bankingsdk.com/Callback";


        static async Task Main(string[] args)
        {
            // Initialize BankingSDK
            Setup();

            // Fetch UserID
            string userId = GetUserId();

            // Initialize Connector
            IBankingConnector bankConnector = BankingFactory.GetConnector(ConnectorType.BE_BNP);

            try
            {
                // Fetch UserContext
                string userContext = FetchUserContext(userId);
                if (userContext == null)
                {
                    Console.Write("Je suis rentré la");

                    // No context found, initialize a new one
                    await bankConnector.RegisterUserAsync(userId);
                    Console.Write("Je suis rentré la aussi ");


                    // Assume the user didn't gave access to his accounts yet
                    
                    await RequestAccountsAccessAsync(bankConnector);
                    Console.Write("Je suis rentré laaaaaaaaaaaaaaaaaaaaaaaaaaaa ");

                }
                else
                {
                    // Existing context found
                    // Assume valid account access granted
                    bankConnector.UserContext = userContext;
                    Console.Write("Je suis rentré ici");
                }

                // Fetch account list and display their balances
                Console.Write("Display");
                await DisplayAccountBalancesAsync(bankConnector);
            }
            finally
            {
                // Always save the user context
                if (bankConnector.UserContextChanged)
                    SaveUserContext(userId, bankConnector.UserContext);
            }
        }

        /// <summary>
        /// Setup for this project
        /// </summary>
        static void Setup()
        {
            // Configure dependency injection
            IServiceCollection services = new ServiceCollection();
            services.AddBankingSdk();

            // Configure BankingSDK general settings
            BankSettings generalBankSettings = new BankSettings
            {

                NcaId = NcaId,
                TlsCertificate = new X509Certificate2(TlsCertificatePath, TlsCertificatePassword),
                SigningCertificate = new X509Certificate2(SigningCertificatePath, SigningCertificatePassword),
                AppClientId= AppClientId,
                AppClientSecret= AppClientSecret
            };
            BankingFactory.SdkSettings.Set(generalBankSettings, BankingSDKCompanyKey, BankingSDKApplicationKey, BankingSDKSecret, BankingSDKTTPName, true);
        }

        /// <summary>
        /// Get UserId
        /// </summary>
        static string GetUserId()
        {
            Console.Write("UserID: ");
            return Console.ReadLine();
        }

        /// <summary>
        /// Fetch the last user context for this user
        /// </summary>
        static string FetchUserContext(string userId)
        {
            string path = Path.Combine(StoreFolderPath, $"{userId}.json");
            if (File.Exists(path))
                return File.ReadAllText(path);
            return null;
        }

        /// <summary>
        /// Save the user context
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userContext"></param>
        static void SaveUserContext(string userId, string userContext)
        {
            string path = Path.Combine(StoreFolderPath, $"{userId}.json");
            File.WriteAllText(path, userContext);
        }

        /// <summary>
        /// Request Account Access
        /// 
        /// Manual process:
        /// You have to go manually to the bankURL, authenticate there and grant the permission to access the account(s)
        /// The bank will then redirect you, you must copy the query string and put it back in the application to continue
        /// 
        /// FlowId:
        /// In this particular case, Crelan ask for a fixed FlowId
        /// Most of the time, you need to provide a unique identifier
        /// </summary>
        static async Task RequestAccountsAccessAsync(IBankingConnector bankConnector)
        {
            // Initialize account access request
            BankingResult<string> bankingResult = await bankConnector.RequestAccountsAccessAsync(new AccountsAccessRequest
            {
                
                FlowId =  "STATE", //Guid.NewGuid().ToString()
                FrequencyPerDay = 2,
                RedirectUrl = RedirectURL,
            });

            if (bankingResult.GetStatus() == ResultStatus.REDIRECT)
            {
                // FlowContext must be reused
                FlowContext flowContext = bankingResult.GetFlowContext();

                // Ask the user to manually go to the redirect URL and enter the result
                string bankURL = bankingResult.GetData();
                Console.WriteLine($"URL: {bankURL}");
                Console.Write("Enter code: ");
                string queryString = Console.ReadLine();
                Console.Write(flowContext);
                // Finalize authentication
                BankingResult<IUserContext> result = await bankConnector.RequestAccountsAccessFinalizeAsync(flowContext, queryString);
                if (result.GetStatus() == ResultStatus.DONE)
                {
                    Console.WriteLine("RequestAccountsAccess succeeded");
                    return;
                }
                
            }

            throw new Exception("RequestAccountsAccess failed");
        }

        /// <summary>
        /// Fetch and display user account balances
        /// </summary>
        static async Task DisplayAccountBalancesAsync(IBankingConnector bankConnector)
        {
            BankingResult<List<Account>> accounts = await bankConnector.GetAccountsAsync();
            if (accounts.GetStatus() == ResultStatus.DONE)
            {
                Console.WriteLine("Listing user accounts...");

                foreach (Account account in accounts.GetData())
                {
                    Console.WriteLine($"Account {account.Iban}");

                    BankingResult<List<Balance>> resultBalances = await bankConnector.GetBalancesAsync(account.Id);
                    if (resultBalances.GetStatus() == ResultStatus.DONE)
                    {
                        List<Balance> accountBalances = resultBalances.GetData();
                        foreach (Balance balance in accountBalances)
                        {
                            Console.WriteLine($"- {balance.BalanceType}: {balance.BalanceAmount.Currency}{balance.BalanceAmount.Amount} ({balance.ReferenceDate:ddMMMyyyy})");
                        }
                    }
                }
            }
        }
    }
}
