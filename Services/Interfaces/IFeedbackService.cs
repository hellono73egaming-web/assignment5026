using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    public interface IFeedbackService
    {
        Task<ServiceResult> SubmitAsync(string customerId, int reservationId, int rating, string? comment);
        Task<double> GetAverageRatingAsync();
        Task<List<Feedback>> GetForCustomerAsync(string customerId);
    }
}
