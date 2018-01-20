using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using OpenSource.UPnP;
using OpenSource.Utilities;

namespace MIG.Interfaces.Protocols
{
    public sealed class UpnpSmartControlPoint
    {
        private ArrayList activeDeviceList = ArrayList.Synchronized(new ArrayList());
        private UPnPDeviceFactory deviceFactory = new UPnPDeviceFactory();
        private LifeTimeMonitor deviceLifeTimeClock = new LifeTimeMonitor();
        private Hashtable deviceTable = new Hashtable();
        private object deviceTableLock = new object();
        private LifeTimeMonitor deviceUpdateClock = new LifeTimeMonitor();
        private UPnPControlPoint genericControlPoint;
        private NetworkInfo hostNetworkInfo;
        private WeakEvent OnAddedDeviceEvent = new WeakEvent();
        private WeakEvent OnDeviceExpiredEvent = new WeakEvent();
        private WeakEvent OnRemovedDeviceEvent = new WeakEvent();
        private WeakEvent OnUpdatedDeviceEvent = new WeakEvent();
        private string searchFilter = "upnp:rootdevice";
        //"ssdp:all"; //

        public UpnpSmartControlPoint()
        {
            deviceFactory.OnDevice += DeviceFactoryCreationSink;
            deviceLifeTimeClock.OnExpired += DeviceLifeTimeClockSink;
            deviceUpdateClock.OnExpired += DeviceUpdateClockSink;
            hostNetworkInfo = new NetworkInfo(NetworkInfoNewInterfaceSink);
            hostNetworkInfo.OnInterfaceDisabled += NetworkInfoOldInterfaceSink;
            genericControlPoint = new UPnPControlPoint(hostNetworkInfo);
            genericControlPoint.OnSearch += UPnPControlPointSearchSink;
            genericControlPoint.OnNotify += SSDPNotifySink;
            genericControlPoint.FindDeviceAsync(searchFilter);
        }

        public void ShutDown()
        {
            deviceFactory.OnDevice -= DeviceFactoryCreationSink;
            deviceLifeTimeClock.OnExpired -= DeviceLifeTimeClockSink;
            deviceUpdateClock.OnExpired -= DeviceUpdateClockSink;
            hostNetworkInfo.OnInterfaceDisabled -= NetworkInfoOldInterfaceSink;
            genericControlPoint.OnSearch -= UPnPControlPointSearchSink;
            genericControlPoint.OnNotify -= SSDPNotifySink;
            deviceFactory.Shutdown();
            deviceFactory = null;
            foreach (UPnPDevice dev in activeDeviceList) 
            {
                dev.Removed();
            }
            hostNetworkInfo = null;
            genericControlPoint.Dispose();
            genericControlPoint = null;
        }

        public ArrayList DeviceTable
        {
            get { return activeDeviceList; }
        }

        public event DeviceHandler OnAddedDevice
        {
            add
            {
                OnAddedDeviceEvent.Register(value);
            }
            remove
            {
                OnAddedDeviceEvent.UnRegister(value);
            }
        }

        public event DeviceHandler OnDeviceExpired
        {
            add
            {
                OnDeviceExpiredEvent.Register(value);
            }
            remove
            {
                OnDeviceExpiredEvent.UnRegister(value);
            }
        }

        public event DeviceHandler OnRemovedDevice
        {
            add
            {
                OnRemovedDeviceEvent.Register(value);
            }
            remove
            {
                OnRemovedDeviceEvent.UnRegister(value);
            }
        }

        public event DeviceHandler OnUpdatedDevice
        {
            add
            {
                OnUpdatedDeviceEvent.Register(value);
            }
            remove
            {
                OnUpdatedDeviceEvent.UnRegister(value);
            }
        }

        private void DeviceFactoryCreationSink(UPnPDeviceFactory sender, UPnPDevice device, Uri locationURL)
        {
            //Console.WriteLine("UPnPDevice[" + device.FriendlyName + "]@" + device.LocationURL + " advertised UDN[" + device.UniqueDeviceName + "]");
            if (!deviceTable.Contains(device.UniqueDeviceName))
            {
                EventLogger.Log(this, EventLogEntryType.Error, "UPnPDevice[" + device.FriendlyName + "]@" + device.LocationURL + " advertised UDN[" + device.UniqueDeviceName + "] in xml but not in SSDP");
            }
            else
            {
                lock (deviceTableLock)
                {
                    DeviceInfo info2 = (DeviceInfo)deviceTable[device.UniqueDeviceName];
                    if (info2.Device != null)
                    {
                        EventLogger.Log(this, EventLogEntryType.Information, "Unexpected UPnP Device Creation: " + device.FriendlyName + "@" + device.LocationURL);
                        return;
                    }
                    DeviceInfo info = (DeviceInfo)deviceTable[device.UniqueDeviceName];
                    info.Device = device;
                    deviceTable[device.UniqueDeviceName] = info;
                    deviceLifeTimeClock.Add(device.UniqueDeviceName, device.ExpirationTimeout);
                    activeDeviceList.Add(device);
                }
                OnAddedDeviceEvent.Fire(this, device);
            }
        }

