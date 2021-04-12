using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ArgosMonitor.Models;
using Discord;
using Discord.Webhook;

namespace ArgosMonitor.Functions
{
    class Discord
    {
        public static async void NotifyDiscordAsync(MonitorTask monitorTask, List<string> availability)
        {
            try
            {
                if (!CheckIfNotifyAllowed(monitorTask.product.productSku))
                {
                    OutputToFile.WriteLine("\nAlready notified for this restock on one postcode! Reducing spam!");
                    return;
                }
            }
            catch (Exception e)
            {
                OutputToFile.WriteLine(e.ToString());
            }


            var embed = new EmbedBuilder();

            var embeds = new List<Embed>();

            var message = "";

            for (var i = 0; i < availability.Count; i++)
            {
                message += availability[i] + "\n";

                if (i > 5) break;
            }

            if (availability.Count > 5)
            {
                message += $"\nPlus {availability.Count - 5} other stores in the area!";
            }

            message += $"\n**Make sure to check your postcode too!**";

            embeds.Add(embed
                .WithAuthor("New stock found on Argos!")
                .WithFooter("RYXX Monitors | @ryxxuk")
                .WithColor(Color.Blue)
                .WithTitle(monitorTask.product.itemName)
                .WithFields(new EmbedFieldBuilder
                {
                    Name = "Postcode Checked:",
                    Value = monitorTask.postcode
                })
                .WithFields(new EmbedFieldBuilder
                {
                    Name = "Available at:",
                    Value = message
                })
                .WithCurrentTimestamp()
                .WithThumbnailUrl(
                    $"https://media.4rgos.it/s/Argos/{monitorTask.product.productSku}_R_SET?$Main768$&amp;w=620&amp;h=620")
                .WithUrl("https://www.argos.co.uk/product/" + monitorTask.product.productSku)
                .Build());


            foreach (var client in monitorTask.webhooks.Select(webhook => new DiscordWebhookClient(webhook)))
            {
                await client.SendMessageAsync("", false, embeds: embeds);
            }
        }

        private static bool CheckIfNotifyAllowed(string productProductSku)
        {
            switch (productProductSku)
            {
                case "8349024":
                    return true;
                case "8349000":
                    return true;
            }



            if (Program.LastNotified.ContainsKey(productProductSku))
            {
                var allowed = Program.LastNotified[productProductSku].AddMinutes(1) < DateTime.Now;

                if (allowed)
                {
                    Program.LastNotified[productProductSku] = DateTime.Now;
                }

                return allowed;
            }
            
            Program.LastNotified.Add(productProductSku, DateTime.Now);

            return true;
        }
    }
}
