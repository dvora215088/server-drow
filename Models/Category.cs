public class Category
{
    public int Id { get; set; }
    public string CategoryName { get; set; }
    public string Description { get; set; }
    
    // אוסף של Worksheets הקשורים לקטגוריה זו
    public ICollection<Worksheet> Worksheets { get; set; }
}