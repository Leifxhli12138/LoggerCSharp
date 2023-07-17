using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using LoggerCSharp.LogUtil;
using Microsoft.Win32;

namespace LoggerCSharp.RegistryUtil
{
    [Flags]
    public enum MonitorFlags
    {
        /// <summary>Notify the caller if a subkey is added or deleted.</summary>
        Key = 0x1,

        /// <summary>Notify the caller of changes to the attributes of the key, such as the security descriptor information.</summary>
        Attribute = 0x2,

        /// <summary>Notify the caller of changes to a value of the key. This can include adding or deleting a value, or changing an existing value.</summary>
        Value = 0x4,

        /// <summary>Notify the caller of changes to the security descriptor of the key.</summary>
        Security = 0x8,

        KeyValue = Key | Value,

        All = 0xf,
    }

    public class RegistryMonitor : IDisposable
    {
        #region P/Invoke
        private const int KEYQUERYVALUE = 0x0001;
        private const int KEYNOTIFY = 0x0010;
        private const int STANDARDRIGHTSREAD = 0x00020000;
        private const int KEYWOW6464KEY = 0x0100;
        private const int KEYWOW6432KEY = 0x0200;

        private static readonly IntPtr HKEYCLASSESROOT = new IntPtr(unchecked((int)0x80000000));
        private static readonly IntPtr HKEYCURRENTUSER = new IntPtr(unchecked((int)0x80000001));
        private static readonly IntPtr HKEYLOCALMACHINE = new IntPtr(unchecked((int)0x80000002));
        private static readonly IntPtr HKEYUSERS = new IntPtr(unchecked((int)0x80000003));
        private static readonly IntPtr HKEYPERFORMANCEDATA = new IntPtr(unchecked((int)0x80000004));
        private static readonly IntPtr HKEYCURRENTCONFIG = new IntPtr(unchecked((int)0x80000005));
        private static readonly IntPtr HKEYDYNDATA = new IntPtr(unchecked((int)0x80000006));

        #region Private member variables

        private readonly object _threadLock = new object();
        private readonly ManualResetEvent _eventTerminate = new ManualResetEvent(false);
        private readonly bool _use32BitView;

        private IntPtr _registryHive;
        private string _registrySubName;
        private Thread _thread;
        private bool _disposed;
        private MonitorFlags _regFlags = MonitorFlags.KeyValue;
        #endregion
        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryMonitor"/> class.
        /// </summary>
        /// <param name="registryKey">The registry key to monitor.</param>
        public RegistryMonitor(RegistryKey registryKey)
        {
            _use32BitView = registryKey.View == RegistryView.Registry32;
            InitRegistryKey(registryKey.Name);
            Name = registryKey.Name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryMonitor"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="is32BitView"></param>
        public RegistryMonitor(string name, bool is32BitView = true)
        {
            Name = name;
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            _use32BitView = is32BitView;
            InitRegistryKey(name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryMonitor"/> class.
        /// </summary>
        /// <param name="registryHive">The registry hive.</param>
        /// <param name="subKey">The sub key.</param>
        /// <param name="is32BitView"></param>
        public RegistryMonitor(RegistryHive registryHive, string subKey, bool is32BitView = true)
        {
            _use32BitView = is32BitView;
            InitRegistryKey(registryHive, subKey);
        }

        ~RegistryMonitor()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }
        #endregion

        #region Event handling

        /// <summary>
        /// Occurs when the specified registry key has changed.
        /// </summary>
        public event EventHandler<string> RegChanged;

        /// <summary>
        /// Occurs when the access to the registry fails.
        /// </summary>
        public event ErrorEventHandler Error;

        public int RegChangedMonitorCount => RegChanged?.GetInvocationList().Length ?? 0;
        #endregion

        public string Name { get; set; }

        /// <summary>
        /// <b>true</b> if this <see cref="RegistryMonitor"/> object is currently monitoring;
        /// otherwise, <b>false</b>.
        /// </summary>
        public bool IsMonitoring => _thread != null;

        /// <summary>
        /// Gets or sets the <see cref="MonitorFlags">MonitorFlags</see>.
        /// </summary>
        public MonitorFlags MonitorFlags
        {
            get => _regFlags;
            set
            {
                lock (_threadLock)
                {
                    if (IsMonitoring)
                    {
                        throw new InvalidOperationException("Monitoring thread is already running");
                    }

                    _regFlags = value;
                }
            }
        }

        /// <summary>
        /// Start monitoring.
        /// </summary>
        public void Start()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null, "This instance is already disposed");
            }

            lock (_threadLock)
            {
                if (!IsMonitoring)
                {
                    _eventTerminate.Reset();
                    _thread = new Thread(MonitorThread)
                    {
                        IsBackground = true,
                    };
                    _thread.Start();
                }
            }
        }

