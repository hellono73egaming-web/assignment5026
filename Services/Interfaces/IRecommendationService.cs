namespace WebApplication2.Services.Interfaces
{
    // Advanced 7: automated dish recommendations (popular dishes + personalised suggestions).
    public interface IRecommendationService
    {
        Task<List<Recommendation>> GetPopularAsync(int take = 4);
        Task<List<Recommendation>> GetForCustomerAsync(string customerId, int take = 4);
    }
}
