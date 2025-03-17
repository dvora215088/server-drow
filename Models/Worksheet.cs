using System.ComponentModel.DataAnnotations.Schema;

public class Worksheet
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string FileUrl{get;set;}
    public string AgeGroup { get; set; }
    public string Difficulty { get; set; }
    public ICollection<Rating> Ratings { get; set; }
    
     [ForeignKey("CategoryId")]
    public int CategoryId { get; set; }  // הוספת שדה מפתח זר ל-FileCategory
    public Category Category { get; set; }  // קשר אל FileCategory
    public User User{get;set;}
    public int UserId{get;set;}
    
}