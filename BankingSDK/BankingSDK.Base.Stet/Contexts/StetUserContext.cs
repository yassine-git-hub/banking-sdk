﻿using BankingSDK.Common.Interfaces.Contexts;
using BankingSDK.Common.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BankingSDK.Base.Stet.Contexts
{
    public class StetUserContext : IUserContext
    {
        public string UserId { get; set; }
        public List<UserAccessToken> Tokens { get; set; } = new List<UserAccessToken>();
        public List<UserAccount> Accounts { get; set; } = new List<UserAccount>();
        List<BaseUserAccount> IUserContext.Accounts { get => Accounts.Cast<BaseUserAccount>().ToList(); set => Accounts = value.Cast<UserAccount>().ToList(); }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class UserAccessToken
    {
        [JsonIgnore]
        public string FullToken
        {
            get
            {
                return $"{TokenType} {AccessToken}";
            }
        }

        public string TokenType { get; set; }
        public string AccessToken { get; set; }
        public DateTime TokenValidUntil { get; set; }
    }

    public class UserAccount : BaseUserAccount
    {
        public string AccessToken { get; set; }
    }
}
