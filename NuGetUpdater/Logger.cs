using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGetUpdater
{
    public class Logger : ILogger
    {
        public void LogDebug(string data)
        {
            Console.WriteLine("[DEBUG] {0}", data);
        }

        public void LogError(string data)
        {
            Console.WriteLine("[ERROR] {0}", data);
        }

        public void LogErrorSummary(string data)
        {
            Console.WriteLine("[ERROR] {0}", data);
        }

        public void LogInformation(string data)
        {
            Console.WriteLine("[INFO] {0}", data);
        }

        public void LogInformationSummary(string data)
        {
            Console.WriteLine("[INFO] {0}", data);
        }

        public void LogMinimal(string data)
        {
            Console.WriteLine("[MIN] {0}", data);
        }

        public void LogVerbose(string data)
        {
            Console.WriteLine("[VERBOSE] {0}", data);
        }

        public void LogWarning(string data)
        {
            Console.WriteLine("[WARNING] {0}", data);
        }
    }
}
