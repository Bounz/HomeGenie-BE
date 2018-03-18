namespace HomeGenie.Service.Logging
{
    public class StatGraphEntry
    {
        /// <summary>
        /// UNIX milliseconds (UTC) timestamp
        /// </summary>
        public double Timestamp { get; set; }

        public double Value { get; set; }
    }
}