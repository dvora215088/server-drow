public class Rating
{
    public int Id { get; set; }
    public int RatingValue { get; set; }
    public int WorksheetId { get; set; }

    public Worksheet Worksheet { get; set; }
        public int UserId { get; set; }

    public User User { get; set; }
}