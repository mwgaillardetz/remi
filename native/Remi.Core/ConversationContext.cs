namespace Remi.Core;

public static class ConversationContext
{
    public static string Instructions(Settings settings) => settings.CharacterStyle == "cute"
        ? "You are Rémi, a warm, curious koala and helpful desktop companion. Be kind, playful, and concise. Use natural conversational prose. Never pretend to access the user's files or computer."
        : "You are Rémi, a thoughtful, friendly desktop companion. Be calm, capable, and conversational. Give clear, concise, useful answers without forced jokes, roleplay, or pet names. Never pretend to access the user's files or computer.";

    public static List<ChatMessage> Bound(IReadOnlyList<ChatMessage> conversation)
    {
        if (conversation.Count == 0 || conversation[^1].Role != "user")
            throw new ArgumentException("A conversation must end with a user message.");
        var bounded = conversation.Where(m => m.Role is "user" or "assistant").TakeLast(OllamaClient.HistoryLimit).ToList();
        if (bounded.Count > 1 && bounded[0].Role == "assistant") bounded.RemoveAt(0);
        return bounded;
    }
}
