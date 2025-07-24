using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Support.Net
{
    [AddINotifyPropertyChangedInterface]
    public class TCPCommander
    {
        public string? ServerIP { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 33456;
        public Action<string>? OnDataReceived { get; set; }

        public int CountRespTimeOut { get; set; } = 0;
        public void Send(string data, bool IsTask = true, bool GetResponse = false, Func<bool>? CheckInterrupt = null)
        {
            if (IsTask)
                Task.Run(() => SendData(data, GetResponse, CheckInterrupt));
            else
                SendData(data, GetResponse, CheckInterrupt);
        }
        private void SendData(string data, bool GetResponse = false, Func<bool>? CheckInterrupt = null)
        {
            //SysLog.Add(LogLevel.Info, "傳出:" + data);
            "TCP傳出".TryCatch(() =>
            {
                TcpClient client = new(ServerIP ?? "", Port);
                string recv = "";
                using NetworkStream stream = client.GetStream();
                byte[] buffer = Encoding.ASCII.GetBytes(data + '\0');
                stream.Write(buffer, 0, buffer.Length);

                bool isRecv = false;
                if (GetResponse)
                {
                    CountRespTimeOut = 0;
                    while (!isRecv)
                    {
                        if (CheckInterrupt?.Invoke() ?? false)
                            return;
                        byte[] response = new byte[256];
                        Array.Clear(response, 0, response.Length);
                        try
                        {
                            stream.ReadTimeout = 1500;
                            int bytes = stream.Read(response, 0, response.Length);
                            isRecv = true;
                            recv = Encoding.ASCII.GetString(response, 0, bytes);
                            OnDataReceived?.Invoke(recv);
                        }
                        catch (Exception)
                        {
                            CountRespTimeOut++;
                        }
                    }
                    stream.Close();
                    client.Close();
                }
            });
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class TCPReceiver
    {
        public const int BufferSize = 4096;
        public int Port { get; set; } = 33456;
        public Action<string>? OnRecvCommand { get; set; }
        private TcpClient? Client { get; set; }
        public void Start()
        {
            Task.Run(Main);
        }
        async Task Main()
        {
            TcpListener server = new TcpListener(IPAddress.Any, Port);
            server.Start();
            while (true)
            {
                Client = await server.AcceptTcpClientAsync();
                _ = HandleClientAsync(Client);
            }
        }

        async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[BufferSize]; // 限制最大字串長度為128

                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    OnRecvCommand?.Invoke(receivedData);
                }
            }catch(Exception e)
            {
                SysLog.Add(LogLevel.Error,"TCP接收錯誤:"+ e.Message);
            }
        }
        // 新增發送資料方法
        public async Task SendData(string data)
        {
            try
            {
                if (Client == null)
                    return;
                byte[] buffer = Encoding.ASCII.GetBytes(data);
                NetworkStream stream = Client.GetStream();
                await stream.WriteAsync(buffer, 0, buffer.Length);
                await stream.FlushAsync();
            }
            catch (Exception e)
            {
                SysLog.Add(LogLevel.Error, "TCP發送錯誤:" + e.Message);
            }
        }
    }
}
