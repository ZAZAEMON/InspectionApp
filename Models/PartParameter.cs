namespace InspectionApp.Models
{
    public enum ParameterInputType { Value, Check }

    public class PartType
    {
        public int Id { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public List<PartParameter> Parameters { get; set; } = new();
    }

    public class PartParameter
    {
        public int Id { get; set; }
        public int PartTypeId { get; set; }
        public int SerialNumber { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string SampleBox { get; set; } = string.Empty;
        public ParameterInputType InputType { get; set; }
    }
}
