namespace hypixel
{
    public class AlternativeName
    {
        public int Id { get; set; }

        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        public string Name { get; set; }
        public int DBItemId { get; set; }

        public static implicit operator string(AlternativeName name) => name.Name;

    }

}