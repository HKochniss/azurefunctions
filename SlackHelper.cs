
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public static class SlackHelper
{
    public static async Task SendSlackNotification(string hookUrl,
        string text,
        string title = null,
        string author = null,
        string imageUrl = null,
        string footer = null,
        string color = SlackColor.Default,
        IEnumerable<SlackMessageAttachmentAction> actions = null)
    {
        SlackMessage message = new SlackMessage()
        {
            Attachments = new List<SlackMessageAttachment>()
                {
                    new SlackMessageAttachment()
                    {
                        Color = color,
                        Title = title,
                        Text = text,
                        ImageUrl = imageUrl,
                        AuthorName = author,
                        Footer = footer
                    }
                }
        };

        message.Attachments[0].Actions = actions;

        await SendSlackNotification(message, hookUrl);
    }

    public static async Task<HttpResponseMessage> SendSlackNotification(SlackMessage message, string hookUrl)
    {
        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage result = await client.PostAsync(hookUrl, new StringContent(JsonConvert.SerializeObject(message), Encoding.UTF8, "application/json"));
            return result;
        }
    }

    public static class SlackColor
    {
        public const string Error = "#ab2524";
        public const string Success = "#5CB85C";
        public const string Warning = "#F0AD4E";
        public const string Default = "#009688";
    }
}


public class SlackMessage
{
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string Text { get; set; }
    [JsonProperty("username", NullValueHandling = NullValueHandling.Ignore)]
    public string Username { get; set; }
    [JsonProperty("icon_url", NullValueHandling = NullValueHandling.Ignore)]
    public string IconUrl { get; set; }
    [JsonProperty("attachments", NullValueHandling = NullValueHandling.Ignore)]
    public List<SlackMessageAttachment> Attachments { get; set; }

}

public class SlackMessageAttachment
{
    [JsonProperty("fallback", NullValueHandling = NullValueHandling.Ignore)]
    public string Fallback { get; set; }
    [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
    public string Color { get; set; }
    [JsonProperty("pretext", NullValueHandling = NullValueHandling.Ignore)]
    public string Pretext { get; set; }
    [JsonProperty("actions", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<SlackMessageAttachmentAction> Actions { get; set; }
    [JsonProperty("author_name", NullValueHandling = NullValueHandling.Ignore)]
    public string AuthorName { get; set; }
    [JsonProperty("author_link", NullValueHandling = NullValueHandling.Ignore)]
    public string AuthorLink { get; set; }
    [JsonProperty("authoricon", NullValueHandling = NullValueHandling.Ignore)]
    public string AuthorIcon { get; set; }
    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string Title { get; set; }
    [JsonProperty("title_link", NullValueHandling = NullValueHandling.Ignore)]
    public string TitleLink { get; set; }
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string Text { get; set; }
    [JsonProperty("fields", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<SlackMessageAttachmentField> Fields { get; set; }
    [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
    public string ImageUrl { get; set; }
    [JsonProperty("thumb_url", NullValueHandling = NullValueHandling.Ignore)]
    public string ThumbUrl { get; set; }
    [JsonProperty("footer", NullValueHandling = NullValueHandling.Ignore)]
    public string Footer { get; set; }
    [JsonProperty("footer_icon", NullValueHandling = NullValueHandling.Ignore)]
    public string FooterIcon { get; set; }
    [JsonProperty("ts", NullValueHandling = NullValueHandling.Ignore)]
    public int Ts { get; set; }
}


public class SlackMessageAttachmentAction
{
    [JsonProperty("type")]
    public string Type { get; set; }
    [JsonProperty("name")]
    public string Name { get; set; }
    [JsonProperty("text")]
    public string Text { get; set; }
    [JsonProperty("url")]
    public string Url { get; set; }
    [JsonProperty("style")]
    public string Style { get; set; }
}



public class SlackMessageAttachmentField
{
    [JsonProperty("title")]
    public string Title { get; set; }
    [JsonProperty("value")]
    public string Value { get; set; }
    [JsonProperty("short")]
    public bool Short { get; set; }
}

