using Sandbox.ModAPI;
using System;
using System.IO;
using System.Text;
using VRage;
using VRage.Utils;

namespace ProcBuild
{
    internal class Logging
    {
        private readonly FastResourceLock m_lock, m_writeLock;
        private readonly StringBuilder m_cache;
        private readonly string m_file;
        private TextWriter m_writer;
        private DateTime m_lastWriteTime;
        private int m_readyTicks;

        private const int WRITE_INTERVAL_TICKS = 30;
        private static readonly TimeSpan WRITE_INTERVAL_TIME = new TimeSpan(
            0, 0, 1);

        internal Logging(string file)
        {
            m_file = file;
            m_writer = null;
            m_lock = new FastResourceLock();
            m_writeLock = new FastResourceLock();
            m_cache = new StringBuilder();
            m_readyTicks = 0;
            m_lastWriteTime = DateTime.Now;
        }

        public void OnUpdate()
        {
            var requiresUpdate = false;
            try
            {
                m_lock.AcquireExclusive();
                requiresUpdate = m_cache.Length > 0;
            }
            finally
            {
                m_lock.ReleaseExclusive();
            }
            if (requiresUpdate)
                m_readyTicks++;
            else
                m_readyTicks = 0;
            if (m_readyTicks <= WRITE_INTERVAL_TICKS) return;
            Flush();
            m_readyTicks = 0;
        }

        public void Flush()
        {
            if (MyAPIGateway.Utilities != null)
                MyAPIGateway.Parallel.StartBackground(() =>
                {
                    try
                    {

                        if (m_writer == null)
                        {
                            try
                            {
                                m_writeLock.AcquireExclusive();
                                if (m_writer == null)
                                {
                                    m_writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(m_file, typeof(Logging));
                                    MyLog.Default.WriteLine("Opened log for ProceduralBuildings");
                                }
                            }
                            finally
                            {
                                m_writeLock.ReleaseExclusive();
                            }
                        }
                        if (m_writer == null || m_cache.Length <= 0) return;
                        string cache = null;
                        try
                        {
                            m_lock.AcquireExclusive();
                            if (m_writer != null && m_cache.Length > 0)
                            {
                                cache = m_cache.ToString();
                                m_cache.Clear();
                                m_lastWriteTime = DateTime.UtcNow;
                            }
                        }
                        finally
                        {
                            m_lock.ReleaseExclusive();
                        }
                        if (cache == null || m_writer == null) return;
                        try
                        {
                            m_writeLock.AcquireExclusive();
                            m_writer.Write(cache);
                            m_writer.Flush();
                        }
                        finally
                        {
                            m_writeLock.ReleaseExclusive();
                        }
                    }
                    catch (Exception e)
                    {
                        MyLog.Default.WriteLine("Procedural LogDump: \r\n" + e.ToString());
                    }
                });
        }

        public void Log(string fmt, params object[] args)
        {
            var shouldFlush = false;
            try
            {
                m_lock.AcquireExclusive();
                m_cache.AppendFormat(DateTime.Now.ToString("[HH:mm:ss] "));
                m_cache.AppendFormat(fmt, args);
                m_cache.Append("\r\n");
                shouldFlush = DateTime.UtcNow - m_lastWriteTime > WRITE_INTERVAL_TIME;
            }
            finally
            {
                m_lock.ReleaseExclusive();
            }
            if (shouldFlush)
                Flush();
        }

        public void Close()
        {
            if (m_lock == null) return;
            string remains = null;
            if (m_cache != null)
                try
                {
                    m_lock.AcquireExclusive();
                    if (m_cache.Length > 0)
                    {
                        remains = m_cache.ToString();
                        m_cache.Clear();
                    }
                }
                finally
                {
                    m_lock.ReleaseExclusive();
                }
            if (m_writer == null) return;
            try
            {
                m_writeLock.AcquireExclusive();
                if (remains != null)
                    m_writer.Write(remains);
                m_writer.Close();
                m_writer = null;
            }
            finally
            {
                m_writeLock.ReleaseExclusive();
            }
        }
    }
}