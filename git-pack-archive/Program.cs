using System;
using System.IO;
using System.Text.RegularExpressions;

namespace gitpackarchive {
    class MainClass {
        private static bool HeaderSent = false;

        public static void Main(string[] args)
        {
            var Out = new BinaryWriter(Console.OpenStandardOutput());
            try {
                DoJob(Out);
            } catch (Exception e) {
                Console.Error.WriteLine("Error: {0}", e);
                if(HeaderSent) {
                    return;
                } else {
                    Out.Write("Status: 500 Fail\n");
                    Out.Write("Content-type: text/html\n\n");
                    HeaderSent = true;
                    Out.Write(
                        failDoc
                            .Replace("%admin%", System.Configuration.ConfigurationManager.AppSettings["admin"])
                            .Replace("%error%", e.Message));
                }
            }
        }

        private static void DoJob(BinaryWriter Out)
        {
            string Query = System.Environment.GetEnvironmentVariable("QUERY_STRING");

            string RepoPath = System.Configuration.ConfigurationManager.AppSettings["repoPath"];
            System.IO.Directory.SetCurrentDirectory(RepoPath);
            Console.Error.WriteLine("Query={0}", Query);
            string Hash = ParseQuery(Query);
            Console.Error.WriteLine("Hash={0}", Hash);
            if (Hash == null) {
                ReportInvalid(Out, "Should provide hash as `h` parameter");
                return;
            }
            ReportBusy(Out);
/*            RunCommand(
                "git",
                string.Format("archive --format=zip {0}", Hash),
                Out.BaseStream,
                1024,
                S => {
                    var W = new BinaryWriter(S);
                    W.Write("Status: 200 OK\n");
                    W.Write("Content-Type: application/x-zip\n");
                    W.Write("Content-Disposition: filename=\"boo.zip\"\n\n");
                    W.Flush();
                    // https://stackoverflow.com/a/1084826/2303202
                    HeaderSent = true;
                }
            ); */
        }

        private static void RunCommand(string Program, string Cmdline, Stream OutStream, int Size, Action<Stream> InitStream)
        {
            // https://stackoverflow.com/a/206347/2303202
            var p = new System.Diagnostics.Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = Program;
            p.StartInfo.Arguments = Cmdline;
            p.Start();
            var Buffer = new byte[Size];
            var Count = p.StandardOutput.BaseStream.Read(Buffer, 0, Size);
            if (Count == Size) {
                InitStream(OutStream);
                Console.Error.WriteLine("Inited");
                OutStream.Write(Buffer, 0, Size);
                p.StandardOutput.BaseStream.CopyTo(OutStream);
                p.WaitForExit();
            } else {
                p.WaitForExit();
                throw new Exception("Command failed to produce output");
            }
        }

        private static string ParseQuery(string Query)
        {
            var r = new Regex(@"(^|&)h=([0-9a-f]{40})($|&)");

            var m = r.Match(Query);
            if (m.Success) {
                return m.Groups[2].Value;
            } else {
                return null;
            }
        }

        private static void ReportInvalid(BinaryWriter Out, string error)
        {
            Out.Write("Status: 400 Wrong\n");
            Out.Write("Content-type: text/html\n\n");
            HeaderSent = true;
            Out.Write(badDoc.Replace("%message%", error));
        }

        private static void ReportBusy(BinaryWriter Out)
        {
            Out.Write("Status: 503 Lunch time\n");
            Out.Write("Retry-after: 60\n");
            Out.Write("Content-type: text/html\n\n");
            HeaderSent = true;
            Out.Write(busyDoc);
        }
    
        private static readonly string busyDoc =
@"<html>
<title>Busy</title>
<body>
Somebody is already dowloading. Please try again later.
</body>
</html>";

        private static readonly string failDoc =
@"<html>
<title>Error</title>
<body>
Something went wrong. Please let %admin% know. Error: %error%
</body>
</html>";

        private static readonly string badDoc =
@"<html>
<title>Invalid request</title>
<body>
Invalid request. %message%
</body>
</html>";
    }
}
