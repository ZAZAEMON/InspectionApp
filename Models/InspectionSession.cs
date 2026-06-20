namespace InspectionApp.Models
{
    public class InspectionSession
    {
        public int Id { get; set; }
        public string PartNumber { get; set; } = "";
        public string Shift { get; set; } = "";
        public string Auditor { get; set; } = "";
        public DateTime SubmittedAt { get; set; }
        public List<InspectionReading> Readings { get; set; } = new();
    }

    public class InspectionReading
    {
        public int SerialNumber { get; set; }
        public string ParameterName { get; set; } = "";
        public string Reading { get; set; } = "";
        public string InputType { get; set; } = "";
    }
}
