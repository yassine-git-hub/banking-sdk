﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BankingSDK.BE.Belfius;
using BankingSDK.Common.Enums;
using BankingSDK.Common.Models.Data;
using BankingSDK.Common.Models.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TestWebApp.Models;

namespace TestWebApp.Controllers
{
    public class BeBelfiusController : BaseController
    {
        public BeBelfiusController(ILogger<HomeController> logger, IConfiguration configuration) : base(logger,
            configuration)
        {
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RequestAccountAccess(string account)
        {
            BeBelfiusConnector connector = new BeBelfiusConnector(Storage.BankSettings);
            string userId = Guid.NewGuid().ToString();
            Console.Write("User id : " + userId);
            string userContext = (await connector.RegisterUserAsync(userId)).GetData().ToJson();
            Console.Write("userContext : " + userContext);
            Console.Write("account : " + account);


            Storage.Connector = connector;

            Storage.FinalizeUrl = Url.Action("RequestAccountAccessFinalize");
            Console.Write("FinalizeUrl  : " + Storage.FinalizeUrl);

            var result = await connector.RequestAccountsAccessAsync(new AccountsAccessRequest
            {
                FlowId = "BELFIUS",
                SingleAccount = account,
                FrequencyPerDay = 2,
                PsuIp = "10.10.10.10",
                RedirectUrl = Storage.CallbackUrl
            });
            Console.Write("finlaise : ");


            if (result.GetStatus() == ResultStatus.REDIRECT)
            {
                Storage.Flow = result.GetFlowContext();
                Storage.SingleAccount = account;
                Storage.SCAUrl = result.GetData();
            }
            else
            {
                Storage.SCAUrl = "";
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> RequestAccountAccessFinalize()
        {
            var connector = (BeBelfiusConnector)Storage.Connector;

            var result = await connector.RequestAccountsAccessFinalizeAsync(Storage.Flow, HttpContext.Request.QueryString.ToString());

            return Redirect(Url.Action("ListAccounts"));
        }

        [HttpGet]
        public async Task<IActionResult> ListAccounts()
        {
            var result = await ((BeBelfiusConnector) Storage.Connector).GetAccountsAsync();
            AccountListViewModel model = new AccountListViewModel(); 
            if (result.GetStatus() == ResultStatus.DONE)
            {
                model.Accounts = result.GetData();
            }
            
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ListTransactions(string accountId)
        {
            var result = await ((BeBelfiusConnector) Storage.Connector).GetTransactionsAsync(accountId);
            TransactionListViewModel model = new TransactionListViewModel();
            model.AccountId = accountId;
            if (result.GetStatus() == ResultStatus.DONE)
            {
                model.Transactions = result.GetData();
            }
            
            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }
    }
}