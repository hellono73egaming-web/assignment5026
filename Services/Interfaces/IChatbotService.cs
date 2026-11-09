namespace WebApplication2.Services.Interfaces
{
    // Advanced 6: chatbot that helps customers with reservations, menu questions and FAQs.
    // Behind an interface so the rule-based engine could later be swapped for an AI model
    // without changing the page or the /api/chat endpoint.
    public interface IChatbotService
    {
        Task<ChatReply> ReplyAsync(string message, string? customerId);
    }
}
