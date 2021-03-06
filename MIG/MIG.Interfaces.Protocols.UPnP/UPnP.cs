﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using MIG.Config;
using OpenSource.UPnP;

namespace MIG.Interfaces.Protocols
{
    public class UPnP : MigInterface
    {

        #region Private fields

        private UpnpSmartControlPoint controlPoint;
        private bool isConnected = false;
        private object deviceOperationLock = new object();
        private List<InterfaceModule> modules = new List<InterfaceModule>();
        //private UPnPDevice localDevice;

        private class DeviceHolder
        {
            public UPnPDevice Device;
            public bool Initialized;
        }

        #endregion

        #region MigInterface API commands

        public enum Commands
        {
            NotSet,

            Control_On,
            Control_Off,
            Control_Level,
            Control_Toggle,

            AvMedia_Browse,
            AvMedia_GetItem,
            AvMedia_GetUri,
            AvMedia_SetUri,
            AvMedia_GetTransportInfo,
            AvMedia_GetMediaInfo,
            AvMedia_GetPositionInfo,

            AvMedia_Play,
            AvMedia_Pause,
            AvMedia_Stop,
            AvMedia_Seek,

            AvMedia_Prev,
            AvMedia_Next,
            AvMedia_SetNext,

            AvMedia_GetMute,
            AvMedia_SetMute,
            AvMedia_GetVolume,
            AvMedia_SetVolume
        }

        #endregion

        #region MIG Interface members

        public event InterfaceModulesChangedEventHandler InterfaceModulesChanged;
        public event InterfacePropertyChangedEventHandler InterfacePropertyChanged;

        public string Domain
        {
            get
            {
                string ifacedomain = this.GetType().Namespace.ToString();
                ifacedomain = ifacedomain.Substring(ifacedomain.LastIndexOf(".") + 1) + "." + this.GetType().Name.ToString();
                return ifacedomain;
            }
        }

        public bool IsEnabled { get; set; }

        public List<Option> Options { get; set; }

        public void OnSetOption(Option option)
        {
            if (IsEnabled)
                Connect();
        }

        public List<InterfaceModule> GetModules()
        {
            Thread updatePropertiesAsync = new Thread(() =>
            {
                Thread.Sleep(2000);
                UpdateDeviceProperties();
            });
            updatePropertiesAsync.Start();
            return modules;
        }

        public bool IsConnected
        {
            get { return isConnected; }
        }

        public bool Connect()
        {
            if (controlPoint == null)
            {
                controlPoint = new UpnpSmartControlPoint();
                controlPoint.OnAddedDevice += controPoint_OnAddedDevice;
                controlPoint.OnRemovedDevice += controPoint_OnRemovedDevice;
                controlPoint.OnDeviceExpired += controPoint_OnDeviceExpired;
                isConnected = true;
            }
            OnInterfaceModulesChanged(this.GetDomain());
            return true;

        }

        public void Disconnect()
        {
            /*
            if (localDevice != null)
            {
                localDevice.StopDevice();
                localDevice = null;
            }
            */
            if (controlPoint != null)
            {
                controlPoint.OnAddedDevice -= controPoint_OnAddedDevice;
                controlPoint.OnRemovedDevice -= controPoint_OnRemovedDevice;
                controlPoint.OnDeviceExpired -= controPoint_OnDeviceExpired;
                controlPoint.ShutDown();
                controlPoint = null;
            }
            isConnected = false;
        }

        public bool IsDevicePresent()
        {
            return true;
        }

        public object InterfaceControl(MigInterfaceCommand request)
        {
            object returnValue = null;
            bool raiseEvent = false;
            string eventParameter = "Status.Unhandled";
            string eventValue = "";
            //
            var device = GetUpnpDevice(request.Address);

            Commands command;
            Enum.TryParse<Commands>(request.Command.Replace(".", "_"), out command);

            // TODO: ??? Commands: SwitchPower, Dimming

