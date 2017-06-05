﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Sres.Net.EEIP
{
    public class EEIPClient
    {
        TcpClient client;
        NetworkStream stream;
        UInt32 sessionHandle;
        UInt32 connectionID_O_T;
        UInt32 connectionID_T_O;
        UInt16 connectionSerialNumber;
        public ushort TCPPort { get; set; } = 0xAF12;
        public ushort UDPPort { get; set; } = 0x08AE;
        public string IPAddress { get; set; } = "172.0.0.1";
        public UInt32 RequestedPacketRate_O_T { get; set; } = 0x7A120;      //500ms
        public UInt32 RequestedPacketRate_T_O { get; set; } = 0x7A120;      //500ms
        public bool O_T_OwnerRedundant { get; set; } = true;                //For Forward Open
        public bool T_O_OwnerRedundant { get; set; } = true;                //For Forward Open
        public bool O_T_VariableLength { get; set; } = true;                //For Forward Open
        public bool T_O_VariableLength { get; set; } = true;                //For Forward Open
        public UInt16 O_T_Length { get; set; } = 505;                //For Forward Open - Max 505
        public UInt16 T_O_Length { get; set; } = 505;                //For Forward Open - Max 505
        public ConnectionType O_T_ConnectionType { get; set; } = ConnectionType.Point_to_Point;
        public ConnectionType T_O_ConnectionType { get; set; } = ConnectionType.Multicast;
        public Priority O_T_Priority { get; set; } = Priority.Scheduled;
        public Priority T_O_Priority { get; set; } = Priority.Scheduled;
        public byte O_T_InstanceID { get; set; } = 0x64;               //Ausgänge
        public byte T_O_InstanceID { get; set; } = 0x65;               //Eingänge
        public byte[] O_T_IOData = new byte[505];   //Class 1 Real-Time IO-Data O->T   
        public byte[] T_O_IOData = new byte[505];    //Class 1 Real-Time IO-Data T->O  
        public RealTimeFormat O_T_RealTimeFormat { get; set; } = RealTimeFormat.Header32Bit;
        public RealTimeFormat T_O_RealTimeFormat { get; set; } = RealTimeFormat.Modeless;

        private void ReceiveCallback(IAsyncResult ar)
        {
            
            UdpClient u = (UdpClient)((UdpState)(ar.AsyncState)).u;
            var asyncResult = u.BeginReceive(new AsyncCallback(ReceiveCallback), (UdpState)(ar.AsyncState));
            System.Net.IPEndPoint e = (System.Net.IPEndPoint)((UdpState)(ar.AsyncState)).e;

            Byte[] receiveBytes = u.EndReceive(ar, ref e);
            string receiveString = Encoding.ASCII.GetString(receiveBytes);

            // EndReceive worked and we have received data and remote endpoint
            if (receiveBytes.Length > 0)
            {
                UInt16 command = Convert.ToUInt16(receiveBytes[0]
                                            | (receiveBytes[1] << 8));
                if (command == 0x63)
                {
                    returnList.Add(Encapsulation.CIPIdentityItem.getCIPIdentityItem(24, receiveBytes));
                }
            }

        }
        public class UdpState
        {
            public System.Net.IPEndPoint e;
            public UdpClient u;

        }

        List<Encapsulation.CIPIdentityItem> returnList = new List<Encapsulation.CIPIdentityItem>();
        /// <summary>
        /// List and identify potential targets. This command shall be sent as braodcast massage using UDP.
        /// </summary>
        /// <returns>List<Encapsulation.CIPIdentityItem> contains the received informations from all devices </returns>	
        public List<Encapsulation.CIPIdentityItem> ListIdentity()
        {
            
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {

                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            System.Net.IPAddress mask = ip.IPv4Mask;
                            System.Net.IPAddress address = ip.Address;

                            String multicastAddress = (address.GetAddressBytes()[0] | (~(mask.GetAddressBytes()[0])) & 0xFF).ToString() + "." + (address.GetAddressBytes()[1] | (~(mask.GetAddressBytes()[1])) & 0xFF).ToString() + "." + (address.GetAddressBytes()[2] | (~(mask.GetAddressBytes()[2])) & 0xFF).ToString() + "." + (address.GetAddressBytes()[3] | (~(mask.GetAddressBytes()[3])) & 0xFF).ToString();

                            byte[] sendData = new byte[24];
                            sendData[0] = 0x63;               //Command for "ListIdentity"
                            System.Net.Sockets.UdpClient udpClient = new System.Net.Sockets.UdpClient();
                            System.Net.IPEndPoint endPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(multicastAddress), 44818);
                            udpClient.Send(sendData, sendData.Length, endPoint);

                            UdpState s = new UdpState();
                            s.e = endPoint;
                            s.u = udpClient;

                            var asyncResult = udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), s);

                            System.Threading.Thread.Sleep(1000);
                            
                            /*asyncResult.AsyncWaitHandle.WaitOne(2000);

                            while (true)
                            {
                                if (asyncResult.IsCompleted)
                                {
                                    try
                                    {
                                        System.Net.IPEndPoint remoteEP = null;
                                        byte[] receivedData = udpClient.EndReceive(asyncResult, ref remoteEP);
                                        // EndReceive worked and we have received data and remote endpoint
                                        if (receivedData.Length > 0)
                                        {
                                            UInt16 command = Convert.ToUInt16(receivedData[0]
                                                                        | (receivedData[1] << 8));
                                            if (command == 0x63)
                                            {
                                                returnList.Add(Encapsulation.CIPIdentityItem.getCIPIdentityItem(24, receivedData));
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        break;
                                    }
                                }
                                else
                                    break;
                            }*/

                        }
                    }
                }
            }
            return returnList;
        }

        /// <summary>
        /// Sends a RegisterSession command to a target to initiate session
        /// </summary>
        /// <param name="address">IP-Address of the target device</param> 
        /// <param name="port">Port of the target device (default should be 0xAF12)</param> 
        /// <returns>Session Handle</returns>	
        public UInt32 RegisterSession(UInt32 address, UInt16 port)
        {
            Encapsulation encapsulation = new Encapsulation();
            encapsulation.Command = Encapsulation.CommandsEnum.RegisterSession;
            encapsulation.Length = 4;
            encapsulation.CommandSpecificData.Add(1);       //Protocol version (should be set to 1)
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);       //Session options shall be set to "0"
            encapsulation.CommandSpecificData.Add(0);


            string ipAddress = Encapsulation.CIPIdentityItem.getIPAddress(address);
            this.IPAddress = ipAddress;
            client = new TcpClient(ipAddress, port);
            stream = client.GetStream();

            stream.Write(encapsulation.toBytes(), 0, encapsulation.toBytes().Length);
            byte[] data = new Byte[256];

            Int32 bytes = stream.Read(data, 0, data.Length);

            UInt32 returnvalue = (UInt32)data[4] + (((UInt32)data[5]) << 8) + (((UInt32)data[6]) << 16) + (((UInt32)data[7]) << 24);
            this.sessionHandle = returnvalue;
            return returnvalue;
        }

        /// <summary>
        /// Sends a UnRegisterSession command to a target to terminate session
        /// </summary> 
        public void UnRegisterSession()
        {
            Encapsulation encapsulation = new Encapsulation();
            encapsulation.Command = Encapsulation.CommandsEnum.UnRegisterSession;
            encapsulation.Length = 0;
            encapsulation.SessionHandle =  sessionHandle;
 
            stream.Write(encapsulation.toBytes(), 0, encapsulation.toBytes().Length);
            byte[] data = new Byte[256];
            client.Close();
            stream.Close();
            sessionHandle = 0;
        }

        System.Net.Sockets.UdpClient udpClientReceive;
        bool udpClientReceiveClosed = false;
        public void ForwardOpen()
        {
            udpClientReceiveClosed = false;
            ushort o_t_headerOffset = 2;                    //Zählt den Sequencecount und evtl 32bit header zu der Länge dazu
            if (O_T_RealTimeFormat == RealTimeFormat.Header32Bit)
                o_t_headerOffset = 6;
            if (O_T_RealTimeFormat == RealTimeFormat.Heartbeat)
                o_t_headerOffset = 0;

            ushort t_o_headerOffset = 2;                    //Zählt den Sequencecount und evtl 32bit header zu der Länge dazu
            if (T_O_RealTimeFormat == RealTimeFormat.Header32Bit)
                t_o_headerOffset = 6;
            if (T_O_RealTimeFormat == RealTimeFormat.Heartbeat)
                t_o_headerOffset = 0;

            int lengthOffset = (5 + (O_T_ConnectionType == ConnectionType.Null ? 0 : 2) + (T_O_ConnectionType == ConnectionType.Null ? 0 : 2));

            Encapsulation encapsulation = new Encapsulation();
            encapsulation.SessionHandle = sessionHandle;
            encapsulation.Command = Encapsulation.CommandsEnum.SendRRData;
            encapsulation.Length = (ushort)(57 + (ushort)lengthOffset);
            //---------------Interface Handle CIP
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Interface Handle CIP

            //----------------Timeout
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Timeout

            //Common Packet Format (Table 2-6.1)
            Encapsulation.CommonPacketFormat commonPacketFormat = new Encapsulation.CommonPacketFormat();
            commonPacketFormat.ItemCount = 0x02;

            commonPacketFormat.AddressItem = 0x0000;        //NULL (used for UCMM Messages)
            commonPacketFormat.AddressLength = 0x0000;

            
            commonPacketFormat.DataItem = 0xB2;
            commonPacketFormat.DataLength = (ushort)(41 + (ushort)lengthOffset);



            //----------------CIP Command "Forward Open"
            commonPacketFormat.Data.Add(0x54);
            //----------------CIP Command "Forward Open"

            //----------------Requested Path size
            commonPacketFormat.Data.Add(2);
            //----------------Requested Path size

            //----------------Path segment for Class ID
            commonPacketFormat.Data.Add(0x20);
            commonPacketFormat.Data.Add((byte)6);
            //----------------Path segment for Class ID

            //----------------Path segment for Instance ID
            commonPacketFormat.Data.Add(0x24);
            commonPacketFormat.Data.Add((byte)1);
            //----------------Path segment for Instace ID

            //----------------Priority and Time/Tick - Table 3-5.16 (Vol. 1)
            commonPacketFormat.Data.Add(0x03);
            //----------------Priority and Time/Tick

            //----------------Timeout Ticks - Table 3-5.16 (Vol. 1)
            commonPacketFormat.Data.Add(0xfa);
            //----------------Timeout Ticks

            this.connectionID_O_T = Convert.ToUInt32(new Random().Next(0xfffffff));
            this.connectionID_T_O = Convert.ToUInt32(new Random().Next(0xfffffff)+1);
            commonPacketFormat.Data.Add((byte)connectionID_O_T);
            commonPacketFormat.Data.Add((byte)(connectionID_O_T >> 8));
            commonPacketFormat.Data.Add((byte)(connectionID_O_T >> 16));
            commonPacketFormat.Data.Add((byte)(connectionID_O_T >> 24));


            commonPacketFormat.Data.Add((byte)connectionID_T_O);
            commonPacketFormat.Data.Add((byte)(connectionID_T_O >> 8));
            commonPacketFormat.Data.Add((byte)(connectionID_T_O >> 16));
            commonPacketFormat.Data.Add((byte)(connectionID_T_O >> 24));

            this.connectionSerialNumber = Convert.ToUInt16(new Random().Next(0xFFFF)+2);
            commonPacketFormat.Data.Add((byte)connectionSerialNumber);
            commonPacketFormat.Data.Add((byte)(connectionSerialNumber >> 8));

            //----------------Originator Vendor ID
            commonPacketFormat.Data.Add(0xFF);
            commonPacketFormat.Data.Add(0);
            //----------------Originaator Vendor ID

            //----------------Originator Serial Number
            commonPacketFormat.Data.Add(0xFF);
            commonPacketFormat.Data.Add(0xFF);
            commonPacketFormat.Data.Add(0xFF);
            commonPacketFormat.Data.Add(0xFF);
            //----------------Originator Serial Number

            //----------------Timeout Multiplier
            commonPacketFormat.Data.Add(0);
            //----------------Timeout Multiplier

            //----------------Reserved
            commonPacketFormat.Data.Add(0);
            commonPacketFormat.Data.Add(0);
            commonPacketFormat.Data.Add(0);
            //----------------Reserved

            //----------------Requested Packet Rate O->T in Microseconds
            commonPacketFormat.Data.Add((byte)RequestedPacketRate_O_T);
            commonPacketFormat.Data.Add((byte)(RequestedPacketRate_O_T >> 8));
            commonPacketFormat.Data.Add((byte)(RequestedPacketRate_O_T >> 16));
            commonPacketFormat.Data.Add((byte)(RequestedPacketRate_O_T >> 24));
            //----------------Requested Packet Rate O->T in Microseconds

            //----------------O->T Network Connection Parameters
            bool redundantOwner = (bool)O_T_OwnerRedundant;
            byte connectionType = (byte)O_T_ConnectionType; //1=Multicast, 2=P2P
            byte priority = (byte)O_T_Priority;         //00=low; 01=High; 10=Scheduled; 11=Urgent
            bool variableLength = O_T_VariableLength;       //0=fixed; 1=variable
            UInt16 connectionSize = (ushort)(O_T_Length + o_t_headerOffset);      //The maximum size in bytes og the data for each direction (were applicable) of the connection. For a variable -> maximum
            UInt16 NetworkConnectionParameters = (UInt16)((UInt16)(connectionSize & 0x1FF) | ((Convert.ToUInt16(variableLength)) << 9) | ((priority & 0x03) << 10) | ((connectionType & 0x03) << 13) | ((Convert.ToUInt16(redundantOwner)) << 15));
            commonPacketFormat.Data.Add((byte)NetworkConnectionParameters);
            commonPacketFormat.Data.Add((byte)(NetworkConnectionParameters >> 8));
            //----------------O->T Network Connection Parameters

            //----------------Requested Packet Rate T->O in Microseconds
            commonPacketFormat.Data.Add((byte)RequestedPacketRate_T_O);
            commonPacketFormat.Data.Add((byte)(RequestedPacketRate_T_O >> 8));
            commonPacketFormat.Data.Add((byte)(RequestedPacketRate_T_O >> 16));
            commonPacketFormat.Data.Add((byte)(RequestedPacketRate_T_O >> 24));
            //----------------Requested Packet Rate T->O in Microseconds

            //----------------T->O Network Connection Parameters


            redundantOwner = (bool)T_O_OwnerRedundant;
            connectionType = (byte)T_O_ConnectionType; //1=Multicast, 2=P2P
            priority = (byte)T_O_Priority;
            variableLength = T_O_VariableLength;
            connectionSize = (byte)(T_O_Length  + t_o_headerOffset);
            NetworkConnectionParameters = (UInt16)((UInt16)(connectionSize & 0x1FF) | ((Convert.ToUInt16(variableLength)) << 9) | ((priority & 0x03) << 10) | ((connectionType & 0x03) << 13) | ((Convert.ToUInt16(redundantOwner)) << 15));
            commonPacketFormat.Data.Add((byte)NetworkConnectionParameters);
            commonPacketFormat.Data.Add((byte)(NetworkConnectionParameters >> 8));
            //----------------T->O Network Connection Parameters

            //----------------Transport Type/Trigger
            commonPacketFormat.Data.Add(0x01);
            //X------- = 0= Client; 1= Server
            //-XXX---- = Production Trigger, 0 = Cyclic, 1 = CoS, 2 = Application Object
            //----XXXX = Transport class, 0 = Class 0, 1 = Class 1, 2 = Class 2, 3 = Class 3
            //----------------Transport Type Trigger
            //Connection Path size 
            commonPacketFormat.Data.Add((byte)((0x2) + (O_T_ConnectionType == ConnectionType.Null ? 0 : 1) + (T_O_ConnectionType == ConnectionType.Null ? 0 : 1) ));
            //Verbindugspfad
            commonPacketFormat.Data.Add((byte)(0x20));
            commonPacketFormat.Data.Add((byte)(0x4));
            commonPacketFormat.Data.Add((byte)(0x24));
            commonPacketFormat.Data.Add((byte)(0x01));
            if (O_T_ConnectionType != ConnectionType.Null)
            {
                commonPacketFormat.Data.Add((byte)(0x2C));
                commonPacketFormat.Data.Add((byte)(O_T_InstanceID));
            }
            if (T_O_ConnectionType != ConnectionType.Null)
            {
                commonPacketFormat.Data.Add((byte)(0x2C));
                commonPacketFormat.Data.Add((byte)(T_O_InstanceID));
            }

            //20 04 24 01 2C 65 2C 6B

            byte[] dataToWrite = new byte[encapsulation.toBytes().Length + commonPacketFormat.toBytes().Length];
            System.Buffer.BlockCopy(encapsulation.toBytes(), 0, dataToWrite, 0, encapsulation.toBytes().Length);
            System.Buffer.BlockCopy(commonPacketFormat.toBytes(), 0, dataToWrite, encapsulation.toBytes().Length, commonPacketFormat.toBytes().Length);
            encapsulation.toBytes();

            stream.Write(dataToWrite, 0, dataToWrite.Length);
            byte[] data = new Byte[512];

            Int32 bytes = stream.Read(data, 0, data.Length);

            //--------------------------BEGIN Error?
            if (data[42] != 0)      //Exception codes see "Table B-1.1 CIP General Status Codes"
            {
                switch (data[42])
                {
                    
                    case 0x1: if (data[43] == 0)
                                throw new CIPException("Connection failure, General Status Code: " + data[42]);
                            else
                                throw new CIPException("Connection failure, General Status Code: " + data[42] + " Additional Status Code: " + ((data[45]<<8)|data[44]) + " " + ObjectLibrary.ConnectionManagerObject.GetExtendedStatus((uint)((data[45] << 8) | data[44])));
                    case 0x14: throw new CIPException("CIP-Exception: Attribute not supported, General Status Code: " + data[42]);
                    case 0x5: throw new CIPException("CIP-Exception: Path destination unknown, General Status Code: " + data[42]);
                    case 0x16: throw new CIPException("CIP-Exception: Object does not exist: " + data[42]);
                    case 0x15: throw new CIPException("CIP-Exception: Too much data: " + data[42]);

                    default: throw new CIPException("CIP-Exception, General Status Code: " + data[42]);
                }
            }
            //--------------------------END Error?
            //Open UDP-Port
            
            System.Net.IPEndPoint endPointReceive = new System.Net.IPEndPoint(System.Net.IPAddress.Any, UDPPort);
            udpClientReceive = new System.Net.Sockets.UdpClient(endPointReceive);
            UdpState s = new UdpState();
            s.e = endPointReceive;
            s.u = udpClientReceive;

            System.Threading.Thread sendThread = new System.Threading.Thread(sendUDP);
            sendThread.Start();

            var asyncResult = udpClientReceive.BeginReceive(new AsyncCallback(ReceiveCallbackClass1), s);
        }

        public void ForwardClose()
        {
            //First stop the Thread which send data

            stopUDP = true;


            int lengthOffset = (5 + (O_T_ConnectionType == ConnectionType.Null ? 0 : 2) + (T_O_ConnectionType == ConnectionType.Null ? 0 : 2));

            Encapsulation encapsulation = new Encapsulation();
            encapsulation.SessionHandle = sessionHandle;
            encapsulation.Command = Encapsulation.CommandsEnum.SendRRData;
            encapsulation.Length = (ushort)(16 +17+ (ushort)lengthOffset);
            //---------------Interface Handle CIP
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Interface Handle CIP

            //----------------Timeout
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Timeout

            //Common Packet Format (Table 2-6.1)
            Encapsulation.CommonPacketFormat commonPacketFormat = new Encapsulation.CommonPacketFormat();
            commonPacketFormat.ItemCount = 0x02;

            commonPacketFormat.AddressItem = 0x0000;        //NULL (used for UCMM Messages)
            commonPacketFormat.AddressLength = 0x0000;


            commonPacketFormat.DataItem = 0xB2;
            commonPacketFormat.DataLength = (ushort)(17 + (ushort)lengthOffset);



            //----------------CIP Command "Forward Close"
            commonPacketFormat.Data.Add(0x4E);
            //----------------CIP Command "Forward Close"

            //----------------Requested Path size
            commonPacketFormat.Data.Add(2);
            //----------------Requested Path size

            //----------------Path segment for Class ID
            commonPacketFormat.Data.Add(0x20);
            commonPacketFormat.Data.Add((byte)6);
            //----------------Path segment for Class ID

            //----------------Path segment for Instance ID
            commonPacketFormat.Data.Add(0x24);
            commonPacketFormat.Data.Add((byte)1);
            //----------------Path segment for Instace ID

            //----------------Priority and Time/Tick - Table 3-5.16 (Vol. 1)
            commonPacketFormat.Data.Add(0x03);
            //----------------Priority and Time/Tick

            //----------------Timeout Ticks - Table 3-5.16 (Vol. 1)
            commonPacketFormat.Data.Add(0xfa);
            //----------------Timeout Ticks

            //Connection serial number
            commonPacketFormat.Data.Add((byte)connectionSerialNumber);
            commonPacketFormat.Data.Add((byte)(connectionSerialNumber >> 8));
            //connection seruial number

            //----------------Originator Vendor ID
            commonPacketFormat.Data.Add(0xFF);
            commonPacketFormat.Data.Add(0);
            //----------------Originaator Vendor ID

            //----------------Originator Serial Number
            commonPacketFormat.Data.Add(0xFF);
            commonPacketFormat.Data.Add(0xFF);
            commonPacketFormat.Data.Add(0xFF);
            commonPacketFormat.Data.Add(0xFF);
            //----------------Originator Serial Number

            //Connection Path size 
            commonPacketFormat.Data.Add((byte)((0x2) + (O_T_ConnectionType == ConnectionType.Null ? 0 : 1) + (T_O_ConnectionType == ConnectionType.Null ? 0 : 1)));
            //Reserved
            commonPacketFormat.Data.Add(0);
            //Reserved


            //Verbindugspfad
            commonPacketFormat.Data.Add((byte)(0x20));
            commonPacketFormat.Data.Add((byte)(0x4));
            commonPacketFormat.Data.Add((byte)(0x24));
            commonPacketFormat.Data.Add((byte)(0x01));
            if (O_T_ConnectionType != ConnectionType.Null)
            {
                commonPacketFormat.Data.Add((byte)(0x2C));
                commonPacketFormat.Data.Add((byte)(O_T_InstanceID));
            }
            if (T_O_ConnectionType != ConnectionType.Null)
            {
                commonPacketFormat.Data.Add((byte)(0x2C));
                commonPacketFormat.Data.Add((byte)(T_O_InstanceID));
            }

            byte[] dataToWrite = new byte[encapsulation.toBytes().Length + commonPacketFormat.toBytes().Length];
            System.Buffer.BlockCopy(encapsulation.toBytes(), 0, dataToWrite, 0, encapsulation.toBytes().Length);
            System.Buffer.BlockCopy(commonPacketFormat.toBytes(), 0, dataToWrite, encapsulation.toBytes().Length, commonPacketFormat.toBytes().Length);
            encapsulation.toBytes();

            stream.Write(dataToWrite, 0, dataToWrite.Length);
            byte[] data = new Byte[512];

            Int32 bytes = stream.Read(data, 0, data.Length);

            //--------------------------BEGIN Error?
            if (data[42] != 0)      //Exception codes see "Table B-1.1 CIP General Status Codes"
            {
                switch (data[42])
                {

                    case 0x1:
                        if (data[43] == 0)
                            throw new CIPException("Connection failure, General Status Code: " + data[42]);
                        else
                            throw new CIPException("Connection failure, General Status Code: " + data[42] + " Additional Status Code: " + ((data[45] << 8) | data[44]) + " " + ObjectLibrary.ConnectionManagerObject.GetExtendedStatus((uint)((data[45] << 8) | data[44])));
                    case 0x14: throw new CIPException("CIP-Exception: Attribute not supported, General Status Code: " + data[42]);
                    case 0x5: throw new CIPException("CIP-Exception: Path destination unknown, General Status Code: " + data[42]);
                    case 0x16: throw new CIPException("CIP-Exception: Object does not exist: " + data[42]);
                    case 0x15: throw new CIPException("CIP-Exception: Too much data: " + data[42]);

                    default: throw new CIPException("CIP-Exception, General Status Code: " + data[42]);
                }
            }


            //Close the Socket for Receive
            udpClientReceiveClosed = true;
            udpClientReceive.Close();
  
           


        }

        private bool stopUDP;
        
        private void sendUDP()
        {
            System.Net.Sockets.UdpClient udpClientsend = new System.Net.Sockets.UdpClient();
            stopUDP = false;
            uint sequenceCount = 0;
            while (!stopUDP)
            {
                byte[] o_t_IOData = new byte[523];
                System.Net.IPEndPoint endPointsend = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(IPAddress), UDPPort);
               
                UdpState send = new UdpState();
                 
                //---------------Item count
                o_t_IOData[0] = 2;
                o_t_IOData[1] = 0;
                //---------------Item count

                //---------------Type ID
                o_t_IOData[2] = 0x02;
                o_t_IOData[3] = 0x80;
                //---------------Type ID

                //---------------Length
                o_t_IOData[4] = 0x08;
                o_t_IOData[5] = 0x00;
                //---------------Length

                //---------------connection ID
                sequenceCount++;
                o_t_IOData[6] = (byte)(connectionID_O_T);
                o_t_IOData[7] = (byte)(connectionID_O_T >> 8); 
                o_t_IOData[8] = (byte)(connectionID_O_T >> 16); 
                o_t_IOData[9] = (byte)(connectionID_O_T >> 24);
                //---------------connection ID     

                //---------------sequence count
                o_t_IOData[10] = (byte)(sequenceCount);
                o_t_IOData[11] = (byte)(sequenceCount >> 8);
                o_t_IOData[12] = (byte)(sequenceCount >> 16);
                o_t_IOData[13] = (byte)(sequenceCount >> 24);
                //---------------sequence count            

                //---------------Type ID
                o_t_IOData[14] = 0xB1;
                o_t_IOData[15] = 0x00;
                //---------------Type ID

                ushort headerOffset = 0;
                if (O_T_RealTimeFormat == RealTimeFormat.Header32Bit)
                    headerOffset = 4;
                if (O_T_RealTimeFormat == RealTimeFormat.Heartbeat)
                    headerOffset = 0;
                ushort o_t_Length = (ushort)(O_T_Length + headerOffset+2);   //Modeless and zero Length

                //---------------Length
                o_t_IOData[16] = (byte)o_t_Length;
                o_t_IOData[17] = (byte)(o_t_Length >> 8);
                //---------------Length

                //---------------Sequence count
                if (O_T_RealTimeFormat != RealTimeFormat.Heartbeat)
                {
                    o_t_IOData[18] = (byte)1;
                    o_t_IOData[19] = (byte)0;
                }
                //---------------Sequence count

                if (O_T_RealTimeFormat == RealTimeFormat.Header32Bit)
                {
                    o_t_IOData[20] = (byte)1;
                    o_t_IOData[21] = (byte)0;
                    o_t_IOData[22] = (byte)0;
                    o_t_IOData[23] = (byte)0;

                }

                    //---------------Write data
                    for ( int i = 0; i < O_T_Length; i++)
                    o_t_IOData[20+headerOffset+i] = (byte)O_T_IOData[i];
                //---------------Write data




                udpClientsend.Send(o_t_IOData, O_T_Length+20+headerOffset, endPointsend);
                System.Threading.Thread.Sleep((int)RequestedPacketRate_O_T/1000);

            }

            udpClientsend.Close();

        }

        private void ReceiveCallbackClass1(IAsyncResult ar)
        {
            
            UdpClient u = (UdpClient)((UdpState)(ar.AsyncState)).u;
            if (udpClientReceiveClosed)
                return;

            u.BeginReceive(new AsyncCallback(ReceiveCallbackClass1), (UdpState)(ar.AsyncState));
            System.Net.IPEndPoint e = (System.Net.IPEndPoint)((UdpState)(ar.AsyncState)).e;


            Byte[] receiveBytes = u.EndReceive(ar, ref e);

            // EndReceive worked and we have received data and remote endpoint

            if (receiveBytes.Length > 20)
            {
                //Get the connection ID
                uint connectionID = (uint)(receiveBytes[6] | receiveBytes[7] << 8 | receiveBytes[8] << 16 | receiveBytes[9] << 24);


                if (connectionID == connectionID_T_O)
                {
                    ushort headerOffset = 0;
                    if (T_O_RealTimeFormat == RealTimeFormat.Header32Bit)
                        headerOffset = 4;
                    if (T_O_RealTimeFormat == RealTimeFormat.Heartbeat)
                        headerOffset = 0;
                    for (int i = 0; i < T_O_Length; i++)
                    {
                        T_O_IOData[i] = receiveBytes[20 + i + headerOffset];
                    }
                    Console.WriteLine(T_O_IOData[0]);


                }
            }
        }



        /// <summary>
        /// Sends a RegisterSession command to a target to initiate session
        /// </summary>
        /// <param name="address">IP-Address of the target device</param> 
        /// <param name="port">Port of the target device (default should be 0xAF12)</param> 
        /// <returns>Session Handle</returns>	
        public UInt32 RegisterSession(string address, UInt16 port)
        {
            string[] addressSubstring = address.Split('.');
            UInt32 ipAddress = UInt32.Parse(addressSubstring[3]) + (UInt32.Parse(addressSubstring[2]) << 8) + (UInt32.Parse(addressSubstring[1]) << 16) + (UInt32.Parse(addressSubstring[0]) << 24);
            return RegisterSession(ipAddress, port);
        }

        /// <summary>
        /// Sends a RegisterSession command to a target to initiate session with the Standard or predefined Port (Standard: 0xAF12)
        /// </summary>
        /// <param name="address">IP-Address of the target device</param> 
        /// <returns>Session Handle</returns>	
        public UInt32 RegisterSession(string address)
        {
            string[] addressSubstring = address.Split('.');
            UInt32 ipAddress = UInt32.Parse(addressSubstring[3]) + (UInt32.Parse(addressSubstring[2]) << 8) + (UInt32.Parse(addressSubstring[1]) << 16) + (UInt32.Parse(addressSubstring[0]) << 24);
            return RegisterSession(ipAddress, this.TCPPort);
        }

        /// <summary>
        /// Sends a RegisterSession command to a target to initiate session with the Standard or predefined Port and Predefined IPAddress (Standard-Port: 0xAF12)
        /// </summary>
        /// <returns>Session Handle</returns>	
        public UInt32 RegisterSession()
        {
            
            return RegisterSession(this.IPAddress, this.TCPPort);
        }

        public byte[] GetAttributeSingle(int classID, int instanceID, int attributeID)
        {
            if (sessionHandle == 0)             //If a Session is not Registers, Try to Registers a Session with the predefined IP-Address and Port
                this.RegisterSession();
            byte[] dataToSend = new byte[48];
            Encapsulation encapsulation = new Encapsulation();
            encapsulation.SessionHandle = sessionHandle;
            encapsulation.Command = Encapsulation.CommandsEnum.SendRRData;
            encapsulation.Length = 24;
            //---------------Interface Handle CIP
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Interface Handle CIP

            //----------------Timeout
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Timeout

            //Common Packet Format (Table 2-6.1)
            Encapsulation.CommonPacketFormat commonPacketFormat = new Encapsulation.CommonPacketFormat();
            commonPacketFormat.ItemCount = 0x02;

            commonPacketFormat.AddressItem = 0x0000;        //NULL (used for UCMM Messages)
            commonPacketFormat.AddressLength = 0x0000;

            commonPacketFormat.DataItem = 0xB2;
            commonPacketFormat.DataLength = 8;



            //----------------CIP Command "Get Attribute Single"
            commonPacketFormat.Data.Add((byte)Sres.Net.EEIP.CIPCommonServices.Get_Attribute_Single);
            //----------------CIP Command "Get Attribute Single"

            //----------------Requested Path size
            commonPacketFormat.Data.Add(3);
            //----------------Requested Path size

            //----------------Path segment for Class ID
            commonPacketFormat.Data.Add(0x20);
            commonPacketFormat.Data.Add((byte)classID);
            //----------------Path segment for Class ID

            //----------------Path segment for Instance ID
            commonPacketFormat.Data.Add(0x24);
            commonPacketFormat.Data.Add((byte)instanceID);
            //----------------Path segment for Instace ID

            //----------------Path segment for Attribute ID
            commonPacketFormat.Data.Add(0x30);
            commonPacketFormat.Data.Add((byte)attributeID);
            //----------------Path segment for Attribute ID

            byte[] dataToWrite = new byte[encapsulation.toBytes().Length + commonPacketFormat.toBytes().Length];
            System.Buffer.BlockCopy(encapsulation.toBytes(), 0, dataToWrite, 0, encapsulation.toBytes().Length);
            System.Buffer.BlockCopy(commonPacketFormat.toBytes(), 0, dataToWrite, encapsulation.toBytes().Length, commonPacketFormat.toBytes().Length);
            encapsulation.toBytes();

            stream.Write(dataToWrite, 0, dataToWrite.Length);
            byte[] data = new Byte[256];

            Int32 bytes = stream.Read(data, 0, data.Length);

            //--------------------------BEGIN Error?
            if (data[42] != 0)      //Exception codes see "Table B-1.1 CIP General Status Codes"
            {
                switch (data[42])
                {
                    case 0x1: throw new CIPException("Connection failure, General Status Code: " + data[42]);
                    case 0x14: throw new CIPException("CIP-Exception: Attribute not supported, General Status Code: " + data[42]);
                    case 0x5: throw new CIPException("CIP-Exception: Path destination unknown, General Status Code: " + data[42]);
                    case 0x16: throw new CIPException("CIP-Exception: Object does not exist: " + data[42]);
                    case 0x15: throw new CIPException("CIP-Exception: Too much data: " + data[42]);
                    default: throw new CIPException("CIP-Exception, General Status Code: " + data[42]); 
                }
            }
            //--------------------------END Error?

            byte[] returnData = new byte[bytes - 44];
            System.Buffer.BlockCopy(data, 44, returnData, 0, bytes-44);

            return returnData;
        }

        /// <summary>
        /// Implementation of Common Service "Get_Attribute_All" - Service Code: 0x01
        /// </summary>
        /// <param name="classID">Class id of requested Attributes</param> 
        /// <param name="instanceID">Instance of Requested Attributes (0 for class Attributes)</param> 
        /// <returns>Session Handle</returns>	
        public byte[] GetAttributeAll(int classID, int instanceID)
        {
            if (sessionHandle == 0)             //If a Session is not Registered, Try to Registers a Session with the predefined IP-Address and Port
                this.RegisterSession();
            byte[] dataToSend = new byte[46];
            Encapsulation encapsulation = new Encapsulation();
            encapsulation.SessionHandle = sessionHandle;
            encapsulation.Command = Encapsulation.CommandsEnum.SendRRData;
            encapsulation.Length = 22;
            //---------------Interface Handle CIP
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Interface Handle CIP

            //----------------Timeout
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Timeout

            //Common Packet Format (Table 2-6.1)
            Encapsulation.CommonPacketFormat commonPacketFormat = new Encapsulation.CommonPacketFormat();
            commonPacketFormat.ItemCount = 0x02;

            commonPacketFormat.AddressItem = 0x0000;        //NULL (used for UCMM Messages)
            commonPacketFormat.AddressLength = 0x0000;

            commonPacketFormat.DataItem = 0xB2;
            commonPacketFormat.DataLength = 6;



            //----------------CIP Command "Get Attribute Single"
            commonPacketFormat.Data.Add((byte)Sres.Net.EEIP.CIPCommonServices.Get_Attributes_All);
            //----------------CIP Command "Get Attribute Single"

            //----------------Requested Path size
            commonPacketFormat.Data.Add(2);
            //----------------Requested Path size

            //----------------Path segment for Class ID
            commonPacketFormat.Data.Add(0x20);
            commonPacketFormat.Data.Add((byte)classID);
            //----------------Path segment for Class ID

            //----------------Path segment for Instance ID
            commonPacketFormat.Data.Add(0x24);
            commonPacketFormat.Data.Add((byte)instanceID);
            //----------------Path segment for Instace ID


            byte[] dataToWrite = new byte[encapsulation.toBytes().Length + commonPacketFormat.toBytes().Length];
            System.Buffer.BlockCopy(encapsulation.toBytes(), 0, dataToWrite, 0, encapsulation.toBytes().Length);
            System.Buffer.BlockCopy(commonPacketFormat.toBytes(), 0, dataToWrite, encapsulation.toBytes().Length, commonPacketFormat.toBytes().Length);
           

            stream.Write(dataToWrite, 0, dataToWrite.Length);
            byte[] data = new Byte[256];

            Int32 bytes = stream.Read(data, 0, data.Length);
            //--------------------------BEGIN Error?
            if (data[42] != 0)      //Exception codes see "Table B-1.1 CIP General Status Codes"
            {
                switch (data[42])
                {
                    case 0x1: throw new CIPException("Connection failure, General Status Code: " + data[42]);
                    case 0x14: throw new CIPException("CIP-Exception: Attribute not supported, General Status Code: " + data[42]);
                    case 0x5: throw new CIPException("CIP-Exception: Path destination unknown, General Status Code: " + data[42]);
                    case 0x16: throw new CIPException("CIP-Exception: Object does not exist: " + data[42]);
                    case 0x15: throw new CIPException("CIP-Exception: Too much data: " + data[42]);
                    default: throw new CIPException("CIP-Exception, General Status Code: " + data[42]);
                }
            }
            //--------------------------END Error?

            byte[] returnData = new byte[bytes - 44];
            System.Buffer.BlockCopy(data, 44, returnData, 0, bytes - 44);

            return returnData;
        }

        public byte[] SetAttributeSingle(int classID, int instanceID, int attributeID, byte[] value)
        {
            if (sessionHandle == 0)             //If a Session is not Registers, Try to Registers a Session with the predefined IP-Address and Port
                this.RegisterSession();
            byte[] dataToSend = new byte[48 + value.Length];
            Encapsulation encapsulation = new Encapsulation();
            encapsulation.SessionHandle = sessionHandle;
            encapsulation.Command = Encapsulation.CommandsEnum.SendRRData;
            encapsulation.Length = (UInt16)(24+value.Length);
            //---------------Interface Handle CIP
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Interface Handle CIP

            //----------------Timeout
            encapsulation.CommandSpecificData.Add(0);
            encapsulation.CommandSpecificData.Add(0);
            //----------------Timeout

            //Common Packet Format (Table 2-6.1)
            Encapsulation.CommonPacketFormat commonPacketFormat = new Encapsulation.CommonPacketFormat();
            commonPacketFormat.ItemCount = 0x02;

            commonPacketFormat.AddressItem = 0x0000;        //NULL (used for UCMM Messages)
            commonPacketFormat.AddressLength = 0x0000;

            commonPacketFormat.DataItem = 0xB2;
            commonPacketFormat.DataLength = (UInt16)(8 + value.Length);



            //----------------CIP Command "Set Attribute Single"
            commonPacketFormat.Data.Add((byte)Sres.Net.EEIP.CIPCommonServices.Set_Attribute_Single);
            //----------------CIP Command "Set Attribute Single"

            //----------------Requested Path size
            commonPacketFormat.Data.Add(3);
            //----------------Requested Path size

            //----------------Path segment for Class ID
            commonPacketFormat.Data.Add(0x20);
            commonPacketFormat.Data.Add((byte)classID);
            //----------------Path segment for Class ID

            //----------------Path segment for Instance ID
            commonPacketFormat.Data.Add(0x24);
            commonPacketFormat.Data.Add((byte)instanceID);
            //----------------Path segment for Instace ID

            //----------------Path segment for Attribute ID
            commonPacketFormat.Data.Add(0x30);
            commonPacketFormat.Data.Add((byte)attributeID);
            //----------------Path segment for Attribute ID

            //----------------Data
            for (int i = 0; i < value.Length; i++)
            {
                commonPacketFormat.Data.Add(value[i]);
            }
            //----------------Data

            byte[] dataToWrite = new byte[encapsulation.toBytes().Length + commonPacketFormat.toBytes().Length];
            System.Buffer.BlockCopy(encapsulation.toBytes(), 0, dataToWrite, 0, encapsulation.toBytes().Length);
            System.Buffer.BlockCopy(commonPacketFormat.toBytes(), 0, dataToWrite, encapsulation.toBytes().Length, commonPacketFormat.toBytes().Length);
            encapsulation.toBytes();

            stream.Write(dataToWrite, 0, dataToWrite.Length);
            byte[] data = new Byte[256];

            Int32 bytes = stream.Read(data, 0, data.Length);

            //--------------------------BEGIN Error?
            if (data[42] != 0)      //Exception codes see "Table B-1.1 CIP General Status Codes"
            {
                switch (data[42])
                {
                    case 0x1: throw new CIPException("Connection failure, General Status Code: " + data[42]);
                    case 0x14: throw new CIPException("CIP-Exception: Attribute not supported, General Status Code: " + data[42]);
                    case 0x5: throw new CIPException("CIP-Exception: Path destination unknown, General Status Code: " + data[42]);
                    case 0x16: throw new CIPException("CIP-Exception: Object does not exist: " + data[42]);
                    case 0x15: throw new CIPException("CIP-Exception: Too much data: " + data[42]);

                    default: throw new CIPException("CIP-Exception, General Status Code: " + data[42]);
                }
            }
            //--------------------------END Error?

            byte[] returnData = new byte[bytes - 44];
            System.Buffer.BlockCopy(data, 44, returnData, 0, bytes - 44);

            return returnData;
        }

        /// <summary>
        /// Implementation of Common Service "Get_Attribute_All" - Service Code: 0x01
        /// </summary>
        /// <param name="classID">Class id of requested Attributes</param> 
        public byte[] GetAttributeAll(int classID)
        {
            return this.GetAttributeAll(classID, 0);
        }

        ObjectLibrary.IdentityObject identityObject;
        public ObjectLibrary.IdentityObject IdentityObject
        {
            get
            {
                if (identityObject == null)
                    identityObject = new ObjectLibrary.IdentityObject(this);
                return identityObject;

            }
        }

        ObjectLibrary.MessageRouterObject messageRouterObject;
        public ObjectLibrary.MessageRouterObject MessageRouterObject
        {
            get
            {
                if (messageRouterObject == null)
                    messageRouterObject = new ObjectLibrary.MessageRouterObject(this);
                return messageRouterObject;

            }
        }

        ObjectLibrary.AssemblyObject assemblyObject;
        public ObjectLibrary.AssemblyObject AssemblyObject
        {
            get
            {
                if (assemblyObject == null)
                    assemblyObject = new ObjectLibrary.AssemblyObject(this);
                return assemblyObject;

            }
        }



        /// <summary>
        /// Converts a bytearray (received e.g. via getAttributeSingle) to ushort
        /// </summary>
        /// <param name="byteArray">bytearray to convert</param> 
        public static ushort ToUshort(byte[] byteArray)
        {
            UInt16 returnValue;
            returnValue = (UInt16)(byteArray[1] << 8 | byteArray[0]);
            return returnValue;
        }

        /// <summary>
        /// Converts a bytearray (received e.g. via getAttributeSingle) to uint
        /// </summary>
        /// <param name="byteArray">bytearray to convert</param> 
        public static uint ToUint(byte[] byteArray)
        {
            UInt32 returnValue = ((UInt32)byteArray[3] << 24 | (UInt32)byteArray[2] << 16 | (UInt32)byteArray[1] << 8 | (UInt32)byteArray[0]);
            return returnValue;
        }

        /// <summary>
        /// Returns the "Bool" State of a byte Received via getAttributeSingle
        /// </summary>
        /// <param name="inputByte">byte to convert</param> 
        /// <param name="bitposition">bitposition to convert (First bit = bitposition 0)</param> 
        /// <returns>Converted bool value</returns>
        public static bool ToBool(byte inputByte, int bitposition)
        {
           
            return (((inputByte>>bitposition)&0x01) != 0) ? true : false;
        }

    }

    public enum ConnectionType : byte
    {
        Null = 0,
        Multicast = 1,
        Point_to_Point = 2
    }

    public enum Priority : byte
    {
        Low = 0,
        High = 1,
        Scheduled = 2,
        Urgent = 3
    }

    public enum RealTimeFormat : byte
    {
        Modeless = 0,
        ZeroLength = 1,
        Heartbeat = 2,
        Header32Bit = 3

        
    }




}