        private void DeviceLifeTimeClockSink(LifeTimeMonitor sender, object obj)
        {
            DeviceInfo info;
            lock (deviceTableLock)
            {
                if (!deviceTable.ContainsKey(obj))
                {
                    return;
                }
                info = (DeviceInfo)deviceTable[obj];
                deviceTable.Remove(obj);
                deviceUpdateClock.Remove(obj);
                if (activeDeviceList.Contains(info.Device))
                {
                    activeDeviceList.Remove(info.Device);
                }
                else
                {
                    info.Device = null;
                }
            }
            if (info.Device != null)
            {
                info.Device.Removed();
                OnDeviceExpiredEvent.Fire(this, info.Device);
            }
        }

        private void DeviceUpdateClockSink(LifeTimeMonitor sender, object obj)
        {
            lock (deviceTableLock)
            {
                if (deviceTable.ContainsKey(obj))
                {
                    DeviceInfo info = (DeviceInfo)deviceTable[obj];
                    if (info.PendingBaseURL != null)
                    {
                        info.BaseURL = info.PendingBaseURL;
                        info.MaxAge = info.PendingMaxAge;
                        info.SourceEP = info.PendingSourceEP;
                        info.LocalEP = info.PendingLocalEP;
                        info.NotifyTime = DateTime.Now;
                        info.Device.UpdateDevice(info.BaseURL, info.LocalEP.Address);
                        deviceTable[obj] = info;
                        deviceLifeTimeClock.Add(info.UDN, info.MaxAge);
                    }
                }
            }
        }

        public UPnPDevice[] GetCurrentDevices()
        {
            return (UPnPDevice[])activeDeviceList.ToArray(typeof(UPnPDevice));
        }

        private void NetworkInfoNewInterfaceSink(NetworkInfo sender, IPAddress Intfce)
        {
            if (genericControlPoint != null)
            {
                genericControlPoint.FindDeviceAsync(searchFilter);
            }
        }

        private void NetworkInfoOldInterfaceSink(NetworkInfo sender, IPAddress Intfce)
        {
            ArrayList list = new ArrayList();
            lock (deviceTableLock)
            {
                foreach (UPnPDevice device in GetCurrentDevices())
                {
                    if (device.InterfaceToHost.Equals(Intfce))
                    {
                        list.Add(UnprotectedRemoveMe(device));
                    }
                }
            }
            foreach (UPnPDevice device2 in list)
            {
                OnRemovedDeviceEvent.Fire(this, device2);
                device2.Removed();
            }
            genericControlPoint.FindDeviceAsync(searchFilter);
        }

        internal void RemoveMe(UPnPDevice _d)
        {
            UPnPDevice parentDevice = _d;
            UPnPDevice device2 = null;
            while (parentDevice.ParentDevice != null)
            {
                parentDevice = parentDevice.ParentDevice;
            }
            lock (deviceTableLock)
            {
                if (!deviceTable.ContainsKey(parentDevice.UniqueDeviceName))
                {
                    return;
                }
                device2 = UnprotectedRemoveMe(parentDevice);
            }
            if (device2 != null)
            {
                device2.Removed();
            }
            if (device2 != null)
            {
                OnRemovedDeviceEvent.Fire(this, device2);
            }
        }

        public void Rescan()
        {
            lock (deviceTableLock)
            {
                IDictionaryEnumerator enumerator = deviceTable.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    string key = (string)enumerator.Key;
                    deviceLifeTimeClock.Add(key, 20);
                }
            }
            genericControlPoint.FindDeviceAsync(searchFilter);
        }

