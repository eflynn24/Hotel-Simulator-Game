using System;
using System.Collections.Generic;

namespace DesertLantern
{
    [Serializable]
    internal sealed class PropertyInvoice
    {
        public PropertyId property;
        public Account account;
        public int amount;
        public int sinceDay;
    }

    internal sealed partial class CountyPortfolio
    {
        public IReadOnlyList<PropertyInvoice> Invoices => save.invoices;

        private void IssueInvoice(PropertyId property, Account account, int amount)
        {
            foreach (PropertyInvoice invoice in save.invoices)
                if (invoice.property == property && invoice.account == account)
                {
                    invoice.amount += amount;
                    return;
                }
            save.invoices.Add(new PropertyInvoice { property = property, account = account, amount = amount, sinceDay = Day });
        }

        public bool PayInvoices(PropertyId property, bool employee = false)
        {
            int slot = MapSlot(property);
            if (slot < 0 || !Owned(slot) || (employee && !HasEmployee(slot, (int)StaffRole.Accountant)))
                return false;
            int paid = 0;
            for (int index = save.invoices.Count - 1; index >= 0; index--)
            {
                PropertyInvoice invoice = save.invoices[index];
                if (invoice.property != property || Cash(slot) < invoice.amount)
                    continue;
                if (property == Motel.Property)
                {
                    if (!Motel.PayUtility(invoice.account, invoice.amount))
                        continue;
                    RecordMotelLedger();
                }
                else
                    PostHotel(Business(slot), invoice.account, -invoice.amount, $"{invoice.account} invoice paid");
                paid += invoice.amount;
                save.invoices.RemoveAt(index);
            }
            if (paid > 0 && employee)
                AddNote(slot, $"Accountant paid ${paid} in routine bills.");
            return paid > 0;
        }

        private void RunAccountingShift()
        {
            foreach (PropertyOffer property in Offers)
                PayInvoices(property.Id, true);
        }
    }
}
