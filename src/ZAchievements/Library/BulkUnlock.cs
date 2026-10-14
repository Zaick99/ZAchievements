using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ZAchievements.Library
{
    /// <summary>
    /// Runs the headless unlock for one game in a child copy of this executable.
    /// Steam binds a process to a single app id, so a library-wide pass has to be
    /// a sequence of short-lived processes rather than one long loop.
    /// </summary>
    internal static class BulkUnlock
    {
        private static readonly TimeSpan PerGameBudget = TimeSpan.FromSeconds(45);

        /// <summary>The number of achievements unlocked, or null when the game could not be touched.</summary>
        public static async Task<int?> RunOneAsync(uint appId, CancellationToken cancel)
        {
            ProcessStartInfo start = new(Environment.ProcessPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };

            start.ArgumentList.Add("--unlock-all");
            start.ArgumentList.Add(appId.ToString(CultureInfo.InvariantCulture));

            using var child = Process.Start(start);
            if (child == null)
            {
                return null;
            }

            var report = child.StandardOutput.ReadToEndAsync(cancel);

            using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            budget.CancelAfter(PerGameBudget);

            try
            {
                await child.WaitForExitAsync(budget.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                Kill(child);
                return null;
            }

            return Parse(await report.ConfigureAwait(true));
        }

        private static int? Parse(string report)
        {
            var line = report.Trim();
            if (line.StartsWith("OK ", StringComparison.Ordinal) == false)
            {
                return null;
            }

            var fields = line.Split(' ');
            return fields.Length > 1 && int.TryParse(fields[1], out var unlocked) ? unlocked : null;
        }

        private static void Kill(Process child)
        {
            try
            {
                if (child.HasExited == false)
                {
                    child.Kill(entireProcessTree: true);
                }
            }
            catch (Exception)
            {
                // The child is already gone, which is the outcome we wanted.
            }
        }
    }
}
