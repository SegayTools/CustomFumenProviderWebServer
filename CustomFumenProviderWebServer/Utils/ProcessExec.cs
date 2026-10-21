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
            if (process is null)
                throw new InvalidOperationException($"Failed to start process: {binFile}");

            //必须同时消费 stdout/stderr。先 await 其中一个时,另一个管道写满会让子进程阻塞,
            //子进程不再产生输出,等待方永远等不到 EOF。
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync();

            return new ExecResult(process.ExitCode, outputTask.Result, errorTask.Result);
        }
    }
}

