// © Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Runs a child process and logs its output.</summary>
static class ChildProcess
{
    /// <summary>Creates a process, redirecting its output, and waits for it to exit.</summary>
    public static Task<int> RunAsync(string fileName, string arguments, IReadOnlyDictionary<string, string> environment,
        TextWriter log, CancellationToken cancellationToken = default(CancellationToken))
    {
        TextWriter synchronizedLog = log != null ? TextWriter.Synchronized(log) : null;
        return RunAsync(fileName, arguments, environment, synchronizedLog, synchronizedLog, cancellationToken);
    }

    /// <summary>Creates a process, redirecting its output, and waits for it to exit.</summary>
    public static Task<int> RunAsync(string fileName, string arguments, IReadOnlyDictionary<string, string> environment,
        TextWriter outputLog, TextWriter errorLog, CancellationToken cancellationToken = default(CancellationToken))
    {
        TaskCompletionSource<int> taskSource = new TaskCompletionSource<int>();

        if (cancellationToken.IsCancellationRequested)
        {
            taskSource.SetCanceled();
            return taskSource.Task;
        }

        Process process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = outputLog != null,
            RedirectStandardError = errorLog != null
        };

        if (environment != null)
        {
            foreach (string key in environment.Keys)
            {
                process.StartInfo.Environment.Add(key, environment[key]);
            }
        }

        if (outputLog != null)
        {
            process.OutputDataReceived += (sender, e) => WriteData(outputLog, e);
        }

        if (errorLog != null)
        {
            process.ErrorDataReceived += (sender, e) => WriteData(errorLog, e);
        }

        process.Start();

        // Ensure the process doesn't wait forever if it tries to read from standard input.
        process.StandardInput.Close();

        if (outputLog != null)
        {
            process.BeginOutputReadLine();
        }

        if (errorLog != null)
        {
            process.BeginErrorReadLine();
        }

        object killProcessLock = new object();

        ChildProcessState state = new ChildProcessState
        {
            TaskSource = taskSource,
            Process = process,
            KillProcessLock = killProcessLock,
            CancellationToken = cancellationToken,
            OutputLog = outputLog,
            ErrorLog = errorLog
        };

        cancellationToken.Register(CancelProcessTask, state);

        Thread thread = new Thread(RunThread);
        thread.Start(state);

