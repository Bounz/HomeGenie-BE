using System;
using System.IO;
using System.Text;

namespace HomeGenie.Service
{
    public class ConsoleRedirect : TextWriter
    {
        private string _lineBuffer = "";

        public Action<string> ProcessOutput;

        public override void Write(string message)
        {
            var newLine = new string(CoreNewLine);
            if (message.IndexOf(newLine) >= 0)
            {
                var parts = message.Split(CoreNewLine);
                if (message.StartsWith(newLine))
                    WriteLine(_lineBuffer);
                else
                    parts[0] = _lineBuffer + parts[0];
                _lineBuffer = "";
                if (parts.Length > 1 && !parts[parts.Length - 1].EndsWith(newLine))
                {
                    _lineBuffer += parts[parts.Length - 1];
                    parts[parts.Length - 1] = "";
                }
                foreach (var s in parts)
                {
                    if (!String.IsNullOrWhiteSpace(s))
                        WriteLine(s);
                }
                message = "";
            }
            _lineBuffer += message;
        }
        public override void WriteLine(string message)
        {
            if (ProcessOutput != null && !string.IsNullOrWhiteSpace(message))
            {
                // log entire line into the "Domain" column
                //SystemLogger.Instance.WriteToLog(new HomeGenie.Data.LogEntry() {
                //    Domain = "# " + this.lineBuffer + message
                //});
                ProcessOutput(_lineBuffer + message);
            }
            _lineBuffer = "";
        }

        public override Encoding Encoding
        {
            get
            {
                return Encoding.UTF8;
            }
        }

    }
}