        /// <summary>
        /// Stops the monitoring thread.
        /// </summary>
        public void Stop()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null, "This instance is already disposed");
            }

            lock (_threadLock)
            {
                var thread = _thread;
                if (thread != null)
                {
                    _eventTerminate.Set();
                    thread.Join();
                }
            }
        }

        #region IDisposable Support

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Stop();
                _eventTerminate.Dispose();
                _disposed = true;
            }
        }
        #endregion

        /// <summary>
        /// Raises the <see cref="RegChanged"/> event.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>OnRegChanged</b> is called when the specified registry key has changed.
        /// </p>
        /// <note type="inheritinfo">
        /// When overriding <see cref="OnRegChanged"/> in a derived class, be sure to call
        /// the base class's <see cref="OnRegChanged"/> method.
        /// </note>
        /// </remarks>
        protected virtual void OnRegChanged(string registryKey)
        {
            RegChanged?.Invoke(this, registryKey);
        }

        /// <summary>
        /// Raises the <see cref="Error"/> event.
        /// </summary>
        /// <param name="e">The <see cref="Exception"/> which occured while watching the registry.</param>
        /// <remarks>
        /// <p>
        /// <b>OnError</b> is called when an exception occurs while watching the registry.
        /// </p>
        /// <note type="inheritinfo">
        /// When overriding <see cref="OnError"/> in a derived class, be sure to call
        /// the base class's <see cref="OnError"/> method.
        /// </note>
        /// </remarks>
        protected virtual void OnError(Exception e)
        {
            Error?.Invoke(this, new ErrorEventArgs(e));
        }

        [DllImport(@"advapi32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int RegOpenKey(IntPtr key, String subKey, out IntPtr resultSubKey);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int RegOpenKeyEx(
            IntPtr hKey,
            string subKey,
            uint options,
            int samDesired,
            out IntPtr phkResult);

        [DllImport("advapi32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int RegNotifyChangeKeyValue(
            IntPtr hKey,
            bool bWatchSubtree,
            MonitorFlags dwNotifyFlags,
            IntPtr hEvent,
            bool fAsynchronous);

        [DllImport("advapi32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int RegCloseKey(IntPtr hKey);

        #region Initialization

        private void InitRegistryKey(RegistryHive hive, string name)
        {
            switch (hive)
            {
                case RegistryHive.ClassesRoot:
                    _registryHive = HKEYCLASSESROOT;
                    break;

                case RegistryHive.CurrentConfig:
                    _registryHive = HKEYCURRENTCONFIG;
                    break;

                case RegistryHive.CurrentUser:
                    _registryHive = HKEYCURRENTUSER;
                    break;

                case RegistryHive.LocalMachine:
                    _registryHive = HKEYLOCALMACHINE;
                    break;

                case RegistryHive.PerformanceData:
                    _registryHive = HKEYPERFORMANCEDATA;
                    break;

                case RegistryHive.Users:
                    _registryHive = HKEYUSERS;
                    break;

                default:
                    throw new InvalidEnumArgumentException(nameof(hive), (int)hive, typeof(RegistryHive));
            }

            _registrySubName = name;
        }

        private void InitRegistryKey(string name)
        {
            var nameParts = name.Split('\\');

            switch (nameParts[0])
            {
                case "HKEY_CLASSES_ROOT":
                case "HKCR":
                    _registryHive = HKEYCLASSESROOT;
                    break;

                case "HKEY_CURRENT_USER":
                case "HKCU":
                    _registryHive = HKEYCURRENTUSER;
                    break;

                case "HKEY_LOCAL_MACHINE":
                case "HKLM":
                    _registryHive = HKEYLOCALMACHINE;
                    break;

                case "HKEY_USERS":
                    _registryHive = HKEYUSERS;
                    break;

                case "HKEY_CURRENT_CONFIG":
                    _registryHive = HKEYCURRENTCONFIG;
                    break;

                default:
                    _registryHive = IntPtr.Zero;
                    throw new ArgumentException("The registry hive '" + nameParts[0] + "' is not supported", "value");
            }

            _registrySubName = string.Join("\\", nameParts, 1, nameParts.Length - 1);
        }

        #endregion

        private void MonitorThread()
        {
            while (!_eventTerminate.WaitOne(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    ThreadLoop();
                }
                catch (Exception e)
                {
                    Logger.LogWarn(e.Message);
                }
            }

            _thread = null;
        }

        private void ThreadLoop()
        {
            var flags = STANDARDRIGHTSREAD | KEYQUERYVALUE | KEYNOTIFY;

            flags |= _use32BitView ? KEYWOW6432KEY : KEYWOW6464KEY;

            var result = RegOpenKeyEx(_registryHive, _registrySubName, 0, 0x0010, out var registryKey);
            using (new DisposeAction(() =>
            {
                if (registryKey != IntPtr.Zero)
                {
                    RegCloseKey(registryKey);
                }
            }))
            {
                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                var eventNotify = new AutoResetEvent(false);
                WaitHandle[] waitHandles = { eventNotify, _eventTerminate };
                while (!_eventTerminate.WaitOne(0, true))
                {
                    result = RegNotifyChangeKeyValue(registryKey, false, _regFlags, eventNotify.SafeWaitHandle.DangerousGetHandle(), true);
                    if (result != 0)
                    {
                        throw new Win32Exception(result);
                    }

                    if (WaitHandle.WaitAny(waitHandles) == 0)
                    {
                        OnRegChanged(_registrySubName);
                    }
                }
            }
        }
    }
}