        return taskSource.Task;
    }

    /// <summary>Creates a process, redirecting its output, and waits for it to exit successfully.</summary>
    public static async Task RunAndThrowOnFailureAsync(string fileName, string arguments,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        StringBuilder outputBuffer = new StringBuilder();
        StringBuilder errorBuffer = new StringBuilder();

        int exitCode;

        using (TextWriter outputLog = new StringWriter(outputBuffer))
        using (TextWriter errorLog = new StringWriter(errorBuffer))
        {
            exitCode = await RunAsync(fileName, arguments, environment, outputLog, errorLog, cancellationToken);
        }

        if (exitCode != 0)
        {
            throw new InvalidOperationException(GetExceptionMessage(fileName, exitCode, outputBuffer.ToString(),
                errorBuffer.ToString()));
        }
    }

    internal static string GetExceptionMessage(string fileName, int exitCode, string standardOutput,
        string standardError)
    {
        Debug.Assert(exitCode != 0);

        StringBuilder message = new StringBuilder();
        message.Append(fileName);
        message.Append(" exited with code ");
        message.Append(exitCode);
        message.Append(".");

        bool hasError = standardError.Length > 0;
        bool hasOutput = standardOutput.Length > 0;

        if (hasError || hasOutput)
        {
            message.AppendLine();

            if (hasError)
            {
                message.AppendLine("Error:");
                message.Append(standardError);
            }

            if (hasError && hasOutput)
            {
                message.AppendLine();
            }

            if (hasOutput)
            {
                message.AppendLine("Output:");
                message.Append(standardOutput);
            }
        }

        const int maxOutputLength = 1000;
        return Truncate(message.ToString(), maxOutputLength);
    }

    internal static string Truncate(string text, int maxLength)
    {
        Debug.Assert(text != null);

        if (text.Length > maxLength)
        {
            StringBuilder truncated = new StringBuilder(maxLength);
            const string ellipsis = "...";
            truncated.Append(text.Substring(0, maxLength - ellipsis.Length));
            truncated.Append(ellipsis);
            Debug.Assert(truncated.Length == maxLength);
            return truncated.ToString();
        }

        return text;
    }

    // This thread owns the Process object and is the only one that disposes of it. It also owns the task and is the
    // only one that marks it as completed.
    static void RunThread(object untypedState)
    {
        ChildProcessState state = (ChildProcessState)untypedState;
        TaskCompletionSource<int> taskSource = state.TaskSource;
        Process process = state.Process;
        object killProcessLock = state.KillProcessLock;
        CancellationToken cancellationToken = state.CancellationToken;
        TextWriter outputLog = state.OutputLog;
        TextWriter errorLog = state.ErrorLog;

        try
        {
            try
            {
                // Always wait for the process to exit before returning to the caller (including marking the task as
                // canceled). Otherwise, resources (such as file handles) may still be in use, and the caller may
                // fail when it performs operations that require that such resources have already been released.
                // For example, deleting temp files passed to the child process may fail because the file is still
                // in use.
                process.WaitForExit();

                // Performance optimization (not needed for correctness): Avoid calling .Kill on the process after
                // this method has already found that the process has exited.
                state.Process = null;

                // CancelProcessTask may have killed the process, but this method owns setting the task's completion
                // state. Any time cancellation was requested, treat the task as canceled here regardless of what
                // caused the process to exit.
                if (cancellationToken.IsCancellationRequested)
                {
                    // Flushing output/error logs is only necessary in the RanToCompletion/success case below.
                    taskSource.TrySetCanceled(cancellationToken);
                }
                else
                {
                    int exitCode = process.ExitCode;

                    if (outputLog != null)
                    {
                        outputLog.Flush();
                    }

                    if (errorLog != null)
                    {
                        errorLog.Flush();
                    }

                    taskSource.SetResult(exitCode);
                }
            }
            catch (Exception exception)
            {
                taskSource.SetException(exception);
            }
        }
        finally
        {
            lock (killProcessLock)
            {
                process.Dispose();
                state.Process = null;
            }
        }
    }

    static void CancelProcessTask(object untypedState)
    {
        ChildProcessState state = (ChildProcessState)untypedState;
        Process process = state.Process;
        object killProcessLock = state.KillProcessLock;

        if (state.Process != null)
        {
            lock (killProcessLock)
            {
                if (state.Process != null)
                {
                    try
                    {
                        // Kill is asynchronous. Have the main thread wait for the process to exit and do normal
                        // cleanup (see futher comments there).
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                        // If the process has already exited, we don't need to do anything more to try to cancel.
                        // This exception type is thrown if Process can't acquire a handle to the underlying
                        // process.
                    }
                    catch (Win32Exception)
                    {
                        // See comment above.
                        // This exception type is throw if, after acquiring a handle to the underlying process, the
                        // TerminateProcess call fails (such as because the process has already exited).
                    }
                }
            }
        }
    }

    static void WriteData(TextWriter writer, DataReceivedEventArgs e)
    {
        string data = e.Data;

        // End of file
        if (data == null)
        {
            writer.Flush();
            return;
        }

        // e.Data is always a line but has the trailing newline removed. See:
        // https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.datareceivedeventargs.data
        writer.WriteLine(data);
    }

    class ChildProcessState
    {
        public TaskCompletionSource<int> TaskSource;
        public Process Process;
        public object KillProcessLock;
        public CancellationToken CancellationToken;
        public TextWriter OutputLog;
        public TextWriter ErrorLog;
    }
}
