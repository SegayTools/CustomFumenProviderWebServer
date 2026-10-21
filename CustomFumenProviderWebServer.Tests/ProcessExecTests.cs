using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Tests;

public class ProcessExecTests
{
    [Fact]
    public async Task Exec_DoesNotDeadlock_WhenChildWritesLargeStdoutAndStderr()
    {
        var scriptPath = Path.Combine(
            Path.GetTempPath(),
            $"process-exec-{Guid.NewGuid():N}" + (OperatingSystem.IsWindows() ? ".bat" : ".sh"));

        var marker = OperatingSystem.IsWindows() ? "10000" : "30000";
        var script = OperatingSystem.IsWindows()
            ? "@echo off\r\nfor /L %%i in (1,1,10000) do (\r\n  echo OUT-%%i\r\n  echo ERR-%%i 1>&2\r\n)\r\n"
            : "#!/bin/sh\ni=1\nwhile [ $i -le 30000 ]; do\n  echo \"OUT-$i\"\n  echo \"ERR-$i\" >&2\n  i=$((i+1))\ndone\n";

        await File.WriteAllTextAsync(scriptPath, script);

        try
        {
            var task = OperatingSystem.IsWindows()
                ? ProcessExec.Exec("cmd.exe", "/c", scriptPath)
                : ProcessExec.Exec("/bin/sh", scriptPath);

            var result = await task.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"OUT-{marker}", result.Output);
            Assert.Contains($"ERR-{marker}", result.Error);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }
}
