﻿using System;
using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace flamingo_contract_staking
{
    partial class FLM
    {
        [DisplayName("Transfer")]
        public static event Action<UInt160, UInt160, BigInteger> OnTransfer;

        [DisplayName("Approval")]
        public static event Action<UInt160, UInt160, BigInteger> OnApprove;

        [Safe]
        public static BigInteger TotalSupply() => TotalSupplyStorage.Get();

        [Safe]
        public static BigInteger BalanceOf(UInt160 usr)
        {
            Assert(CheckAddrValid(true, usr), "BalanceOf: invalid usr, usr-".ToByteArray().Concat(usr).ToByteString());
            return BalanceStorage.Get(usr);
        }

        [Safe]
        public static BigInteger Allowance(UInt160 usr, UInt160 spender)
        {
            Assert(CheckAddrValid(true, usr, spender), "Allowance: invalid usr or spender, usr-".ToByteArray().Concat(usr).Concat("and spender-".ToByteArray()).Concat(spender).ToByteString());
            return AllowanceStorage.Get(usr, spender);
        }

        public static bool Approve(UInt160 usr, UInt160 spender, BigInteger amount)
        {
            Assert(CheckAddrValid(true, usr, spender), "approve: invalid usr or spender, usr-".ToByteArray().Concat(usr).Concat("and spender-".ToByteArray()).Concat(spender).ToByteString());
            Assert(Runtime.CheckWitness(usr) || usr.Equals(Runtime.CallingScriptHash), "approve: CheckWitness failed, usr-".ToByteArray().Concat(usr).ToByteString());
            if (spender.Equals(usr)) return true;
            if(amount >= 0)
            {
                AllowanceStorage.Increase(usr, spender, amount);
            }
            else
            {
                AllowanceStorage.Decrease(usr, spender, -amount);
            }
            OnApprove(usr, spender, amount);
            return true;
        }

        public static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            Assert(CheckAddrValid(true, from, to), "transfer: invalid from or to, owner-".ToByteArray().Concat(from).Concat("and to-".ToByteArray()).Concat(to).ToByteString());
            Assert(Runtime.CheckWitness(from) || from.Equals(Runtime.CallingScriptHash), "transfer: CheckWitness failed, from-".ToByteArray().Concat(from).ToByteString());
            return TransferInternal(from, from, to, amount, data);
        }

        public static bool TransferFrom(UInt160 spender, UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            Assert(CheckAddrValid(true, spender, from, to), "transferFrom: invalid spender or from or to, spender-".ToByteArray().Concat(spender).Concat("and from-".ToByteArray()).Concat(from).Concat("and to-".ToByteArray()).Concat(to).ToByteString());
            Assert(Runtime.CheckWitness(spender) || from.Equals(Runtime.CallingScriptHash), "transfer: CheckWitness failed, from-".ToByteArray().Concat(from).ToByteString());
            return TransferInternal(spender, from, to, amount, data);
        }

        private static bool TransferInternal(UInt160 spender, UInt160 from, UInt160 to, BigInteger amount, object data = null)
        {
            Assert(amount >= 0, "transferInternal: invalid amount-".ToByteArray().Concat(amount.ToByteArray()).ToByteString());

            bool result = true;
            if (spender != from)
            {
                result = AllowanceStorage.Decrease(from, spender, amount);
                Assert(result, "transferInternal:invalid allowance-".ToByteArray().Concat(amount.ToByteArray()).ToByteString());
            }
            if (from != UInt160.Zero && amount != 0)
            {
                result = BalanceStorage.Reduce(from, amount);
                Assert(result, "transferInternal:invalid balance-".ToByteArray().Concat(amount.ToByteArray()).ToByteString());
            }
            else if (from == UInt160.Zero)
            { 
                TotalSupplyStorage.Increase(amount);
            }
            if (to != UInt160.Zero && amount != 0)
            {
                BalanceStorage.Increase(to, amount);
            }
            else if (to == UInt160.Zero)
            {
                TotalSupplyStorage.Reduce(amount);
            }

            // Validate payable
            if (ContractManagement.GetContract(to) != null)
                Contract.Call(to, "onNEP17Payment", CallFlags.All, new object[] { from, amount, data });

            if (result)
            {
                OnTransfer(from, to, amount);
            }
            return result;
        }
    }
}
