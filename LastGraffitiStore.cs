using System;
using System.Collections.Generic;

namespace QuickPickGraffiti
{
    internal sealed class LastGraffitiStore
    {
        private readonly Func<string, string> read;
        private readonly Action<string, string> write;
        internal LastGraffitiStore(Func<string, string> read, Action<string, string> write)
        {
            this.read = read;
            this.write = write;
        }
        internal int FindAvailable(string size, IList<string> titles)
        {
            string remembered = read(size);
            if (string.IsNullOrEmpty(remembered) || titles == null) return -1;
            for (int i = 0; i < titles.Count; i++)
                if (string.Equals(titles[i], remembered, StringComparison.Ordinal)) return i;
            return -1;
        }
        internal void RecordPainted(string size, string title)
        {
            if (!string.IsNullOrEmpty(title) && !string.Equals(read(size), title, StringComparison.Ordinal))
                write(size, title);
        }
    }
}
