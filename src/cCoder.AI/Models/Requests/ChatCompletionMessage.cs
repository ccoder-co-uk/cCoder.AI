namespace cCoder.AI.Models.Requests;

public class ChatCompletionMessage
{
    public ChatCompletionMessage()
    {
    }

    public ChatCompletionMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
