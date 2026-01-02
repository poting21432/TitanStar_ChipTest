using Support.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Support.Net
{
    public class TCPCommand(string serverIp, int serverPort)
    {
        public string ServerIp { get; set; } = serverIp;
        public int ServerPort { get; set; } = serverPort;
        public int TimeoutMs { get; set; } = 30000;

        private static readonly SemaphoreSlim _asyncLock = new(1, 1);
        TcpClient _tcpClient = new();
        /// <summary>
        /// 傳送一個訊息給 Server 並等待回應一次
        /// </summary>
        public async Task ConnectAsync(CancellationToken token = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _tcpClient = new();
            var connectTask = _tcpClient!.ConnectAsync(ServerIp, ServerPort);
            var completedTask = await Task.WhenAny(connectTask!, Task.Delay(TimeoutMs, cts.Token));
            if (completedTask != connectTask || _tcpClient == null || !_tcpClient.Connected)
            {
                SysLog.Add(LogLevel.Error, "通訊異常:連線逾時");
                return;
            }
            SysLog.Add(LogLevel.Info, "頻譜儀連線成功");
        }
        public async Task ClearReadBuffer(CancellationToken token = default)
        {
            int timeOut = 3000;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeOut);
            await _asyncLock.WaitAsync();
            NetworkStream stream = _tcpClient!.GetStream();
            try
            {
                // 嘗試連線
                stream.ReadTimeout = timeOut;
                stream.WriteTimeout = timeOut;
                DateTime t_start = DateTime.Now;
                while(!cts.IsCancellationRequested)
                {
                    Thread.Sleep(200);
                    if ((DateTime.Now - t_start).TotalMilliseconds > timeOut)
                        break;
                    if (!stream.DataAvailable)
                        continue;
                    // 等待回應
                    byte[] recvBuffer = new byte[2048];
                    int bytesRead = await ReadWithTimeoutAsync(stream, recvBuffer, cts.Token, false);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                _asyncLock.Release();
            }
        }
        public async Task<string> SendAndReceiveAsync(string message, CancellationToken token = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeoutMs);
            await _asyncLock.WaitAsync();
            try
            {
                if (!_tcpClient.Connected)
                {
                    await ConnectAsync(token);
                }
                // 嘗試連線
                NetworkStream stream = _tcpClient!.GetStream();
                stream.ReadTimeout = TimeoutMs;
                stream.WriteTimeout = TimeoutMs;

                // 發送訊息
                byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(sendBuffer, cts.Token);

                // 等待回應
                byte[] recvBuffer = new byte[2048];
                int bytesRead = await ReadWithTimeoutAsync(stream, recvBuffer, cts.Token);
                if (bytesRead <= 0)
                    return "";

                return Encoding.UTF8.GetString(recvBuffer, 0, bytesRead);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("操作逾時");
            }
            finally
            {
                _asyncLock.Release();
            }
        }
        public async Task<string[]> SendAndReceiveSequenceAsync(string message, int recCount, CancellationToken token = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeoutMs);
            await _asyncLock.WaitAsync();
            List<string> results = [];
            try
            {
                if (!_tcpClient.Connected)
                    await ConnectAsync(token);
                NetworkStream stream = _tcpClient!.GetStream();
                stream.ReadTimeout = TimeoutMs;
                stream.WriteTimeout = TimeoutMs;

                // 發送訊息
                byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(sendBuffer, cts.Token);
                for (int i = 0; i < recCount; i++)
                {
                    // 等待回應
                    byte[] recvBuffer = new byte[2048];
                    int bytesRead = await ReadWithTimeoutAsync(stream, recvBuffer, cts.Token);
                    if (bytesRead <= 0)
                        return [];
                    results.Add(Encoding.UTF8.GetString(recvBuffer, 0, bytesRead));
                }
                return results.ToArray();
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("操作逾時");
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        private async Task<int> ReadWithTimeoutAsync(NetworkStream stream, byte[] buffer, CancellationToken token, bool IsLog = true)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeoutCts.CancelAfter(TimeoutMs);

                var readTask = stream.ReadAsync(buffer, 0, buffer.Length, timeoutCts.Token);
                var completed = await Task.WhenAny(readTask, Task.Delay(TimeoutMs, timeoutCts.Token));
                if (completed != readTask)
                    throw new TimeoutException("接收逾時");
                return await readTask;
            }
            catch (Exception e)
            {
                if(IsLog)
                    SysLog.Add(LogLevel.Error, $"通訊異常:{e.Message}");
                return -1;
            }
            finally
            {
            }
        }

        public void Disconnect(TcpClient? _tcpClient)
        {
            try
            {
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }
            catch { }
        }
    }
}
