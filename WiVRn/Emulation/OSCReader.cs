using OscCore;
using OscCore.LowLevel;
using WVFaceTracking;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;

namespace WiVRn.Emulation
{
    public class OSCReader
    {
        private readonly ConcurrentDictionary<string, string> oscAddressValues = new();
        private readonly object printLock = new();
        private readonly CancellationTokenSource cts = new();
        private readonly int listenPort = 9000;
        private Thread? oscThread;

        static OSCReader()
        {
            WiVRnLogger.Log("OSCReader static constructor: class loaded");
        }

        public OSCReader()
        {
            WiVRnLogger.Log("OSCReader instance constructor: about to start tasks");
            oscThread = new Thread(() => RunThread(cts.Token)) { IsBackground = true };
            oscThread.Start();
        }

        private void RunThread(CancellationToken token)
        {
            try
            {
                var listenTask = ListenLoop(token);
                // var printTask = PrintLoop(token);
                // Task.WaitAll(new[] { listenTask, printTask }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                WiVRnLogger.Log($"OSCReader thread error: {ex.Message}");
            }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            WiVRnLogger.Log($"[DEBUG] Entered ListenLoop method");
            WiVRnLogger.Log($"ListenLoop starting, attempting to bind UDP socket on 127.0.0.1:{listenPort}");
            try
            {
                using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, listenPort));
                WiVRnLogger.Log($"OSC ListenLoop started on 127.0.0.1:{listenPort}");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync();
                        var bytes = new ArraySegment<byte>(result.Buffer, 0, result.Buffer.Length);
                        if (IsBundle(bytes))
                        {
                            var bundle = new OscCore.OscBundleRaw(bytes);
                            foreach (var message in bundle)
                                UpdateOscAddress(message);
                        }
                        else
                        {
                            var message = new OscCore.OscMessageRaw(bytes);
                            UpdateOscAddress(message);
                        }
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (Exception ex)
                    {
                        WiVRnLogger.Log($"OSC Listen error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                WiVRnLogger.Log($"Failed to bind UDP socket: {ex.Message}");
            }
        }

        private void UpdateOscAddress(OscCore.OscMessageRaw message)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < message.Count; i++)
            {
                var arg = message[i];
                try { sb.Append($"{message.ReadFloat(ref arg)} "); continue; } catch { }
                try { sb.Append($"{message.ReadInt(ref arg)} "); continue; } catch { }
                try { sb.Append($"\"{message.ReadString(ref arg)}\" "); continue; } catch { }
                sb.Append("[unknown] ");
            }
            oscAddressValues[message.Address] = sb.ToString().Trim();
        }

        // private async Task PrintLoop(CancellationToken token)
        // {
        //     while (!token.IsCancellationRequested)
        //     {
        //         PrintOscAddresses();
        //         await Task.Delay(1000, token);
        //     }
        // }

        private void PrintOscAddresses()
        {
            lock (printLock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("OSC Address Monitor\n====================\n");
                foreach (var kvp in oscAddressValues.OrderBy(kvp => kvp.Key))
                {
                    sb.AppendLine($"{kvp.Key,-30} : {kvp.Value}");
                }
                WiVRnLogger.Log(sb.ToString());
            }
        }

        public string? GetValue(string address)
        {
            oscAddressValues.TryGetValue(address, out var value);
            return value;
        }

        private static readonly byte[] BundlePrefix = Encoding.ASCII.GetBytes("#bundle");
        private static bool IsBundle(ArraySegment<byte> bytes)
        {
            var prefix = BundlePrefix;
            if (bytes.Array == null || bytes.Count < prefix.Length)
                return false;
            for (int i = 0; i < prefix.Length; i++)
                if (bytes.Array[bytes.Offset + i] != prefix[i])
                    return false;
            return true;
        }
    }
}