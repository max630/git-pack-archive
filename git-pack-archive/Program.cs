using System;

namespace gitpackarchive {
    class MainClass {
        public static void Main(string[] args)
        {
            ReportBusy();
        }

        private static void ReportBusy()
        {
            Console.WriteLine("Status: 503 Lunch");
            Console.WriteLine("Retry-after: 60");
            Console.WriteLine("Content-type: text/html");
            Console.WriteLine("");
            Console.WriteLine(busyDoc);
        }
    
        private static readonly string busyDoc =
@"<html>
<title>Bisy</title>
<body>
Somebody is already dowloading. Please try again later.
</body>
</html>";
    }
}
