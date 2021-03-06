﻿using System;
using System.Globalization;
using System.IO;
using BBox3Tool.enums;
using BBox3Tool.objects;

namespace BBox3Tool.session
{
    internal class FritzBoxSession : IModemSession
    {
        private VDSL2Profile _vdslProfile;

        private TelnetConnection _tc;

        public FritzBoxSession()
	    {
            DeviceName = "Fritz!Box 7390";
            DSLStandard = DSLStandard.unknown;
            Distance = null;
            VectoringDown = false;
            VectoringUp = false;
            VectoringDeviceCapable = true;
            VectoringROPCapable = null;
	    }

        //TODO FritzBox:
        //- check vectoring down : G.INP down > 10
        //- check vectoring down : G.INP up > 10
        //- check vectoring ROP capable : Far-end ITU Vendor Id = Broadcom

        public bool OpenSession(string host, string username, string password)
        {
            try
            {
                // New connection
                _tc = new TelnetConnection(host, 23);

                // Login
                var passwordPrompt = _tc.Read(200);
                if (passwordPrompt.Contains("password:"))
                {
                    _tc.WriteLine(password);
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool CloseSession()
        {
            // Close session if still connected
            if (_tc.IsConnected)
            {
                _tc.WriteLine("^C");
                _tc.WriteLine("exit");
            }

            // Kill socket
            _tc.CloseConnection();

            return true;
        }

        public void RefreshData() { 
            //not implemented
        }

        public void GetLineData()
        {
            // Exec 'vdsl' command
            if (_tc.Read(500).EndsWith("# "))
            {
                _tc.WriteLine("vdsl");
            }

            // Wait for cpe prompt
            if (_tc.Read(1000).EndsWith("cpe>"))
            {
                // Request extended port status
                _tc.WriteLine("11");
            }

            // Read reply
            var extendedPortStatusReply = _tc.Read(2000);
            if (extendedPortStatusReply.Contains("Far-end ITU Vendor Id"))
            {
                // Parse results
                ParsePortStatus(extendedPortStatusReply);
            }
            else
            {
                throw new Exception("Unable to read extended port status. Try rebooting the Fritz!Box.");
            }

            // Wait for cpe prompt
            if (extendedPortStatusReply.EndsWith("cpe> "))
            {
                // Request near-end SNR margin and attenuation
                _tc.WriteLine("13");
            }

            // Read reply
            var getsnrReply = _tc.Read(2000);
            if (getsnrReply.Contains("Attenuation"))
            {
                // Parse results
                ParseVdslSnr(getsnrReply);
            }
        }

        private void ParsePortStatus(string extendedPortStatus)
        {
            var reader = new StringReader(extendedPortStatus);
            while (true)
            {
                var line = reader.ReadLine();
                if (line != null)
                {
                    var array = line.Split(':');
                    switch (array[0])
                    {
                        case "Bearer1 Downstream payload rate":
                            var dsCurrentBitRate = array[1].Trim().Replace(" kbps", "");
                            DownstreamCurrentBitRate = Convert.ToInt32(dsCurrentBitRate);
                            break;
                        case "Bearer1 Upstream payload rate":
                            var usCurrentBitRate = array[1].Trim().Replace(" kbps", "");
                            UpstreamCurrentBitRate = Convert.ToInt32(usCurrentBitRate);
                            break;
                        case "Downstream attainable payload rate":
                            var dsMaxBitRate = array[1].Trim().Replace(" kbps", "");
                            DownstreamMaxBitRate = Convert.ToInt32(dsMaxBitRate);
                            break;
                        case "Downstream Training Margin":
                            var dsAttenuation = array[1].Trim().Replace(" dB", "");
                            DownstreamAttenuation = Convert.ToDecimal(dsAttenuation, CultureInfo.InvariantCulture);
                            break;
                        case "Bandplan Type...........":
                            _vdslProfile = VDSL2Profile.p8d;
                            if (array[1].Trim().Equals("0"))
                            {
                                _vdslProfile = VDSL2Profile.p17a;
                            }
                            break;
                        case "Far-end ITU Vendor Id":
                            break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private void ParseVdslSnr(string snr)
        {
            var reader = new StringReader(snr);
            while (true)
            {
                var line = reader.ReadLine();
                if (line != null)
                {
                    var array = line.Split(':');
                    switch (array[0])
                    {
                        case "Attenuation":
                            var dsNoiseMargin = array[1].Trim().Replace(" dB", "");
                            DownstreamNoiseMargin = Convert.ToDecimal(dsNoiseMargin, CultureInfo.InvariantCulture);
                            break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public DeviceInfo GetDeviceInfo()
        {
            var deviceInfo = new DeviceInfo();
            return deviceInfo;
        }

        public string GetDebugValue(string debugValue)
        {
            return "Not implemented yet!";
        }

        public bool LineConnected
        {
            get
            {
                //TODO implementation
                return true;
            }
        }

        public int DownstreamCurrentBitRate { get; private set; }

        public int UpstreamCurrentBitRate { get; private set; }

        public int DownstreamMaxBitRate { get; private set; }

        public int UpstreamMaxBitRate { get; private set; }

        public decimal DownstreamAttenuation { get; private set; }

        public decimal UpstreamAttenuation { get; private set; }

        public decimal DownstreamNoiseMargin { get; private set; }

        public decimal UpstreamNoiseMargin { get; private set; }

        public decimal? Distance { get; private set; }

        public string DeviceName { get; private set; }

        public bool VectoringDown { get; private set; }

        public bool VectoringUp { get; private set; }
        
        public bool VectoringDeviceCapable { get; private set; }

        public bool? VectoringROPCapable { get; private set; }

        public DSLStandard DSLStandard { get; private set; }
    }
}