using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ZAchievements.Infra;

namespace ZAchievements.Library
{
    /// <summary>
    /// The apps Steam has given a settled answer about — granted, or nothing free to
    /// grant — so a run picks up where it stopped. Games Steam silently refused are
    /// deliberately absent: they are still worth asking for once the quota refills.
    /// </summary>
    internal sealed class ClaimLog
    {
        private readonly HashSet<uint> _seen = new();
        private readonly string _path;

        public ClaimLog()
        {
            this._path = Path.Combine(AppPaths.Data, "settled.txt");
            this.Read();
        }

        public int Count => this._seen.Count;

        public bool Knows(uint appId) => this._seen.Contains(appId);

        public void Remember(IEnumerable<uint> appIds)
        {
            List<string> fresh = new();
            foreach (var id in appIds)
            {
                if (this._seen.Add(id) == true)
                {
                    fresh.Add(id.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (fresh.Count == 0)
            {
                return;
            }

            try
            {
                File.AppendAllLines(this._path, fresh);
            }
            catch (IOException)
            {
                // The run still works; only the resume point is lost.
            }
        }

        private void Read()
        {
            try
            {
                if (File.Exists(this._path) == false)
                {
                    return;
                }

                foreach (var line in File.ReadLines(this._path))
                {
                    if (uint.TryParse(line, NumberStyles.None, CultureInfo.InvariantCulture, out var id) == true)
                    {
                        this._seen.Add(id);
                    }
                }
            }
            catch (IOException)
            {
                // An unreadable log just means nothing is skipped.
            }
        }

        public void Forget()
        {
            this._seen.Clear();

            try
            {
                File.Delete(this._path);
            }
            catch (IOException)
            {
                // Nothing to do: the in-memory set is already empty.
            }
        }
    }
}
