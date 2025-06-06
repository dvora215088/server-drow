public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string Role { get; set; }

    public ICollection<Worksheet> Worksheets { get; set; }
    public ICollection<FavoriteWorksheet> FavoriteWorksheets { get; set; }
}
