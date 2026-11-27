using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private int financeTab;
        private PropertyId ledgerProperty;
        private Vector2 ledgerScroll;

        private void DrawPortfolioFinances(Rect viewport, float scale)
        {
            float split = viewport.width / 3;
            string[] tabs = { "Updates", "Ledger", "Invoices" };
            for (int tab = 0; tab < tabs.Length; tab++)
                if (DrawButton(new Rect(viewport.x + split * tab, viewport.y, split - 3 * scale, 28 * scale), tabs[tab]))
                    financeTab = tab;
            viewport.y += 34 * scale;
            viewport.height -= 34 * scale;
            if (financeTab == 0)
            {
                DrawPortfolioInbox(viewport, scale);
                return;
            }
            if (financeTab == 2)
            {
                DrawInvoices(viewport, scale);
                return;
            }
            string name = ledgerProperty == PropertyId.None ? "All properties" : CountyPortfolio.PropertyName(ledgerProperty);
            if (DrawButton(new Rect(viewport.x, viewport.y, viewport.width, 26 * scale), name + " · Change filter"))
            {
                int next = CountyPortfolio.MapSlot(ledgerProperty) + 1;
                while (next < CountyPortfolio.Offers.Length && !Campaign.Owned(next))
                    next++;
                ledgerProperty = next == CountyPortfolio.Offers.Length ? PropertyId.None : CountyPortfolio.Offers[next].Id;
                ledgerScroll = Vector2.zero;
            }
            viewport.y += 31 * scale;
            viewport.height -= 31 * scale;
            var ledger = Campaign.Ledger;
            int rows = 0;
            foreach (PropertyTransaction transaction in ledger)
                if (ledgerProperty == PropertyId.None || ledgerProperty == transaction.property)
                    rows++;
            if (rows == 0)
            {
                GUI.Label(viewport, "New transactions appear here as they occur.", countyBodyStyle ?? label);
                return;
            }
            float width = viewport.width - 20 * scale;
            ledgerScroll = GUI.BeginScrollView(viewport, ledgerScroll, new Rect(0, 0, width, rows * 46 * scale));
            int row = 0;
            for (int index = ledger.Count - 1; index >= 0; index--)
            {
                PropertyTransaction transaction = ledger[index];
                if (ledgerProperty != PropertyId.None && ledgerProperty != transaction.property)
                    continue;
                float y = row++ * 46 * scale;
                if (y + 46 * scale < ledgerScroll.y || y > ledgerScroll.y + viewport.height)
                    continue;
                GUI.Label(new Rect(0, y, width, 23 * scale),
                    $"{CountyPortfolio.PropertyName(transaction.property)} · {transaction.description}", detail);
                GUI.Label(new Rect(0, y + 22 * scale, width, 22 * scale),
                    $"{MotelService.DisplayTime(transaction.minute)} · {transaction.amount:+$#,0;−$#,0;$0} · Balance ${transaction.balance:N0}",
                    countyBodyStyle ?? label);
            }
            GUI.EndScrollView();
        }
        private Vector2 invoiceScroll;

        private void DrawInvoices(Rect viewport, float scale)
        {
            var invoices = Campaign.Invoices;
            if (invoices.Count == 0)
            {
                GUI.Label(viewport, "All property invoices are paid.", countyBodyStyle ?? label);
                return;
            }
            float width = viewport.width - 20 * scale;
            invoiceScroll = GUI.BeginScrollView(viewport, invoiceScroll,
                new Rect(0, 0, width, CountyPortfolio.Offers.Length * 57 * scale));
            int row = 0;
            foreach (var offer in CountyPortfolio.Offers)
            {
                int total = 0;
                int oldestDay = int.MaxValue;
                foreach (PropertyInvoice invoice in invoices)
                    if (invoice.property == offer.Id)
                    {
                        total += invoice.amount;
                        oldestDay = Mathf.Min(oldestDay, invoice.sinceDay);
                    }
                if (total == 0)
                    continue;
                float y = row++ * 57 * scale;
                GUI.Label(new Rect(0, y, width - 120 * scale, 28 * scale), offer.Name, detail);
                GUI.Label(new Rect(0, y + 23 * scale, width - 120 * scale, 25 * scale),
                    $"Utility bills ${total:N0} · Since day {oldestDay + 1}", countyBodyStyle ?? label);
                if (DrawButton(new Rect(width - 117 * scale, y + 7 * scale, 112 * scale, 31 * scale), "Pay invoices") &&
                    Campaign.PayInvoices(offer.Id))
                    Campaign.Save();
            }
            GUI.EndScrollView();
        }
    }
}
