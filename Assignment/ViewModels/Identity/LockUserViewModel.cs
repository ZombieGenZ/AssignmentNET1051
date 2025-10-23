namespace Assignment.ViewModels.Identity
{
    public class LockUserViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public int DurationValue { get; set; }

        public string DurationUnit { get; set; } = "minute";

        public bool Unlock { get; set; }

        public bool IsPermanent { get; set; }
    }
}
