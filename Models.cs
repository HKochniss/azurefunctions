
public class SlackActionRequest
{
    public string type { get; set; }
    public Action1[] actions { get; set; }
    public string callback_id { get; set; }
    public Team team { get; set; }
    public Channel channel { get; set; }
    public User user { get; set; }
    public string action_ts { get; set; }
    public string message_ts { get; set; }
    public string attachment_id { get; set; }
    public string token { get; set; }
    public bool is_app_unfurl { get; set; }
    public Original_Message original_message { get; set; }
    public string response_url { get; set; }
    public string trigger_id { get; set; }
}

public class Team
{
    public string id { get; set; }
    public string domain { get; set; }
}

public class Channel
{
    public string id { get; set; }
    public string name { get; set; }
}

public class User
{
    public string id { get; set; }
    public string name { get; set; }
}

public class Original_Message
{
    public string text { get; set; }
    public string username { get; set; }
    public string bot_id { get; set; }
    public Attachment[] attachments { get; set; }
    public string type { get; set; }
    public string subtype { get; set; }
    public string ts { get; set; }
}

public class Attachment
{
    public string callback_id { get; set; }
    public string text { get; set; }
    public int id { get; set; }
    public string color { get; set; }
    public Action[] actions { get; set; }
    public string fallback { get; set; }
}

public class Action
{
    public string id { get; set; }
    public string name { get; set; }
    public string text { get; set; }
    public string type { get; set; }
    public string data_source { get; set; }
}

public class Action1
{
    public string name { get; set; }
    public string type { get; set; }
    public Selected_Options[] selected_options { get; set; }
}

public class Selected_Options
{
    public string value { get; set; }
}
