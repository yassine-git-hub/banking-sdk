﻿using BankingSDK.Base.BerlinGroup.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BankingSDK.BE.Puilaetco.Models
{
    internal class PuilaetcoTransactions
    {
        public PuilaetcoAccount account { get; set; }
        public PuilaetcoTransactionsModel transactions { get; set; }
    }

    internal class PuilaetcoTransactionsModel
    {
        public List<BerlinGroupTransactionDto> booked { get; set; } = new List<BerlinGroupTransactionDto>();
        public List<BerlinGroupTransactionDto> pending { get; set; } = new List<BerlinGroupTransactionDto>();
        public PuilaetcoLinksDto _links { get; set; }
        
        public uint PageTotal
        {
            get
            {
                if (string.IsNullOrEmpty(_links?.last))
                {
                    return 1;
                }

                return uint.Parse(HttpUtility.ParseQueryString((new Uri(_links.last)).Query).Get("page"));
            }
        }

        public List<BerlinGroupTransactionDto> all
        {
            get
            {
                var temp = new List<BerlinGroupTransactionDto>();
                temp.AddRange(booked);
                temp.AddRange(pending);
                return temp.OrderByDescending(x => x.valueDate).ToList();
            }
        }
    }

    internal class PuilaetcoLinksDto
    {
        public string last { get; set; }
    }
}
