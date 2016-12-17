﻿using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text;
using System.Net.Http;
using NadekoBot.Services;
using System.Threading.Tasks;
using NadekoBot.Attributes;
using System.Text.RegularExpressions;
using System.Net;
using NadekoBot.Modules.Searches.Models;
using System.Collections.Generic;
using ImageSharp;
using NadekoBot.Extensions;
using System.IO;
using NadekoBot.Modules.Searches.Commands.OMDB;

namespace NadekoBot.Modules.Searches
{
    [NadekoModule("Searches", "~")]
    public partial class Searches : DiscordModule
    {
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Weather(string city, string country)
        {
            city = city.Replace(" ", "");
            country = city.Replace(" ", "");
            string response;
            using (var http = new HttpClient())
                response = await http.GetStringAsync($"http://api.ninetales.us/nadekobot/weather/?city={city}&country={country}").ConfigureAwait(false);

            var obj = JObject.Parse(response)["weather"];

            var embed = new EmbedBuilder()
                .AddField(fb => fb.WithName("🌍 **Location**").WithValue($"{obj["target"]}").WithIsInline(true))
                .AddField(fb => fb.WithName("📏 **Lat,Long**").WithValue($"{obj["latitude"]}, {obj["longitude"]}").WithIsInline(true))
                .AddField(fb => fb.WithName("☁ **Condition**").WithValue($"{obj["condition"]}").WithIsInline(true))
                .AddField(fb => fb.WithName("😓 **Humidity**").WithValue($"{obj["humidity"]}%").WithIsInline(true))
                .AddField(fb => fb.WithName("💨 **Wind Speed**").WithValue($"{obj["windspeedk"]}km/h ({obj["windspeedm"]}mph)").WithIsInline(true))
                .AddField(fb => fb.WithName("🌡 **Temperature**").WithValue($"{obj["centigrade"]}°C ({obj["fahrenheit"]}°F)").WithIsInline(true))
                .AddField(fb => fb.WithName("🔆 **Feels like**").WithValue($"{obj["feelscentigrade"]}°C ({obj["feelsfahrenheit"]}°F)").WithIsInline(true))
                .AddField(fb => fb.WithName("🌄 **Sunrise**").WithValue($"{obj["sunrise"]}").WithIsInline(true))
                .AddField(fb => fb.WithName("🌇 **Sunset**").WithValue($"{obj["sunset"]}").WithIsInline(true))
                .WithColor(NadekoBot.OkColor);
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Youtube([Remainder] string query = null)
        {
            if (!(await ValidateQuery(Context.Channel, query).ConfigureAwait(false))) return;
            var result = (await NadekoBot.Google.GetVideosByKeywordsAsync(query, 1)).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(result))
            {
                await Context.Channel.SendErrorAsync("No results found for that query.").ConfigureAwait(false);
                return;
            }

            await Context.Channel.SendMessageAsync(result).ConfigureAwait(false);

            //await Context.Channel.EmbedAsync(new Discord.API.Embed() { Video = new Discord.API.EmbedVideo() { Url = result.Replace("watch?v=", "embed/") }, Color = NadekoBot.OkColor }).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Imdb([Remainder] string query = null)
        {
            if (!(await ValidateQuery(Context.Channel, query).ConfigureAwait(false))) return;
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var movie = await OmdbProvider.FindMovie(query);
            if (movie == null)
            {
                await Context.Channel.SendErrorAsync("Failed to find that movie.").ConfigureAwait(false);
                return;
            }
            await Context.Channel.EmbedAsync(movie.GetEmbed()).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task RandomCat()
        {
            using (var http = new HttpClient())
            {
                var res = JObject.Parse(await http.GetStringAsync("http://www.random.cat/meow").ConfigureAwait(false));
                await Context.Channel.SendMessageAsync(res["file"].ToString()).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task RandomDog()
        {
            using (var http = new HttpClient())
            {
                await Context.Channel.SendMessageAsync("http://random.dog/" + await http.GetStringAsync("http://random.dog/woof")
                             .ConfigureAwait(false)).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task I([Remainder] string query = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;
            try
            {
                using (var http = new HttpClient())
                {
                    var reqString = $"https://www.googleapis.com/customsearch/v1?q={Uri.EscapeDataString(query)}&cx=018084019232060951019%3Ahs5piey28-e&num=1&searchType=image&fields=items%2Flink&key={NadekoBot.Credentials.GoogleApiKey}";
                    var obj = JObject.Parse(await http.GetStringAsync(reqString).ConfigureAwait(false));
                    await Context.Channel.SendMessageAsync(obj["items"][0]["link"].ToString()).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException exception)
            {
                if (exception.Message.Contains("403 (Forbidden)"))
                {
                    await Context.Channel.SendErrorAsync("Daily limit reached!");
                }
                else
                {
                    await Context.Channel.SendErrorAsync("Something went wrong.");
                    _log.Error(exception);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Ir([Remainder] string query = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;
            try
            {
                using (var http = new HttpClient())
                {
                    var rng = new NadekoRandom();
                    var reqString = $"https://www.googleapis.com/customsearch/v1?q={Uri.EscapeDataString(query)}&cx=018084019232060951019%3Ahs5piey28-e&num=1&searchType=image&start={ rng.Next(1, 50) }&fields=items%2Flink&key={NadekoBot.Credentials.GoogleApiKey}";
                    var obj = JObject.Parse(await http.GetStringAsync(reqString).ConfigureAwait(false));
                    var items = obj["items"] as JArray;
                    await Context.Channel.SendMessageAsync(items[0]["link"].ToString()).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException exception)
            {
                if (exception.Message.Contains("403 (Forbidden)"))
                {
                    await Context.Channel.SendErrorAsync("Daily limit reached!");
                }
                else
                {
                    await Context.Channel.SendErrorAsync("Something went wrong.");
                    _log.Error(exception);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Lmgtfy([Remainder] string ffs = null)
        {
            if (string.IsNullOrWhiteSpace(ffs))
                return;

            await Context.Channel.SendConfirmAsync(await NadekoBot.Google.ShortenUrl($"<http://lmgtfy.com/?q={ Uri.EscapeUriString(ffs) }>"))
                           .ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Shorten([Remainder] string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return;

            var shortened = await NadekoBot.Google.ShortenUrl(arg).ConfigureAwait(false);

            if (shortened == arg)
            {
                await Context.Channel.SendErrorAsync("Failed to shorten that url.").ConfigureAwait(false);
            }

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(NadekoBot.OkColor)
                                                           .AddField(efb => efb.WithName("Original Url")
                                                                               .WithValue($"<{arg}>"))
                                                            .AddField(efb => efb.WithName("Short Url")
                                                                                .WithValue($"<{shortened}>")))
                                                            .ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Google([Remainder] string terms = null)
        {
            terms = terms?.Trim();
            if (string.IsNullOrWhiteSpace(terms))
                return;

            await Context.Channel.SendConfirmAsync($"https://google.com/search?q={ WebUtility.UrlEncode(terms).Replace(' ', '+') }")
                           .ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task MagicTheGathering([Remainder] string name = null)
        {
            var arg = name;
            if (string.IsNullOrWhiteSpace(arg))
            {
                await Context.Channel.SendErrorAsync("Please enter a card name to search for.").ConfigureAwait(false);
                return;
            }

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            string response = "";
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                response = await http.GetStringAsync($"https://api.deckbrew.com/mtg/cards?name={Uri.EscapeUriString(arg)}")
                                        .ConfigureAwait(false);
                try
                {
                    var items = JArray.Parse(response).Shuffle().ToList();
                    if (items == null)
                        throw new KeyNotFoundException("Cannot find a card by that name");
                    var item = items[0];
                    var storeUrl = await NadekoBot.Google.ShortenUrl(item["store_url"].ToString());
                    var cost = item["cost"].ToString();
                    var desc = item["text"].ToString();
                    var types = String.Join(",\n", item["types"].ToObject<string[]>());
                    var img = item["editions"][0]["image_url"].ToString();
                    var embed = new EmbedBuilder().WithColor(NadekoBot.OkColor)
                                    .WithTitle(item["name"].ToString())
                                    .WithDescription(desc)
                                    .WithImageUrl(img)
                                    .AddField(efb => efb.WithName("Store Url").WithValue(storeUrl).WithIsInline(true))
                                    .AddField(efb => efb.WithName("Cost").WithValue(cost).WithIsInline(true))
                                    .AddField(efb => efb.WithName("Types").WithValue(types).WithIsInline(true));
                                    //.AddField(efb => efb.WithName("Store Url").WithValue(await NadekoBot.Google.ShortenUrl(items[0]["store_url"].ToString())).WithIsInline(true));

                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    await Context.Channel.SendErrorAsync($"Error could not find the card '{arg}'.").ConfigureAwait(false);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Hearthstone([Remainder] string name = null)
        {
            var arg = name;
            if (string.IsNullOrWhiteSpace(arg))
            {
                await Context.Channel.SendErrorAsync("Please enter a card name to search for.").ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(NadekoBot.Credentials.MashapeKey))
            {
                await Context.Channel.SendErrorAsync("Bot owner didn't specify MashapeApiKey. You can't use this functionality.").ConfigureAwait(false);
                return;
            }

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            string response = "";
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                http.DefaultRequestHeaders.Add("X-Mashape-Key", NadekoBot.Credentials.MashapeKey);
                response = await http.GetStringAsync($"https://omgvamp-hearthstone-v1.p.mashape.com/cards/search/{Uri.EscapeUriString(arg)}")
                                        .ConfigureAwait(false);
                try
                {
                    var items = JArray.Parse(response).Shuffle().ToList();
                    var images = new List<ImageSharp.Image>();
                    if (items == null)
                        throw new KeyNotFoundException("Cannot find a card by that name");
                    foreach (var item in items.Where(item => item.HasValues && item["img"] != null).Take(4))
                    {
                        using (var sr = await http.GetStreamAsync(item["img"].ToString()))
                        {
                            var imgStream = new MemoryStream();
                            await sr.CopyToAsync(imgStream);
                            imgStream.Position = 0;
                            images.Add(new ImageSharp.Image(imgStream));
                        }
                    }
                    string msg = null;
                    if (items.Count > 4)
                    {
                        msg = "⚠ Found over 4 images. Showing random 4.";
                    }
                    var ms = new MemoryStream();
                    images.AsEnumerable().Merge().SaveAsPng(ms);
                    ms.Position = 0;
                    await Context.Channel.SendFileAsync(ms, arg + ".png", msg).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await Context.Channel.SendErrorAsync($"Error occured.").ConfigureAwait(false);
                    _log.Error(ex);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Yodify([Remainder] string query = null)
        {
            if (string.IsNullOrWhiteSpace(NadekoBot.Credentials.MashapeKey))
            {
                await Context.Channel.SendErrorAsync("Bot owner didn't specify MashapeApiKey. You can't use this functionality.").ConfigureAwait(false);
                return;
            }

            var arg = query;
            if (string.IsNullOrWhiteSpace(arg))
            {
                await Context.Channel.SendErrorAsync("Please enter a sentence.").ConfigureAwait(false);
                return;
            }
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                http.DefaultRequestHeaders.Add("X-Mashape-Key", NadekoBot.Credentials.MashapeKey);
                http.DefaultRequestHeaders.Add("Accept", "text/plain");
                var res = await http.GetStringAsync($"https://yoda.p.mashape.com/yoda?sentence={Uri.EscapeUriString(arg)}").ConfigureAwait(false);
                try
                {
                    var embed = new EmbedBuilder()
                        .WithUrl("http://www.yodaspeak.co.uk/")
                        .WithAuthor(au => au.WithName("Yoda").WithIconUrl("http://www.yodaspeak.co.uk/yoda-small1.gif"))
                        .WithDescription(res)
                        .WithColor(NadekoBot.OkColor);
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    await Context.Channel.SendErrorAsync("Failed to yodify your sentence.").ConfigureAwait(false);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task UrbanDict([Remainder] string query = null)
        {
            var channel = (ITextChannel)Context.Channel;

            if (string.IsNullOrWhiteSpace(NadekoBot.Credentials.MashapeKey))
            {
                await Context.Channel.SendErrorAsync("Bot owner didn't specify MashapeApiKey. You can't use this functionality.").ConfigureAwait(false);
                return;
            }

            var arg = query;
            if (string.IsNullOrWhiteSpace(arg))
            {
                await Context.Channel.SendErrorAsync("Please enter a search term.").ConfigureAwait(false);
                return;
            }
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                http.DefaultRequestHeaders.Add("Accept", "application/json");
                var res = await http.GetStringAsync($"http://api.urbandictionary.com/v0/define?term={Uri.EscapeUriString(arg)}").ConfigureAwait(false);
                try
                {
                    var items = JObject.Parse(res);
                    var item = items["list"][0];
                    var word = item["word"].ToString();
                    var def = item["definition"].ToString();
                    var link = item["permalink"].ToString();
                    var embed = new EmbedBuilder().WithColor(NadekoBot.OkColor)
                                     .WithUrl(link)
                                     .WithAuthor(eab => eab.WithIconUrl("http://i.imgur.com/nwERwQE.jpg").WithName(word))
                                     .WithDescription(def);
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    await Context.Channel.SendErrorAsync("Failed finding a definition for that term.").ConfigureAwait(false);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Hashtag([Remainder] string query = null)
        {
            var arg = query;
            if (string.IsNullOrWhiteSpace(arg))
            {
                await Context.Channel.SendErrorAsync("Please enter a search term.").ConfigureAwait(false);
                return;
            }
            if (string.IsNullOrWhiteSpace(NadekoBot.Credentials.MashapeKey))
            {
                await Context.Channel.SendErrorAsync("Bot owner didn't specify MashapeApiKey. You can't use this functionality.").ConfigureAwait(false);
                return;
            }

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            string res = "";
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                http.DefaultRequestHeaders.Add("X-Mashape-Key", NadekoBot.Credentials.MashapeKey);
                res = await http.GetStringAsync($"https://tagdef.p.mashape.com/one.{Uri.EscapeUriString(arg)}.json").ConfigureAwait(false);
            }

            try
            {
                var items = JObject.Parse(res);
                var item = items["defs"]["def"];
                var hashtag = item["hashtag"].ToString();
                var link = item["uri"].ToString();
                var desc = item["text"].ToString();
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(NadekoBot.OkColor)
                                                                 .WithAuthor(eab => eab.WithUrl(link)
                                                                                       .WithIconUrl("http://res.cloudinary.com/urbandictionary/image/upload/a_exif,c_fit,h_200,w_200/v1394975045/b8oszuu3tbq7ebyo7vo1.jpg")
                                                                                       .WithName(query))
                                                                 .WithDescription(desc));
            }
            catch
            {
                await Context.Channel.SendErrorAsync("Failed finding a definition for that tag.").ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Catfact()
        {
            using (var http = new HttpClient())
            {
                var response = await http.GetStringAsync("http://catfacts-api.appspot.com/api/facts").ConfigureAwait(false);
                if (response == null)
                    return;

                var fact = JObject.Parse(response)["facts"][0].ToString();
                await Context.Channel.SendConfirmAsync("🐈fact", fact).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Revav([Remainder] IUser usr = null)
        {
            if (usr == null)
                usr = Context.User;
            await Context.Channel.SendConfirmAsync($"https://images.google.com/searchbyimage?image_url={usr.AvatarUrl}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Revimg([Remainder] string imageLink = null)
        {
            imageLink = imageLink?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(imageLink))
                return;
            await Context.Channel.SendConfirmAsync($"https://images.google.com/searchbyimage?image_url={imageLink}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Safebooru([Remainder] string tag = null)
        {
            tag = tag?.Trim() ?? "";
            var link = await GetSafebooruImageLink(tag).ConfigureAwait(false);
            if (link == null)
                await Context.Channel.SendErrorAsync("No results.");
            else
                await Context.Channel.SendMessageAsync(link).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Wiki([Remainder] string query = null)
        {
            query = query?.Trim();
            if (string.IsNullOrWhiteSpace(query))
                return;
            using (var http = new HttpClient())
            {
                var result = await http.GetStringAsync("https://en.wikipedia.org//w/api.php?action=query&format=json&prop=info&redirects=1&formatversion=2&inprop=url&titles=" + Uri.EscapeDataString(query));
                var data = JsonConvert.DeserializeObject<WikipediaApiModel>(result);
                if (data.Query.Pages[0].Missing)
                    await Context.Channel.SendErrorAsync("That page could not be found.");
                else
                    await Context.Channel.SendMessageAsync(data.Query.Pages[0].FullUrl);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Color([Remainder] string color = null)
        {
            color = color?.Trim().Replace("#", "");
            if (string.IsNullOrWhiteSpace((string)color))
                return;
            var img = new ImageSharp.Image(50, 50);

            var red = Convert.ToInt32(color.Substring(0, 2), 16);
            var green = Convert.ToInt32(color.Substring(2, 2), 16);
            var blue = Convert.ToInt32(color.Substring(4, 2), 16);

            img.BackgroundColor(new ImageSharp.Color(color));

            await Context.Channel.SendFileAsync(img.ToStream(), $"{color}.png");
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Videocall([Remainder] params IUser[] users)
        {
            try
            {
                var allUsrs = users.Append(Context.User);
                var allUsrsArray = allUsrs.ToArray();
                var str = allUsrsArray.Aggregate("http://appear.in/", (current, usr) => current + Uri.EscapeUriString(usr.Username[0].ToString()));
                str += new NadekoRandom().Next();
                foreach (var usr in allUsrsArray)
                {
                    await (await (usr as IGuildUser).CreateDMChannelAsync()).SendConfirmAsync(str).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Avatar([Remainder] IUser usr = null)
        {
            if (usr == null)
            {
                await Context.Channel.SendErrorAsync("Invalid user specified.").ConfigureAwait(false);
                return;
            }
            await Context.Channel.SendMessageAsync(await NadekoBot.Google.ShortenUrl(usr.AvatarUrl).ConfigureAwait(false)).ConfigureAwait(false);
        }

        public static async Task<string> GetSafebooruImageLink(string tag)
        {
            var rng = new NadekoRandom();
            var url =
            $"http://safebooru.org/index.php?page=dapi&s=post&q=index&limit=100&tags={tag.Replace(" ", "_")}";
            using (var http = new HttpClient())
            {
                var webpage = await http.GetStringAsync(url).ConfigureAwait(false);
                var matches = Regex.Matches(webpage, "file_url=\"(?<url>.*?)\"");
                if (matches.Count == 0)
                    return null;
                var match = matches[rng.Next(0, matches.Count)];
                return "http:" + matches[rng.Next(0, matches.Count)].Groups["url"].Value;
            }
        }
        
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Wikia(string target, [Remainder] string query = null)
        {
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(query))
            {
                await Context.Channel.SendErrorAsync("Please enter a target wikia, followed by search query.").ConfigureAwait(false);
                return;
            }
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                try
                {
                    var res = await http.GetStringAsync($"http://www.{Uri.EscapeUriString(target)}.wikia.com/api/v1/Search/List?query={Uri.EscapeUriString(query)}&limit=25&minArticleQuality=10&batch=1&namespaces=0%2C14").ConfigureAwait(false);
                    var items = JObject.Parse(res);
                    var found = items["items"][0];
                    var response = $@"`Title:` {found["title"].ToString()}
`Quality:` {found["quality"]}
`URL:` {await NadekoBot.Google.ShortenUrl(found["url"].ToString()).ConfigureAwait(false)}";
                    await Context.Channel.SendMessageAsync(response);
                }
                catch
                {
                    await Context.Channel.SendErrorAsync($"Failed finding `{query}`.").ConfigureAwait(false);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task MCPing([Remainder] string query = null)
        {
            var arg = query;
            if (string.IsNullOrWhiteSpace(arg))
            {
                await Context.Channel.SendErrorAsync("💢 Please enter a `ip:port`.").ConfigureAwait(false);
                return;
            }
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                string ip = arg.Split(':')[0];
                string port = arg.Split(':')[1];
                var res = await http.GetStringAsync($"https://api.minetools.eu/ping/{Uri.EscapeUriString(ip)}/{Uri.EscapeUriString(port)}").ConfigureAwait(false);
                try
                {
                    var items = JObject.Parse(res);
                    var sb = new StringBuilder();
                    int ping = (int)Math.Ceiling(Double.Parse(items["latency"].ToString()));
                    sb.AppendLine($"`Server:` {arg}");
                    sb.AppendLine($"`Version:` {items["version"]["name"].ToString()} / Protocol {items["version"]["protocol"].ToString()}");
                    sb.AppendLine($"`Description:` {items["description"].ToString()}");
                    sb.AppendLine($"`Online Players:` {items["players"]["online"].ToString()}/{items["players"]["max"].ToString()}");
                    sb.Append($"`Latency:` {ping}");
                    await Context.Channel.SendMessageAsync(sb.ToString());
                }
                catch
                {
                    await Context.Channel.SendErrorAsync($"Failed finding `{arg}`.").ConfigureAwait(false);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task MCQ([Remainder] string query = null)
        {
            var arg = query;
            if (string.IsNullOrWhiteSpace(arg))
            {
                await Context.Channel.SendErrorAsync("Please enter `ip:port`.").ConfigureAwait(false);
                return;
            }
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                try
                {
                    string ip = arg.Split(':')[0];
                    string port = arg.Split(':')[1];
                    var res = await http.GetStringAsync($"https://api.minetools.eu/query/{Uri.EscapeUriString(ip)}/{Uri.EscapeUriString(port)}").ConfigureAwait(false);
                    var items = JObject.Parse(res);
                    var sb = new StringBuilder();
                    sb.AppendLine($"`Server:` {arg.ToString()} 〘Status: {items["status"]}〙");
                    sb.AppendLine($"`Player List (First 5):`");
                    foreach (var item in items["Playerlist"].Take(5))
                    {
                        sb.AppendLine($"〔:rosette: {item}〕");
                    }
                    sb.AppendLine($"`Online Players:` {items["Players"]} / {items["MaxPlayers"]}");
                    sb.AppendLine($"`Plugins:` {items["Plugins"]}");
                    sb.Append($"`Version:` {items["Version"]}");
                    await Context.Channel.SendMessageAsync(sb.ToString());
                }
                catch
                {
                    await Context.Channel.SendErrorAsync($"Failed finding server `{arg}`.").ConfigureAwait(false);
                }
            }
        }

        public static async Task<bool> ValidateQuery(IMessageChannel ch, string query)
        {
            if (!string.IsNullOrEmpty(query.Trim())) return true;
            await ch.SendErrorAsync("Please specify search parameters.").ConfigureAwait(false);
            return false;
        }
    }
}
