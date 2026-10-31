using System;

namespace DesktopHover
{
    // The worker publishes ownership here; only the UI thread consumes and paints results.
    internal sealed class DocumentProgress : IDisposable
    {
        private readonly object gate = new object();
        private DocumentPreviewResult latest;
        private bool disposed;
        internal void Publish(DocumentPreviewResult result)
        {
            lock (gate)
            {
                if (disposed) { result.Dispose(); return; }
                if (latest != null) latest.Dispose();
                latest = result;
            }
        }
        internal DocumentPreviewResult Take()
        {
            lock (gate) { var result = latest; latest = null; return result; }
        }
        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                if (latest != null) latest.Dispose();
                latest = null;
            }
        }
    }
}
