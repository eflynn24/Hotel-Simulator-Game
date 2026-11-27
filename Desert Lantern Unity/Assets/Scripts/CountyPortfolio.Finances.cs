using System;
using System.Collections.Generic;

namespace DesertLantern
{
    [Serializable]
    internal sealed class PropertyTransaction
    {
        public PropertyId property;
        public int minute;
        public Account account;
        public int amount;
        public int balance;
        public string description;
    }

    internal sealed partial class CountyPortfolio
    {
        private const int LedgerLimit = 2048;
        private const int Accounts = (int)Account.Financing + 1;
        private readonly int[] motelTotals = new int[Accounts];
        private int motelLedgerDay;
        private int motelLedgerBalance;
        public IReadOnlyList<PropertyTransaction> Ledger => save.ledger;

        public void UpdateMotel(MotelService state)
        {
            if (state.Property != PropertyId.RoadsideMotel)
                throw new ArgumentException("The roadside motel can only be updated by its own simulation.", nameof(state));
            Motel = state;
            RecordMotelLedger();
        }

        private void CaptureMotelBaseline()
        {
            motelLedgerDay = Motel.ClockMinute / MotelService.MinutesPerDay;
            motelLedgerBalance = Motel.Cash;
            for (int account = 0; account < Accounts; account++)
                motelTotals[account] = Motel.ReportAmount(0, (Account)account);
        }

        private void RecordMotelLedger()
        {
            int day = Motel.ClockMinute / MotelService.MinutesPerDay;
            if (motelLedgerDay != day)
                Array.Clear(motelTotals, 0, motelTotals.Length);
            motelLedgerDay = day;
            for (int account = 0; account < Accounts; account++)
            {
                int total = Motel.ReportAmount(0, (Account)account);
                int amount = total - motelTotals[account];
                motelTotals[account] = total;
                if (amount == 0)
                    continue;
                motelLedgerBalance += amount;
                AddTransaction(Motel.Property, Motel.ClockMinute, (Account)account, amount, motelLedgerBalance, ((Account)account).ToString());
            }

            if (Motel.Cash != motelLedgerBalance)
            {
                int adjustment = Motel.Cash - motelLedgerBalance;
                motelLedgerBalance = Motel.Cash;
                AddTransaction(Motel.Property, Motel.ClockMinute, Account.Adjustments, adjustment, motelLedgerBalance, "Balance reconciliation");
            }
        }

        private void PostHotel(Hotel hotel, Account account, int amount, string description)
        {
            if (amount == 0)
                return;
            hotel.cash += amount;
            switch (account)
            {
                case Account.RoomRevenue:
                case Account.ServiceRevenue:
                    hotel.revenue += amount;
                    break;
                case Account.Payroll:
                    hotel.payroll -= amount;
                    break;
                case Account.Electricity:
                case Account.Water:
                case Account.Internet:
                    hotel.bills -= amount;
                    break;
                case Account.Financing:
                    break;
                default:
                    hotel.expenses -= amount;
                    break;
            }
            AddTransaction(hotel.propertyId, ClockMinute, account, amount, hotel.cash, description);
        }

        private void PostCompany(int amount, string description)
        {
            save.funds += amount;
            AddTransaction(PropertyId.None, ClockMinute, Account.Financing, amount, save.funds, description);
        }

        private void AddTransaction(PropertyId property, int minute, Account account, int amount, int balance, string description)
        {
            if (account != Account.Financing && registry.TryGetValue(property, out Hotel hotel))
                hotel.mastery.profit += amount;
            save.ledger.Add(new PropertyTransaction
            {
                property = property,
                minute = minute,
                account = account,
                amount = amount,
                balance = balance,
                description = description
            });
            if (save.ledger.Count > LedgerLimit)
                save.ledger.RemoveAt(0);
        }

        private void IssueHotelBills(int property)
        {
            Hotel hotel = Business(property);
            IssueInvoice(hotel.propertyId, Account.Electricity, 8 + Offers[property].Rooms / 3);
            IssueInvoice(hotel.propertyId, Account.Water, 5 + Offers[property].Rooms / 2 - Offers[property].Rooms / 3);
            IssueInvoice(hotel.propertyId, Account.Internet, 3);
            if (hotel.propertyId == PropertyId.LuxuryHotel)
                for (int department = 0; department < 6; department++)
                    if (((hotel.luxuryDepartments & ~hotel.pausedDepartments) & (1 << department)) != 0)
                        IssueInvoice(hotel.propertyId, Account.Electricity, 13 + department * 2);
        }
    }
}