            switch (command)
            {
            case Commands.Control_On:
            case Commands.Control_Off:
                {
                    bool commandValue = (command == Commands.Control_On ? true : false);
                    var newValue = new UPnPArgument("newTargetValue", commandValue);
                    var args = new UPnPArgument[] { 
                        newValue
                    };
                    InvokeUpnpDeviceService(device, "SwitchPower", "SetTarget", args);
                    //
                    raiseEvent = true;
                    eventParameter = "Status.Level";
                    eventValue = (commandValue ? "1" : "0");
                }
                break;
            case Commands.Control_Level:
                {
                    var newvalue = new UPnPArgument("NewLoadLevelTarget", (byte)uint.Parse(request.GetOption(0)));
                    var args = new UPnPArgument[] { 
                        newvalue
                    };
                    InvokeUpnpDeviceService(device, "Dimming", "SetLoadLevelTarget", args);
                    //
                    raiseEvent = true;
                    eventParameter = "Status.Level";
                    eventValue = (double.Parse(request.GetOption(0)) / 100d).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                break;
            case Commands.Control_Toggle:
                // TODO: not implemented yet
                break;
            case Commands.AvMedia_GetItem:
                {
                    string deviceId = request.Address;
                    string id = request.GetOption(0);
                    //
                    var objectId = new UPnPArgument("ObjectID", id);
                    var flags = new UPnPArgument("BrowseFlag", "BrowseMetadata");
                    var filter = new UPnPArgument("Filter", "upnp:album,upnp:artist,upnp:genre,upnp:title,res@size,res@duration,res@bitrate,res@sampleFrequency,res@bitsPerSample,res@nrAudioChannels,res@protocolInfo,res@protection,res@importUri");
                    var startIndex = new UPnPArgument("StartingIndex", (uint)0);
                    var requestedCount = new UPnPArgument("RequestedCount", (uint)1);
                    var sortCriteria = new UPnPArgument("SortCriteria", "");
                    //
                    var result = new UPnPArgument("Result", "");
                    var returnedNumber = new UPnPArgument("NumberReturned", "");
                    var totalMatches = new UPnPArgument("TotalMatches", "");
                    var updateId = new UPnPArgument("UpdateID", "");
                    //
                    InvokeUpnpDeviceService(device, "ContentDirectory", "Browse", new UPnPArgument[] { 
                        objectId,
                        flags,
                        filter,
                        startIndex,
                        requestedCount,
                        sortCriteria,
                        result,
                        returnedNumber,
                        totalMatches,
                        updateId
                    });
                    //
                    try
                    {
                        string ss = result.DataValue.ToString();
                        var item = XDocument.Parse(ss, LoadOptions.SetBaseUri).Descendants().Where(ii => ii.Name.LocalName == "item").First();
                        returnValue = MIG.Utility.Serialization.JsonSerialize(item, true);
                    }
                    catch
                    {
                        // TODO: MigService.Log.Error(e);
                    }
                }
                break;
            case Commands.AvMedia_GetUri:
                {
                    string deviceId = request.Address;
                    string id = request.GetOption(0);
                    //
                    var objectId = new UPnPArgument("ObjectID", id);
                    var flags = new UPnPArgument("BrowseFlag", "BrowseMetadata");
                    var filter = new UPnPArgument("Filter", "upnp:album,upnp:artist,upnp:genre,upnp:title,res@size,res@duration,res@bitrate,res@sampleFrequency,res@bitsPerSample,res@nrAudioChannels,res@protocolInfo,res@protection,res@importUri");
                    var startIndex = new UPnPArgument("StartingIndex", (uint)0);
                    var requestedCount = new UPnPArgument("RequestedCount", (uint)1);
                    var sortCriteria = new UPnPArgument("SortCriteria", "");
                    //
                    var result = new UPnPArgument("Result", "");
                    var returnedNumber = new UPnPArgument("NumberReturned", "");
                    var totalMatches = new UPnPArgument("TotalMatches", "");
                    var updateId = new UPnPArgument("UpdateID", "");
                    //
                    InvokeUpnpDeviceService(device, "ContentDirectory", "Browse", new UPnPArgument[] { 
                        objectId,
                        flags,
                        filter,
                        startIndex,
                        requestedCount,
                        sortCriteria,
                        result,
                        returnedNumber,
                        totalMatches,
                        updateId
                    });
                    //
                    try
                    {
                        string ss = result.DataValue.ToString();
                        var item = XDocument.Parse(ss, LoadOptions.SetBaseUri).Descendants().Where(ii => ii.Name.LocalName == "item").First();
                        //
                        foreach (var i in item.Elements())
                        {
                            var protocolUri = i.Attribute("protocolInfo");
                            if (protocolUri != null)
                            {
                                returnValue = new ResponseText(i.Value);
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // TODO: MigService.Log.Error(e);
                    }
                }
                break;
            case Commands.AvMedia_Browse:
                {
                    string deviceId = request.Address;
                    string id = request.GetOption(0);
                    //
                    var objectId = new UPnPArgument("ObjectID", id);
                    var flags = new UPnPArgument("BrowseFlag", "BrowseDirectChildren");
                    var filter = new UPnPArgument("Filter", "upnp:album,upnp:artist,upnp:genre,upnp:title,res@size,res@duration,res@bitrate,res@sampleFrequency,res@bitsPerSample,res@nrAudioChannels,res@protocolInfo,res@protection,res@importUri");
                    var startIndex = new UPnPArgument("StartingIndex", (uint)0);
                    var requestedCount = new UPnPArgument("RequestedCount", (uint)0);
                    var sortCriteria = new UPnPArgument("SortCriteria", "");
                    //
                    var result = new UPnPArgument("Result", "");
                    var returnedNumber = new UPnPArgument("NumberReturned", "");
                    var totalMatches = new UPnPArgument("TotalMatches", "");
                    var updateId = new UPnPArgument("UpdateID", "");
                    //
                    InvokeUpnpDeviceService(device, "ContentDirectory", "Browse", new UPnPArgument[] { 
                        objectId,
                        flags,
                        filter,
                        startIndex,
                        requestedCount,
                        sortCriteria,
                        result,
                        returnedNumber,
                        totalMatches,
                        updateId
                    });
                    //
                    try
                    {
                        string ss = result.DataValue.ToString();
                        var root = XDocument.Parse(ss, LoadOptions.SetBaseUri).Elements();
                        //
                        string jsonres = "[";
                        foreach (var i in root.Elements())
                        {
                            string itemId = i.Attribute("id").Value;
                            string itemTitle = i.Descendants().Where(n => n.Name.LocalName == "title").First().Value;
                            string itemClass = i.Descendants().Where(n => n.Name.LocalName == "class").First().Value;
                            jsonres += "{ \"Id\" : \"" + itemId + "\", \"Title\" : \"" + itemTitle.Replace("\"", "\\\"") + "\", \"Class\" : \"" + itemClass + "\" },\n";
                        }
                        jsonres = jsonres.TrimEnd(',', '\n') + "]";
                        //
                        returnValue = jsonres;
                    }
                    catch
                    {
                        // TODO: MigService.Log.Error(e);
                    }
                }
                break;
            case Commands.AvMedia_GetTransportInfo:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var transportState = new UPnPArgument("CurrentTransportState", "");
                    var transportStatus = new UPnPArgument("CurrentTransportStatus", "");
                    var currentSpeed = new UPnPArgument("CurrentSpeed", "");
                    var args = new UPnPArgument[] { 
                        instanceId,
                        transportState,
                        transportStatus,
                        currentSpeed
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "GetTransportInfo", args);
                    //
                    string jsonres = "{ ";
                    jsonres += "\"CurrentTransportState\" : \"" + transportState.DataValue + "\", ";
                    jsonres += "\"CurrentTransportStatus\" : \"" + transportStatus.DataValue + "\", ";
                    jsonres += "\"CurrentSpeed\" : \"" + currentSpeed.DataValue + "\"";
                    jsonres += " }";
                    //
                    returnValue = jsonres;
                }
                break;
            case Commands.AvMedia_GetMediaInfo:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var nrTracks = new UPnPArgument("NrTracks", (uint)0);
                    var mediaDuration = new UPnPArgument("MediaDuration", "");
                    var currentUri = new UPnPArgument("CurrentURI", "");
                    var currentUriMetadata = new UPnPArgument("CurrentURIMetaData", "");
                    var nextUri = new UPnPArgument("NextURI", "");
                    var nextUriMetadata = new UPnPArgument("NextURIMetaData", "");
                    var playMedium = new UPnPArgument("PlayMedium", "");
                    var recordMedium = new UPnPArgument("RecordMedium", "");
                    var writeStatus = new UPnPArgument("WriteStatus", "");
                    var args = new UPnPArgument[] { 
                        instanceId,
                        nrTracks,
                        mediaDuration,
                        currentUri,
                        currentUriMetadata,
                        nextUri,
                        nextUriMetadata,
                        playMedium,
                        recordMedium,
                        writeStatus
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "GetMediaInfo", args);
                    //
                    string jsonres = "{ ";
                    jsonres += "\"NrTracks\" : \"" + nrTracks.DataValue + "\", ";
                    jsonres += "\"MediaDuration\" : \"" + mediaDuration.DataValue + "\", ";
                    jsonres += "\"CurrentURI\" : \"" + currentUri.DataValue + "\", ";
                    jsonres += "\"CurrentURIMetaData\" : " + MIG.Utility.Serialization.JsonSerialize(GetJsonFromXmlItem(currentUriMetadata.DataValue.ToString())) + ", ";
                    jsonres += "\"NextURI\" : \"" + nextUri.DataValue + "\", ";
                    jsonres += "\"NextURIMetaData\" : " + MIG.Utility.Serialization.JsonSerialize(GetJsonFromXmlItem(nextUriMetadata.DataValue.ToString())) + ", ";
                    jsonres += "\"PlayMedium\" : \"" + playMedium.DataValue + "\", ";
                    jsonres += "\"RecordMedium\" : \"" + recordMedium.DataValue + "\", ";
                    jsonres += "\"WriteStatus\" : \"" + writeStatus.DataValue + "\"";
                    jsonres += " }";
                    //
                    returnValue = jsonres;
                }
                break;
            case Commands.AvMedia_GetPositionInfo:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var currentTrack = new UPnPArgument("Track", (uint)0);
                    var trackDuration = new UPnPArgument("TrackDuration", "");
                    var trackMetadata = new UPnPArgument("TrackMetaData", "");
                    var trackUri = new UPnPArgument("TrackURI", "");
                    var relativeTime = new UPnPArgument("RelTime", "");
                    var absoluteTime = new UPnPArgument("AbsTime", "");
                    var relativeCount = new UPnPArgument("RelCount", (uint)0);
                    var absoluteCount = new UPnPArgument("AbsCount", (uint)0);
                    var args = new UPnPArgument[] { 
                        instanceId,
                        currentTrack,
                        trackDuration,
                        trackMetadata,
                        trackUri,
                        relativeTime,
                        absoluteTime,
                        relativeCount,
                        absoluteCount
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "GetPositionInfo", args);
                    //
                    string jsonres = "{";
                    jsonres += "\"Track\" : \"" + currentTrack.DataValue + "\",";
                    jsonres += "\"TrackDuration\" : \"" + trackDuration.DataValue + "\",";
                    jsonres += "\"TrackMetaData\" : " + MIG.Utility.Serialization.JsonSerialize(GetJsonFromXmlItem(trackMetadata.DataValue.ToString())) + ",";
                    jsonres += "\"TrackURI\" : \"" + trackUri.DataValue + "\",";
                    jsonres += "\"RelTime\" : \"" + relativeTime.DataValue + "\",";
                    jsonres += "\"AbsTime\" : \"" + absoluteTime.DataValue + "\",";
                    jsonres += "\"RelCount\" : \"" + relativeCount.DataValue + "\",";
                    jsonres += "\"AbsCount\" : \"" + absoluteCount.DataValue + "\"";
                    jsonres += "}";
                    //
                    returnValue = jsonres;
                }
                break;
            case Commands.AvMedia_SetUri:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var currentUri = new UPnPArgument("CurrentURI", request.GetOption(0));
                    var uriMetadata = new UPnPArgument("CurrentURIMetaData", "");
                    var args = new UPnPArgument[] { 
                        instanceId,
                        currentUri,
                        uriMetadata
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "SetAVTransportURI", args);
                }
                break;
            case Commands.AvMedia_Play:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var speed = new UPnPArgument("Speed", "1");
                    var args = new UPnPArgument[] { 
                        instanceId,
                        speed
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "Play", args);
                }
                break;
            case Commands.AvMedia_Pause:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var args = new UPnPArgument[] { 
                        instanceId
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "Pause", args);
                }
                break;
            case Commands.AvMedia_Seek:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var unit = new UPnPArgument("Unit", "REL_TIME");
                    var target = new UPnPArgument("Target", request.GetOption(0));
                    var args = new UPnPArgument[] { 
                        instanceId,
                        unit,
                        target
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "Seek", args);
                }
                break;
            case Commands.AvMedia_Stop:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var args = new UPnPArgument[] { 
                        instanceId
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "Stop", args);
                }
                break;
            case Commands.AvMedia_Prev:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var args = new UPnPArgument[] { 
                        instanceId
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "Previous", args);
                }
                break;
            case Commands.AvMedia_Next:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var args = new UPnPArgument[] { 
                        instanceId
                    };
                    InvokeUpnpDeviceService(device, "AVTransport", "Next", args);
                }
                break;
            case Commands.AvMedia_GetMute:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var channel = new UPnPArgument("Channel", "Master");
                    var currentMute = new UPnPArgument("CurrentMute", "");
                    var args = new UPnPArgument[] { 
                        instanceId,
                        channel,
                        currentMute
                    };
                    InvokeUpnpDeviceService(device, "RenderingControl", "GetMute", args);
                    returnValue = new ResponseText(currentMute.DataValue.ToString());
                }
                break;
            case Commands.AvMedia_SetMute:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var channel = new UPnPArgument("Channel", "Master");
                    var mute = new UPnPArgument("DesiredMute", request.GetOption(0) == "1" ? true : false);
                    var args = new UPnPArgument[] { 
                        instanceId,
                        channel,
                        mute
                    };
                    InvokeUpnpDeviceService(device, "RenderingControl", "SetMute", args);
                }
                break;
            case Commands.AvMedia_GetVolume:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var channel = new UPnPArgument("Channel", "Master");
                    var currentVolume = new UPnPArgument("CurrentVolume", "");
                    var args = new UPnPArgument[] { 
                        instanceId,
                        channel,
                        currentVolume
                    };
                    InvokeUpnpDeviceService(device, "RenderingControl", "GetVolume", args);
                    returnValue = new ResponseText(currentVolume.DataValue.ToString());
                }
                break;
            case Commands.AvMedia_SetVolume:
                {
                    var instanceId = new UPnPArgument("InstanceID", (uint)0);
                    var channel = new UPnPArgument("Channel", "Master");
                    var volume = new UPnPArgument("DesiredVolume", UInt16.Parse(request.GetOption(0)));
                    var args = new UPnPArgument[] { 
                        instanceId,
                        channel,
                        volume
                    };
                    InvokeUpnpDeviceService(device, "RenderingControl", "SetVolume", args);
                }
                break;
            }

            // raise event
            if (raiseEvent)
            {
                OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + (device != null ? device.StandardDeviceType : "device"), eventParameter, eventValue);
            }

            return returnValue;
        }

        #endregion

        #region Public members

        public UPnP()
        {

        }

        public UpnpSmartControlPoint UpnpControlPoint
        {
            get { return controlPoint; }
        }

        /*
        public void CreateLocalDevice(
            string deviceGuid,
            string deviceType,
            string presentationUrl,
            string rootDirectory,
            string modelName,
            string modelDescription,
            string modelUrl,
            string modelNumber,
            string manufacturer,
            string manufacturerUrl
        )
        {
            if (localDevice != null)
            {
                localDevice.StopDevice();
                localDevice = null;
            }
            //
            IPHostEntry host;
            //string localIP = "";
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    //localIP = ip.ToString();
                    break;
                }
            }
            localDevice = UPnPDevice.CreateRootDevice(900, 1, rootDirectory);
            //hgdevice.Icon = null;
            if (presentationUrl != "")
            {
                localDevice.HasPresentation = true;
                localDevice.PresentationURL = presentationUrl;
            }
            localDevice.FriendlyName = modelName + ": " + Environment.MachineName;
            localDevice.Manufacturer = manufacturer;
            localDevice.ManufacturerURL = manufacturerUrl;
            localDevice.ModelName = modelName;
            localDevice.ModelDescription = modelDescription;
            if (Uri.IsWellFormedUriString(manufacturerUrl, UriKind.Absolute))
            {
                localDevice.ModelURL = new Uri(manufacturerUrl);
            }
            localDevice.ModelNumber = modelNumber;
            localDevice.StandardDeviceType = deviceType;
            localDevice.UniqueDeviceName = deviceGuid;
            localDevice.StartDevice();

        }
        */

        #endregion

        #region Private Members

        private string GetJsonFromXmlItem(String metadata)
        {
            var item = XDocument.Parse(metadata, LoadOptions.SetBaseUri).Descendants().Where(ii => ii.Name.LocalName == "item").First();
            return MIG.Utility.Serialization.JsonSerialize(item, true);
        }

        private UPnPDevice GetUpnpDevice(string deviceId)
        {
            UPnPDevice device = null;
            foreach (UPnPDevice d in controlPoint.DeviceTable)
            {
                if (d.UniqueDeviceName == deviceId)
                {
                    device = d;
                    break;
                }
            }
            return device;
        }

        private void InvokeUpnpDeviceService(UPnPDevice device, string serviceId, string methodName, params UPnPArgument[] args)
        {
            foreach (UPnPService s in device.Services)
            {
                if (s.ServiceID.StartsWith("urn:upnp-org:serviceId:" + serviceId))
                {
                    s.InvokeSync(methodName, args);
                }
            }
        }

        private void controPoint_OnAddedDevice(UpnpSmartControlPoint sender, UPnPDevice device)
        {
            if (String.IsNullOrWhiteSpace(device.StandardDeviceType))
                return;

            //foreach (UPnPService s in device.Services)
            //{
            //    s.Subscribe(1000, new UPnPService.UPnPEventSubscribeHandler(_subscribe_sink));
            //}

            lock (deviceOperationLock)
            {
                InterfaceModule module = new InterfaceModule();
                module.Domain = this.Domain;
                module.Address = device.UniqueDeviceName;
                module.Description = device.FriendlyName + " (" + device.ModelName + ")";
                if (device.StandardDeviceType == "MediaRenderer")
                {
                    module.ModuleType = MIG.ModuleTypes.MediaReceiver;
                }
                else if (device.StandardDeviceType == "MediaServer")
                {
                    module.ModuleType = MIG.ModuleTypes.MediaTransmitter;
                }
                else if (device.StandardDeviceType == "SwitchPower")
                {
                    module.ModuleType = MIG.ModuleTypes.Switch;
                }
                else if (device.StandardDeviceType == "BinaryLight")
                {
                    module.ModuleType = MIG.ModuleTypes.Light;
                }
                else if (device.StandardDeviceType == "DimmableLight")
                {
                    module.ModuleType = MIG.ModuleTypes.Dimmer;
                }
                else
                {
                    module.ModuleType = MIG.ModuleTypes.Sensor;
                }
                module.CustomData = new DeviceHolder() { Device = device, Initialized = false };
                modules.Add(module);
                //
                OnInterfacePropertyChanged(this.GetDomain(), "1", "DLNA/UPnP Controller", "Controller.Status", "Added node " + module.Description);
            }
            OnInterfaceModulesChanged(this.GetDomain());
        }

        private void controPoint_OnRemovedDevice(UpnpSmartControlPoint sender, UPnPDevice device)
        {
            lock (deviceOperationLock)
            {
                var module = modules.Find(m => m.Address == device.UniqueDeviceName);
                if (module != null)
                {
                    OnInterfacePropertyChanged(this.GetDomain(), "1", "DLNA/UPnP Controller", "Controller.Status", "Removed node " + module.Description);
                    modules.Remove(module);
                    OnInterfaceModulesChanged(this.GetDomain());
                }
            }
        }

        private void controPoint_OnDeviceExpired(UpnpSmartControlPoint sender, UPnPDevice device)
        {
            lock (deviceOperationLock)
            {
                var module = modules.Find(m => m.Address == device.UniqueDeviceName);
                if (module != null)
                {
                    OnInterfacePropertyChanged(this.GetDomain(), "1", "DLNA/UPnP Controller", "Controller.Status", "Removed node " + module.Description);
                    modules.Remove(module);
                    OnInterfaceModulesChanged(this.GetDomain());
                }
            }
        }

        //        private void _subscribe_sink(UPnPService sender, bool SubscribeOK)
        //        {
        //Console.WriteLine("\n\n\n" + sender.ServiceURN + "\n\n\n");
        //            if (SubscribeOK)
        //            {
        //                sender.OnUPnPEvent += sender_OnUPnPEvent;
        //            }
        //        }

        //        void sender_OnUPnPEvent(UPnPService sender, long SEQ)
        //        {
        //Console.WriteLine("\n\n\n" + sender.ServiceURN + " - " + SEQ + "\n\n\n");
        //        }

        private void UpdateDeviceProperties()
        {
            lock (deviceOperationLock)
            {
                for (int d = 0; d < modules.Count; d++)
                {
                    var module = modules[d];
                    var deviceHolder = module.CustomData as DeviceHolder;
                    if (deviceHolder.Initialized)
                        continue;
                    deviceHolder.Initialized = true;
                    var device = deviceHolder.Device;
                    if (!String.IsNullOrWhiteSpace(device.StandardDeviceType))
                    {
                        OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.DeviceType", device.StandardDeviceType);
                        OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.Version", device.Major + "." + device.Minor);
                        // TODO: the following events are HG specific and should be moved somehow into HG code
                        if (device.StandardDeviceType == "MediaRenderer")
                        {
                            module.ModuleType = MIG.ModuleTypes.MediaReceiver;
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "Widget.DisplayModule", "homegenie/generic/mediareceiver");
                        }
                        else if (device.StandardDeviceType == "MediaServer")
                        {
                            module.ModuleType = MIG.ModuleTypes.MediaTransmitter;
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "Widget.DisplayModule", "homegenie/generic/mediaserver");
                        }
                        else if (device.StandardDeviceType == "SwitchPower")
                        {
                            module.ModuleType = MIG.ModuleTypes.Switch;
                        }
                        else if (device.StandardDeviceType == "BinaryLight")
                        {
                            module.ModuleType = MIG.ModuleTypes.Light;
                        }
                        else if (device.StandardDeviceType == "DimmableLight")
                        {
                            module.ModuleType = MIG.ModuleTypes.Dimmer;
                        }
                        else if (device.HasPresentation && device.PresentationURL != null)
                        {
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "Widget.DisplayModule", "homegenie/generic/link");
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "FavouritesLink.Url", device.PresentationURL);
                        }               
                        if (!String.IsNullOrWhiteSpace(device.DeviceURN))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.DeviceURN", device.DeviceURN);
                        if (!String.IsNullOrWhiteSpace(device.DeviceURN_Prefix))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.DeviceURN_Prefix", device.DeviceURN_Prefix);
                        if (!String.IsNullOrWhiteSpace(device.FriendlyName))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.FriendlyName", device.FriendlyName);
                        if (!String.IsNullOrWhiteSpace(device.LocationURL))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.LocationURL", device.LocationURL);
                        if (!String.IsNullOrWhiteSpace(device.ModelName))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.ModelName", device.ModelName);
                        if (!String.IsNullOrWhiteSpace(device.ModelNumber))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.ModelNumber", device.ModelNumber);
                        if (!String.IsNullOrWhiteSpace(device.ModelDescription))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.ModelDescription", device.ModelDescription);
                        if (device.ModelURL != null)
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.ModelURL", device.ModelURL.ToString());
                        if (!String.IsNullOrWhiteSpace(device.Manufacturer))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.Manufacturer", device.Manufacturer);
                        if (!String.IsNullOrWhiteSpace(device.ManufacturerURL))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.ManufacturerURL", device.ManufacturerURL);
                        if (!String.IsNullOrWhiteSpace(device.PresentationURL))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.PresentationURL", device.PresentationURL);
                        if (!String.IsNullOrWhiteSpace(device.UniqueDeviceName))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.UniqueDeviceName", device.UniqueDeviceName);
                        if (!String.IsNullOrWhiteSpace(device.SerialNumber))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.SerialNumber", device.SerialNumber);
                        if (!String.IsNullOrWhiteSpace(device.StandardDeviceType))
                            OnInterfacePropertyChanged(this.GetDomain(), device.UniqueDeviceName, "UPnP " + device.FriendlyName, "UPnP.StandardDeviceType", device.StandardDeviceType);
                    }
                }
            }
        }


        #region Events

        protected virtual void OnInterfaceModulesChanged(string domain)
        {
            if (InterfaceModulesChanged != null)
            {
                var args = new InterfaceModulesChangedEventArgs(domain);
                InterfaceModulesChanged(this, args);
            }
        }

        protected virtual void OnInterfacePropertyChanged(string domain, string source, string description, string propertyPath, object propertyValue)
        {
            if (InterfacePropertyChanged != null)
            {
                var args = new InterfacePropertyChangedEventArgs(domain, source, description, propertyPath, propertyValue);
                InterfacePropertyChanged(this, args);
            }
        }

        #endregion

        #endregion

    }

    #region UpnpSmartControlPoint helper class

    // adapted from:
    // https://code.google.com/p/phanfare-tools/
    // http://phanfare-tools.googlecode.com/svn/trunk/Phanfare.MediaServer/UPnP/Intel/UPNP/UPnPInternalSmartControlPoint.cs

    #endregion

}
