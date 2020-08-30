﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace FluxWebServer
{
    class FluxServer
    {
        private int port;
        private string path;
        private TcpListener tcpListener;
        private bool stoppingListener;
        public event EventHandler<LogMessageEventArgs> LogMessage;

        public FluxServer(int port, string path)
        {
            this.port = port;
            this.path = path;
        }

        public bool Start()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == 10048)
                    OnLogMessage(new LogMessageEventArgs("Error: Address is already in use, ensure that the port is open."));
                else
                    OnLogMessage(new LogMessageEventArgs($"Error: {ex.ToString()}"));
                return false;
            }

            tcpListener.BeginAcceptTcpClient(ManageClient, tcpListener);
            return true;
        }

        public void Stop()
        {
            stoppingListener = true;
            tcpListener.Stop();
        }

        private void ManageClient(IAsyncResult iarStatus)
        {
#if DEBUG
            Console.WriteLine("=== Incoming connection");
#endif
            if (stoppingListener)
            {
                stoppingListener = false;
                return;
            }
            byte[] bContent;
            tcpListener = (TcpListener) iarStatus.AsyncState;
            var tcpClient = tcpListener.EndAcceptTcpClient(iarStatus);
            tcpListener.BeginAcceptTcpClient(ManageClient, tcpListener);
            
            byte[] bInput = new byte[tcpClient.Available];
            NetworkStream nsInput = tcpClient.GetStream();
            nsInput.Read(bInput, 0, bInput.Length);
            string data = Encoding.UTF8.GetString(bInput);
#if DEBUG
            Console.WriteLine($"<-- {data} from {tcpClient.Client.Handle}");
#endif
            if (new System.Text.RegularExpressions.Regex("^GET").IsMatch(data))
            {
                string[] strDataW = data.Split(new char[] { ' ' });
                string strFilePath = strDataW[1];
                OnLogMessage(new LogMessageEventArgs($"\"{strDataW[0]} {strDataW[1]}\" from {tcpClient.Client.RemoteEndPoint.ToString()}"));

                try
                {
                    if (strFilePath.Substring(Math.Max(0, strFilePath.Length - 4)) == ".php")
                    {
                        Process phpProc = new Process();
                        phpProc.StartInfo.UseShellExecute = false;
                        phpProc.StartInfo.RedirectStandardOutput = true;
                        phpProc.StartInfo.FileName = "php\\php-cgi.exe";
                        phpProc.StartInfo.Arguments = path + strFilePath;
                        phpProc.Start();
                        string phpResult = phpProc.StandardOutput.ReadToEnd();
                        phpProc.WaitForExit();
                        bContent = Encoding.UTF8.GetBytes(phpResult);
                    }
                    else
                        bContent = File.ReadAllBytes(path + strFilePath.Replace("/", @"\"));

                }
                catch (Exception ex)
                {
                    if (ex is FileNotFoundException || ex is DirectoryNotFoundException || ex is UnauthorizedAccessException)
                    {
                        bContent = Encoding.UTF8.GetBytes(ReadEmbeddedFile("404.html"));
                    }
                    else
                    {
                        bContent = Encoding.UTF8.GetBytes(ReadEmbeddedFile("error.html"));
                        OnLogMessage(new LogMessageEventArgs($"Error: {ex}"));
                        return;
                    }
                }

                byte[] bHeader = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=UTF-8\r\nContent-Length: {bContent.Length}\r\n\r\n");
                byte[] bResult = JoinByteArray(bHeader, bContent);
                nsInput.Write(bResult, 0, bResult.Length);
                nsInput.Close();
            }
            tcpClient.Close();
        }

        private string ReadEmbeddedFile(string file)
        {
            string result;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"FluxWebServer.resources.{file}"))
            using (StreamReader reader = new StreamReader(stream))
            {
                result = reader.ReadToEnd();
                reader.Close();
            }
            return result;
        }

        private byte[] JoinByteArray(byte[] first, byte[] last)
        {
            byte[] bTmp = new byte[first.Length + last.Length];
            Buffer.BlockCopy(first, 0, bTmp, 0, first.Length);
            Buffer.BlockCopy(last, 0, bTmp, first.Length, last.Length);
            return bTmp;
        }
        
        protected virtual void OnLogMessage(LogMessageEventArgs e)
        {
            LogMessage?.Invoke(this, e);
        }
    }

    public class LogMessageEventArgs : EventArgs
    {
        public string Message;

        public LogMessageEventArgs(string message)
        {
            Message = message;
        }
    }
}