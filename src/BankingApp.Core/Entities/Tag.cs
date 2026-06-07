namespace BankingApp.Core.Entities;

// represents a label that can be attached to transactions for grouping and filtering
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // hex color used to render the badge in the UI — defaults to gray
    public string Color { get; set; } = "#6B7280";
    // many-to-many navigation property — one tag can belong to multiple transactions
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}