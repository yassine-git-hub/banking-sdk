﻿using System;
using System.Collections.Generic;
using System.Text;

namespace BankingSDK.BE.Belfius.Models
{
    public class BelfiusAccessData
    {
        public string Token
        {
            get
            {
                return $"{token_type} {access_token}";
            }
        }
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
        public string scope { get; set; }
        public string logical_id { get; set; }
    }
}
