using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using BankingSDK;
using BankingSDK.Base;
using BankingSDK.BE.KBC;
using BankingSDK.BE.ING;
using BankingSDK.Common;
using BankingSDK.Common.Enums;
using BankingSDK.Common.Models;
using BankingSDK.Common.Models.Request;
using Microsoft.Extensions.DependencyInjection;
using BankingSDK.Common.Interfaces.Contexts;
using System.Collections.Generic;
using BankingSDK.Common.Models.Data;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddBankingSdk();

            // Setting general settings of BankingSDk
            BankSettings generalBankSettings = new BankSettings();
            generalBankSettings.NcaId = "VALID_NCA_ID";
            string filePath = @"C:\Users\Yassine\Documents\StageEntrainement\BankingSDK - Copie\BankingSDK\Test\TestApp\example_eidas_client_tls.cer";
            string filePath2 = @"C:\Users\Yassine\Documents\StageEntrainement\BankingSDK - Copie\BankingSDK\Test\TestApp\example_eidas_client_signing.cer";

            generalBankSettings.TlsCertificate = new X509Certificate2(filePath, "banking");
            generalBankSettings.SigningCertificate = new X509Certificate2(filePath2, "banking");


            SdkApiSettings.CompanyKey = "1df53a2d-26aa-4ef1-9f3c-09a1092e5413";
            SdkApiSettings.ApplicationKey = "d7726cbf-e51f-4e0e-a3bb-132913ad8032";
            SdkApiSettings.Secret = "W2ANVUVRQS7AKBFCZLKHGLWXCC6FRUGXAV3O3E33H7D1ZULX6ME6UKOKGNKSXPA6LK9YH1JUSR9JI58S0JIEPKCZHSIQ4KF27GPOUWPFDTODKECLP11OG5RBHJRZT20HMHLBPT7D4DB559PCP6BCS1VBZOHKF0VK3G1D2B564XUD4VFH4OJW1YZVVKLBLW8ER0CYIMKOBEH4GSGEI9Q1Q9SERGRSZ3UHZQVDTR9SD4UWQ4ODDH3MGGXSI7GC42ZH";
            SdkApiSettings.TppLegalName = "EXTHAND";
            SdkApiSettings.IsSandbox = false;

            
            //BankingSDK. bankConnector = new Connector(generalBankSettings);
            BankingSDK.BE.KBC.BeKbcConnector bankConnector = new BeKbcConnector(generalBankSettings);

            string userId = Guid.NewGuid().ToString();
            string userContect =  ( await bankConnector.RegisterUserAsync(userId)).GetData().ToJson();

            string callBackUrl = "https://developer.bankingsdk.com/callback";

            AccountsAccessRequest accountsAccessRequest = new AccountsAccessRequest {
                FlowId = SdkApiSettings.ApplicationKey,
                FrequencyPerDay = 4,
                RedirectUrl = callBackUrl,
                PsuIp ="10.10.10.10", 
                SingleAccount= "BE91732047678076"
            };

            BankingResult<string> bankingResult = await bankConnector.RequestAccountsAccessAsync(accountsAccessRequest);

            if (bankingResult.GetStatus() == ResultStatus.REDIRECT)
            {
                // We get the flow context.
                var flowContext = bankingResult.GetFlowContext();
                string redirectUrlOnTheBank = bankingResult.GetData();

                var psi = new ProcessStartInfo(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe");
                psi.Arguments = redirectUrlOnTheBank;
                Process.Start(psi);


                Console.WriteLine("QueryString received?");
                string queryString = Console.ReadLine();

                BankingResult<IUserContext> result = await bankConnector.RequestAccountsAccessFinalizeAsync(flowContext,queryString);

                if(result.GetStatus() == ResultStatus.DONE)
                {
                    Console.WriteLine("Ok. Cool.");

                    BankingResult<List<Account>> accounts = await bankConnector.GetAccountsAsync();
                    foreach(Account account in accounts.GetData())
                    {
                        Console.WriteLine("Account " + account.Iban);

                        BankingResult<List<Balance>> resultBalances = await bankConnector.GetBalancesAsync(account.Iban);
                        if (resultBalances.GetStatus() == ResultStatus.DONE)
                        {
                            List<Balance> accountBalances = resultBalances.GetData();
                            foreach(Balance balance in accountBalances)
                            {
                                Console.WriteLine("  Balance : " + balance.BalanceAmount + " " + balance.ReferenceDate?.ToString("dd MMM yyyy"));
                            }
                        }
                    }
                }

            }
            Console.ReadLine();
            return;
        }
    }
}
