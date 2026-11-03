namespace WebApplication2.Services.Interfaces
{
    // Abstraction for the session-based shopping cart (CS10, CS11).
    public interface ICartService
    {
        List<CartLine> GetLines();
        void Add(int menuItemId, int quantity = 1, string? remarks = null);
        void UpdateQuantity(int menuItemId, int quantity);
        void Remove(int menuItemId);
        void Clear();
        int ItemCount();
    }
}
