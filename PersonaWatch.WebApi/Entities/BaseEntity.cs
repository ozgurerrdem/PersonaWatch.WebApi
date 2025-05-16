namespace PersonaWatch.WebApi.Entities
{
    public abstract class BaseEntity
    {
        public string CreatedUserName { get; set; } = "system";
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string? UpdatedUserName { get; set; } = "system";
        public DateTime? UpdatedDate { get; set; } = DateTime.Now;
        public char RecordStatus { get; set; } = 'A';
    }
}