        internal void SSDPNotifySink(IPEndPoint source, IPEndPoint local, Uri LocationURL, bool IsAlive, string USN, string SearchTarget, int MaxAge, HTTPMessage Packet)
        {
            UPnPDevice device = null;
            if (SearchTarget == searchFilter)
            {
                if (!IsAlive)
                {
                    lock (deviceTableLock)
                    {
                        device = UnprotectedRemoveMe(USN);
                    }
                    if (device != null)
                    {
                        device.Removed();
                    }
                    if (device != null)
                    {
                        OnRemovedDeviceEvent.Fire(this, device);
                    }
                }
                else
                {
                    lock (deviceTableLock)
                    {
                        if (!deviceTable.ContainsKey(USN))
                        {
                            DeviceInfo info = new DeviceInfo();
                            info.Device = null;
                            info.UDN = USN;
                            info.NotifyTime = DateTime.Now;
                            info.BaseURL = LocationURL;
                            info.MaxAge = MaxAge;
                            info.LocalEP = local;
                            info.SourceEP = source;
                            deviceTable[USN] = info;
                            deviceFactory.CreateDevice(info.BaseURL, info.MaxAge, IPAddress.Any, info.UDN);
                        }
                        else
                        {
                            DeviceInfo info2 = (DeviceInfo)deviceTable[USN];
                            if (info2.Device != null)
                            {
                                if (info2.BaseURL.Equals(LocationURL))
                                {
                                    deviceUpdateClock.Remove(info2);
                                    info2.PendingBaseURL = null;
                                    info2.PendingMaxAge = 0;
                                    info2.PendingLocalEP = null;
                                    info2.PendingSourceEP = null;
                                    info2.NotifyTime = DateTime.Now;
                                    deviceTable[USN] = info2;
                                    deviceLifeTimeClock.Add(info2.UDN, MaxAge);
                                }
                                else if (info2.NotifyTime.AddSeconds(10.0).Ticks < DateTime.Now.Ticks)
                                {
                                    info2.PendingBaseURL = LocationURL;
                                    info2.PendingMaxAge = MaxAge;
                                    info2.PendingLocalEP = local;
                                    info2.PendingSourceEP = source;
                                    deviceTable[USN] = info2;
                                    deviceUpdateClock.Add(info2.UDN, 3);
                                }
                            }
                        }
                    }
                }
            }
        }

        internal UPnPDevice UnprotectedRemoveMe(UPnPDevice _d)
        {
            UPnPDevice parentDevice = _d;
            while (parentDevice.ParentDevice != null)
            {
                parentDevice = parentDevice.ParentDevice;
            }
            return UnprotectedRemoveMe(parentDevice.UniqueDeviceName);
        }

        internal UPnPDevice UnprotectedRemoveMe(string UDN)
        {
            UPnPDevice device = null;
            try
            {
                DeviceInfo info = (DeviceInfo)deviceTable[UDN];
                device = info.Device;
                deviceTable.Remove(UDN);
                deviceLifeTimeClock.Remove(info.UDN);
                deviceUpdateClock.Remove(info);
                activeDeviceList.Remove(device);
            }
            catch
            {
            }
            return device;
        }

        private void UPnPControlPointSearchSink(IPEndPoint source, IPEndPoint local, Uri LocationURL, string USN, string SearchTarget, int MaxAge)
        {
            lock (deviceTableLock)
            {
                if (!deviceTable.ContainsKey(USN))
                {
                    DeviceInfo info = new DeviceInfo();
                    info.Device = null;
                    info.UDN = USN;
                    info.NotifyTime = DateTime.Now;
                    info.BaseURL = LocationURL;
                    info.MaxAge = MaxAge;
                    info.LocalEP = local;
                    info.SourceEP = source;
                    deviceTable[USN] = info;
                    deviceFactory.CreateDevice(info.BaseURL, info.MaxAge, IPAddress.Any, info.UDN);
                }
                else
                {
                    DeviceInfo info2 = (DeviceInfo)deviceTable[USN];
                    if (info2.Device != null)
                    {
                        if (info2.BaseURL.Equals(LocationURL))
                        {
                            deviceUpdateClock.Remove(info2);
                            info2.PendingBaseURL = null;
                            info2.PendingMaxAge = 0;
                            info2.PendingLocalEP = null;
                            info2.PendingSourceEP = null;
                            info2.NotifyTime = DateTime.Now;
                            deviceTable[USN] = info2;
                            deviceLifeTimeClock.Add(info2.UDN, MaxAge);
                        }
                        else if (info2.NotifyTime.AddSeconds(10.0).Ticks < DateTime.Now.Ticks)
                        {
                            info2.PendingBaseURL = LocationURL;
                            info2.PendingMaxAge = MaxAge;
                            info2.PendingLocalEP = local;
                            info2.PendingSourceEP = source;
                            deviceUpdateClock.Add(info2.UDN, 3);
                        }
                    }
                }
            }
        }

        public delegate void DeviceHandler(UpnpSmartControlPoint sender, UPnPDevice Device);

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceInfo
        {
            public UPnPDevice Device;
            public DateTime NotifyTime;
            public string UDN;
            public Uri BaseURL;
            public int MaxAge;
            public IPEndPoint LocalEP;
            public IPEndPoint SourceEP;
            public Uri PendingBaseURL;
            public int PendingMaxAge;
            public IPEndPoint PendingLocalEP;
            public IPEndPoint PendingSourceEP;
        }
    }
}