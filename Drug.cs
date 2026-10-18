namespace HospitalApi.Models
{
    // A prescribing formulary/catalog entry (not stock inventory).
    public class Drug
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Form { get; set; }       // e.g. tablet, syrup, injection
        public string? Strength { get; set; }   // e.g. 500mg
        public bool IsActive { get; set; } = true;
    }
}
