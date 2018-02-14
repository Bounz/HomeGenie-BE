using System;

namespace HomeGenie.Service.Logging
{
    public class StatisticsDbEntry
    {
        public Guid Id { get; set; }
        public DateTime TimeStart { get; set; }
        public DateTime TimeEnd { get; set; }
        public string Domain { get; set; }
        public string Address { get; set; }
        public string Parameter { get; set; }
        public double AvgValue { get; set; }
        public string ModuleName { get; set; }
    }
}
