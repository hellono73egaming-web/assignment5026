using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services { 
    public class FeedbackService : IFeedbackService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        public FeedbackService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public async Task<ServiceResult> SubmitAsync(string customerId, int reservationId, int rating, string? comment)
        {
            if (rating < 1 || rating > 5)
            {
                return ServiceResult.Fail("Rating must be between 1 and 5.");
            }

            if (comment?.Length > 1000)
            {
                return ServiceResult.Fail("Comment must be less than 1000 characters.");
            }

            var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId && r.CustomerId == customerId);

            if (reservation == null)
            {
                return ServiceResult.Fail("No reservation found.");
            }

            if (reservation.Status != ReservationStatus.Completed)
                
            {
                return ServiceResult.Fail("Feedback can only be submitted for completed reservations.");
            }
            bool Rated = await _db.Feedbacks.AnyAsync(f => f.ReservationId == reservationId);
            if (Rated)
            {
                return ServiceResult.Fail("You have already rated this visit.");
            }

            _db.Feedbacks.Add(new Feedback
            {
                CustomerId = customerId,
                ReservationId = reservationId,
                Rating = rating,
                Comment = comment
            });

            await _db.SaveChangesAsync();

            return ServiceResult.Ok();
        }

        public async Task<double> GetAverageRatingAsync()
        {
            if (!await _db.Feedbacks.AnyAsync())
            {
                return 0;
            }
            return await _db.Feedbacks.AverageAsync(f => f.Rating);
        }

        // CS17 - the customer's own reviews, newest first
        public Task<List<Feedback>> GetForCustomerAsync(string customerId) =>
            _db.Feedbacks
               .Where(f => f.CustomerId == customerId)
               .OrderByDescending(f => f.CreatedAt)
               .ToListAsync();

        // Review management - every review with who wrote it and which visit, newest first
        public Task<List<Feedback>> GetAllAsync() =>
            _db.Feedbacks
               .Include(f => f.Customer)
               .Include(f => f.Reservation).ThenInclude(r => r!.RestaurantTable)
               .OrderByDescending(f => f.CreatedAt)
               .ToListAsync();

        // Admin replies to a review; the customer gets a notification
        public async Task<ServiceResult> ReplyAsync(int feedbackId, string reply)
        {
            reply = reply?.Trim() ?? string.Empty;
            if (reply.Length < 2 || reply.Length > 500)
            {
                return ServiceResult.Fail("Reply must be between 2 and 500 characters.");
            }

            var feedback = await _db.Feedbacks.FirstOrDefaultAsync(f => f.Id == feedbackId);
            if (feedback == null)
            {
                return ServiceResult.Fail("Review not found.");
            }

            feedback.AdminReply = reply;
            feedback.RepliedAt = DateTime.Now;

            _notifications.Queue(feedback.CustomerId, "We replied to your review",
                $"Thank you for your feedback. Our reply: \"{(reply.Length > 120 ? reply[..120] + "..." : reply)}\"",
                NotificationType.General, "/CustomerPanel/Feedback");

            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }
    }
}