using System;
using System.Net.Sockets;
using System.Text;
using BepInEx.Logging;

namespace COTL.Common
{
    /// <summary>
    /// Reusable helper for transmitting UDP messages to localhost:9999 (or custom host/port).
    /// </summary>
    public static class UdpMessenger
    {
        private static readonly UdpClient client = new UdpClient();
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = 9999;

        /// <summary>
        /// Sends a text message over UDP to the specified host and port (default 127.0.0.1:9999).
        /// </summary>
        public static void Send(string text, ManualLogSource log = null, string host = DefaultHost, int port = DefaultPort)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                client.Send(bytes, bytes.Length, host, port);
            }
            catch (Exception ex)
            {
                if (log != null)
                {
                    log.LogError($"[UdpMessenger] Failed to send UDP message to {host}:{port} - {ex.Message}");
                }
            }
        }
    }
}
