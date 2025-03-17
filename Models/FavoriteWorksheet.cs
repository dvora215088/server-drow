
public class FavoriteWorksheet
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int WorksheetId { get; set; }
    public User User { get; set; }
    public Worksheet Worksheet { get; set; }
}