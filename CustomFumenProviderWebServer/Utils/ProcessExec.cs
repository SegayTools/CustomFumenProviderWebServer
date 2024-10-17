using System.Diagnostics;

namespace CustomFumenProviderWebServer.Utils
{
    public static class ProcessExec
    {
        public record ExecResult(int ExitCode, string Output, string Error);

        public static async Task<ExecResult> Exec(string binFile, params string[] args)
        {
            var startInfo = new ProcessStartInfo(binFile, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(startInfo);

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            return new ExecResult(process.ExitCode, output, error);
        }
    }
}

