using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace gitpackarchive {
    class MainClass {
        private static bool HeaderSent = false;

        private static System.Text.ASCIIEncoding Ascii = new System.Text.ASCIIEncoding();

        public static void Main(string[] args)
        {
            Console.Error.WriteLine("path={0}", System.Environment.GetEnvironmentVariable("PATH"));

            var OutStream = Console.OpenStandardOutput();
            try {
                DoJob(OutStream);
            } catch (Exception e) {
                Console.Error.WriteLine("Error: {0}", e);
                if(HeaderSent) {
                    return;
                } else {
                    using (var Out = new BinaryWriter(OutStream)) {
                        Out.Write(Ascii.GetBytes("Status: 500 Fail\n"));
                        Out.Write(Ascii.GetBytes("Content-type: text/html\n\n"));
                        HeaderSent = true;
                        Out.Write(
                            Ascii.GetBytes(failDoc
                                .Replace("%admin%", System.Configuration.ConfigurationManager.AppSettings["admin"])
                                .Replace("%error%", e.Message)));
                    }
                }
            }
        }

        private static void DoJob(Stream OutStream)
        {
            string Query = System.Environment.GetEnvironmentVariable("QUERY_STRING");

            string RepoPath = System.Configuration.ConfigurationManager.AppSettings["repoPath"];
            System.IO.Directory.SetCurrentDirectory(RepoPath);
            Console.Error.WriteLine("Query={0}", Query);
            string Hash = ParseQuery(Query);
            Console.Error.WriteLine("Hash={0}", Hash);
            if (Hash == null) {
                ReportInvalid(OutStream, "Should provide hash as `h` parameter");
                return;
            }
            RunCommand(
                System.Configuration.ConfigurationManager.AppSettings["gitExe"],
                string.Format("archive --format=zip {0}", Hash),
                OutStream,
                1024,
                S => {
                    var W = new BinaryWriter(S);
                    W.Write(Ascii.GetBytes("Status: 200 OK\n"));
                    W.Write(Ascii.GetBytes("Content-Type: application/x-zip\n"));
                    W.Write(Ascii.GetBytes("Content-Disposition: filename=\"boo.zip\"\n\n"));
                    W.Flush();
                    // https://stackoverflow.com/a/1084826/2303202
                    HeaderSent = true;
                }
            );
        }

        private static void RunCommand(string Program, string Cmdline, Stream OutStream, int Size, Action<Stream> InitStream)
        {
            // https://stackoverflow.com/a/206347/2303202
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = Program;
            p.StartInfo.Arguments = Cmdline;
            p.Start();
            var Buffer = new byte[Size];
            Console.Error.WriteLine("Start reading");
            int Count;
            if (ReadAndWait(p, Buffer, out Count)) {
                InitStream(OutStream);
                Console.Error.WriteLine("Inited");
                OutStream.Write(Buffer, 0, Size);
                Console.Error.WriteLine("Started copying");
                p.StandardOutput.BaseStream.CopyTo(OutStream);
                Console.Error.WriteLine("Done copying");
                p.WaitForExit();
            } else {
                p.StandardOutput.BaseStream.CopyTo(Stream.Null);
                Console.Error.WriteLine("output ={0}", Ascii.GetString(Buffer, 0, Count));
                p.WaitForExit();
                throw new Exception("Command failed to produce output");
            }
        }
        
        private static bool ReadAndWait(Process p, byte[] Buf, out int Count)
        {
            Count = 0;
            while (Count < Buf.Length && !p.WaitForExit(100)) {
                Count += p.StandardOutput.BaseStream.Read(Buf, Count, Buf.Length - Count);
                Console.Error.WriteLine("Count={0}", Count);
            }
            
            if (Count == Buf.Length) {
                return true;
            } else if (p.HasExited) {
                return p.ExitCode == 0;
            } else {
                throw new Exception("Internal error: should fill buffer");
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

        private static void ReportInvalid(Stream OutStream, string error)
        {
            using (var Out = new BinaryWriter(OutStream)) {
                Out.Write(Ascii.GetBytes("Status: 400 Wrong\n"));
                Out.Write(Ascii.GetBytes("Content-type: text/html\n\n"));
                HeaderSent = true;
                Out.Write(Ascii.GetBytes(badDoc.Replace("%message%", error)));
            }
        }

        private static void ReportBusy(Stream OutStream)
        {
            using (var Out = new BinaryWriter(OutStream)) {
                Out.Write(Ascii.GetBytes("Status: 503 Lunch time\n"));
                Out.Write(Ascii.GetBytes("Retry-After: 60\n"));
                Out.Write(Ascii.GetBytes("Content-Type: text/html\n\n"));
                HeaderSent = true;
                Out.Write(Ascii.GetBytes(busyDoc));
            }
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
