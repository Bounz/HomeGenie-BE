using System;

namespace HomeGenie.Service
{
    public interface IDateTime
    {
        DateTime Now { get; }
        DateTime UtcNow { get; }
        DateTime Today { get; }
    }
}