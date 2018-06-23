/*
 * Based on https://github.com/wilfrem/UnixSignalWaiter
 */

using System;
using System.Reflection;

namespace HomeGenie.Utils
{
    /// <summary>
    /// Utility for waiting UNIX exit signal
    /// </summary>
    public class SignalWaiter
    {
        private bool _alreadyInited;
        private readonly PlatformID _platform;
        private Assembly _posixAsm;
        private Type _unixSignalType, _signumType;
        private MethodInfo _unixSignalWaitAny;

        private Array _signals;

        private static readonly Lazy<SignalWaiter> LazyInstance = new Lazy<SignalWaiter>(() => new SignalWaiter());
        public static SignalWaiter Instance => LazyInstance.Value;

        private SignalWaiter()
        {
            _platform = Environment.OSVersion.Platform;
        }

        private void Setup()
        {
            if (_alreadyInited)
                return;

            //dynamic load Mono.Posix assembly
            _posixAsm = Assembly.Load("Mono.Posix, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");

            //create UnixSignal Type
            _unixSignalType = _posixAsm.GetType("Mono.Unix.UnixSignal");

            //get member function WaitAny
            _unixSignalWaitAny = _unixSignalType.GetMethod("WaitAny", new[] { _unixSignalType.MakeArrayType() });

            //create Signum enum type
            _signumType = _posixAsm.GetType("Mono.Unix.Native.Signum");

            _signals = Array.CreateInstance(_unixSignalType, 2);
            _signals.SetValue(Activator.CreateInstance(_unixSignalType, _signumType.GetField("SIGINT").GetValue(null)), 0);
            _signals.SetValue(Activator.CreateInstance(_unixSignalType, _signumType.GetField("SIGTERM").GetValue(null)), 1);
            _alreadyInited = true;
        }

        public bool CanWaitExitSignal()
        {
            return !(_platform != PlatformID.Unix && _platform != PlatformID.MacOSX);
        }

        /// <summary>
        /// Wait while getting exit signal
        /// </summary>
        /// <exception cref="InvalidOperationException">when call it on Windows</exception>
        public void WaitExitSignal()
        {
            if (!CanWaitExitSignal())
            {
                throw new InvalidOperationException("not unix platform");
            }

            Setup();

            // Wait for a unix SIGINT/SIGTERM signal
            for (var exit = false; !exit; )
            {
                var id = (int)_unixSignalWaitAny.Invoke(null, new object[] { _signals });

                if (id >= 0 && id < _signals.Length)
                {
                    dynamic val = _signals.GetValue(id);
                    if (val.IsSet)
                        exit = true;
                }
            }
        }

        /// <summary>
        /// Wait while getting exit signal and execute given action
        /// </summary>
        /// <param name="action">Action to invoke on receiving the signal</param>
        /// <exception cref="InvalidOperationException">when call it on Windows</exception>
        public void WaitExitSignal(Action action)
        {
            WaitExitSignal();
            action.Invoke();
        }
    }
}
