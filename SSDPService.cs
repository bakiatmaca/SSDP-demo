/* 30-06-2013 Baki Turan Atmaca
 * http://bakiatmaca.com/
 * bakituranatmaca@gmail.com
 */
using System;
using System.Threading;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace com.lxport.net.ssdp
{
    public class SSDPService
    {

        protected const string SSDP_SENDMESSAGE = "HTTP/1.1 200 OK\r"
                                                    + "Host:239.255.255.250:1900\r"
                                                    + "NT:urn:schemas-upnp-org:service:{SERVICENAME}\r"
                                                    + "Location:{SERVICEURLINFO}\r"
                                                    + "Server:{SERVERINFO}\r"
                                                    + "\n";

        protected const string SSDP_SEARCHMESSAGE = "M-SEARCH * HTTP/1.1\r\n"
                                                    + "HOST:239.255.255.250:1900\r\n"
                                                    + "MAN:\"ssdp:discover\"\r\n"
                                                    + "ST:ssdp:{SERVICENAME}\r\n";

        public const string SSDP_MULTICASTIPADRESS = "239.255.255.250";
        public const int SSDP_PORT = 1900;

        protected UdpClient _udpclient;
        protected Thread receiveThread;
        protected Thread selfannouncementThread;

        protected string _ServiceName = "lxport-ssdpservice-demo-server-v1.0";
        protected string ServiceName
        {
            get { return _ServiceName; }
            set { _ServiceName = value; }
        }

        protected bool _IsRunning;
        public bool IsRunning
        {
            get { return _IsRunning; }
        }

        protected bool _IsSelfAnnouncement;
        public bool IsSelfAnnouncement
        {
            get { return _IsSelfAnnouncement; }
        }

        protected int _ReTryMessage;
        public int ReTryMessage
        {
            get { return _ReTryMessage; }
            set { _ReTryMessage = value; }
        }

        protected int _DelayRetry;
        public int DelayRetry
        {
            get { return _DelayRetry; }
            set { _DelayRetry = value; }
        }

        protected int _SelfAnnouncementDelay;
        public int SelfAnnouncementDelay
        {
            get { return _SelfAnnouncementDelay; }
            set { _SelfAnnouncementDelay = value; }
        }
        
        protected bool _multicastLoopback;
        public bool MulticastLoopback
        {
            set { _multicastLoopback = value; }
        }

        protected string _multicastadress;
        protected int _port;
        protected string _serviceurlinfo;

        protected string _ssdp_sendmessage;
        protected string _ssdp_searchmessage;

        private bool _isDebugMode;
        public bool IsDebugMode
        {
            get { return _isDebugMode; }
            set { _isDebugMode = value; }
        }

        public SSDPService(string ServiceURLInfo)
        {
            _isDebugMode = false;
            _multicastLoopback = false;
            _serviceurlinfo = ServiceURLInfo;
            _multicastadress = SSDP_MULTICASTIPADRESS;
            _port = SSDP_PORT;

            _ssdp_sendmessage = SSDP_SENDMESSAGE;
            _ssdp_searchmessage = SSDP_SEARCHMESSAGE;

            _ReTryMessage = 3;
            _DelayRetry = 0;
            _IsRunning = false;
            _IsSelfAnnouncement = false;
            _SelfAnnouncementDelay = 1000;
        }

        public SSDPService(string ServiceURLInfo, string MulticastAdress, int Port)
            : this(ServiceURLInfo)
        {
            _multicastadress = MulticastAdress;
            _port = Port;
        }

        public SSDPService(string ServiceURLInfo, string MulticastAdress, int Port, string SSDPSendMessage, string SSDPSearchMessage)
            : this(ServiceURLInfo)
        {
            _multicastadress = MulticastAdress;
            _port = Port;

            _ssdp_sendmessage = SSDPSendMessage;
            _ssdp_searchmessage = SSDPSearchMessage;
        }

        public void Start()
        {
            if (!_IsRunning)
            {
                _ssdp_sendmessage = _ssdp_sendmessage.Replace("{SERVICENAME}", _ServiceName);
                _ssdp_sendmessage = _ssdp_sendmessage.Replace("{SERVERINFO}", Environment.OSVersion.VersionString);
                _ssdp_sendmessage = _ssdp_sendmessage.Replace("{SERVICEURLINFO}", _serviceurlinfo);

                _ssdp_searchmessage = _ssdp_searchmessage.Replace("{SERVICENAME}", _ServiceName);

                try
                {
                    _udpclient = new UdpClient(_port);

                    IPAddress multicastaddress = IPAddress.Parse(_multicastadress);
                    _udpclient.JoinMulticastGroup(multicastaddress);
                    _udpclient.EnableBroadcast = true;
                    _udpclient.MulticastLoopback = _multicastLoopback;

                    _IsRunning = true;
                }
                catch (Exception ex)
                {
                    _IsRunning = false;

                    if (IsDebugMode)
                        Console.WriteLine("Start Error - " + ex.ToString());
                }

                if (_IsRunning)
                {
                    receiveThread = new Thread(ServiceInfoAnnouncement);
                    receiveThread.Start();
                }
            }
        }

        public void StartSelfAnnouncement()
        {
            try
            {
                if (!_IsSelfAnnouncement)
                {
                    _IsSelfAnnouncement = true;
                    selfannouncementThread = new Thread(SelfServiceInfoAnnouncement);
                    selfannouncementThread.Start();
                }
            }
            catch
            {
                _IsSelfAnnouncement = false;
            }
        }

        public void Stop()
        {
            _IsRunning = false;

            try
            {
                //PORCELY
                _udpclient.Close();
            }
            catch
            { }

            try
            {
                //PORCELY
                if (receiveThread != null)
                    selfannouncementThread.Abort();
            }
            catch
            { }

        }

        private void ServiceInfoAnnouncement()
        {
            try
            {
                while (_IsRunning)
                {
                    IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Broadcast, _port);

                    byte[] data = _udpclient.Receive(ref ipEndPoint);

                    string message = ASCIIEncoding.Default.GetString(data);

                    // Raise the AfterReceive event
                    if ((message != null) &&
                        (message.StartsWith(_ssdp_searchmessage)))
                    {
                        if (IsDebugMode)
                            Console.WriteLine("Receive udp multicast\nfrom: {0}\ndata: \n{1}", ipEndPoint.Address.ToString(), message);

                        message = null;

                        //SendSSDPMessage
                        SendSSDPMessage(_ssdp_sendmessage, _ReTryMessage, _DelayRetry);

                        if (IsDebugMode)
                            Console.WriteLine("Send udp multicast {0} times\ndata: \n{1}", _ReTryMessage.ToString(), _ssdp_sendmessage);
                    }
                }

                _udpclient.Close();
            }
            catch (Exception e)
            {
                _IsRunning = false;

                if (IsDebugMode)
                    Console.WriteLine("ServiceInfoAnnouncement Error - " + e.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendSSDPMessage(string ssdpmessage, int retrymessage, int delayretry)
        {
            if (_IsRunning)
            {
                IPEndPoint remoteep = new IPEndPoint(IPAddress.Broadcast, _port);
                Byte[] buffer = new Byte[ssdpmessage.Length];
                buffer = Encoding.UTF8.GetBytes(ssdpmessage);

                for (int i = 0; i < retrymessage; i++)
                {
                    //Send
                    _udpclient.Send(buffer, buffer.Length, remoteep);

                    if (delayretry > 0)
                        Thread.Sleep(delayretry);
                }

                if (IsDebugMode)
                    Console.WriteLine("Send udp multicast {0} times\nfrom: {1}\ndata: \n{2}", ReTryMessage.ToString(), remoteep.Address.ToString(), ssdpmessage);
            }
        }

        public void SelfServiceInfoAnnouncement()
        {
            while (_IsRunning && _IsSelfAnnouncement)
            {
                try
                {
                    SendSSDPMessage(_ssdp_sendmessage, _ReTryMessage, _SelfAnnouncementDelay);
                }
                catch (Exception e)
                {
                    _IsSelfAnnouncement = false;

                    if (IsDebugMode)
                        Console.WriteLine("SelfServiceInfoAnnouncement Error - " + e.ToString());
                }
            }

            _IsSelfAnnouncement = false;
        }

    }
}
