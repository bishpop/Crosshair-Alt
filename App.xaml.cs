using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace CrosshairAlt
{
    public partial class App : Application
    {
        private const string MutexName = "Local\\CrosshairAlt_SingleInstance_Mutex";
        private const string WakeEventName = "Local\\CrosshairAlt_WakeEvent";

        private Mutex _instanceMutex;
        private EventWaitHandle _wakeEvent;
        private RegisteredWaitHandle _wakeWait;

        public App()
        {
            DispatcherUnhandledException += OnUnhandled;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _instanceMutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                try
                {
                    using (var existing = EventWaitHandle.OpenExisting(WakeEventName))
                        existing.Set();
                }
                catch { }
                Shutdown();
                return;
            }

            _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
            var main = new MainWindow();
            MainWindow = main;

            _wakeWait = ThreadPool.RegisterWaitForSingleObject(_wakeEvent, (state, timedOut) =>
            {
                Dispatcher.BeginInvoke(new Action(() => main.ShowMenu()));
            }, null, -1, false);

            if (!main.StartMinimizedOnBoot)
                main.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _wakeWait?.Unregister(null); } catch { }
            try { _wakeEvent?.Dispose(); } catch { }
            try { _instanceMutex?.ReleaseMutex(); _instanceMutex?.Dispose(); } catch { }
            base.OnExit(e);
        }

        private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, "Crosshair Alt - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
