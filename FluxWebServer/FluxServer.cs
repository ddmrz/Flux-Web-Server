﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FluxWebServer
{
    class FluxServer
    {
        private frmMain _frmMain;
        private int port;
        private string path;
        private TcpListener tcpListener;
        private bool stoppingListener;

        public FluxServer(frmMain form, int port, string path)
        {
            _frmMain = form;
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
                {
                    _frmMain.Log("Error: Address is already in use, ensure that the port is open.");
                }
                else
                {
                    _frmMain.Log($"Error: {ex.ToString()}");
                }
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
            if (new System.Text.RegularExpressions.Regex("^GET").IsMatch(data))
            {
                string[] strDataW = data.Split(new char[] { ' ' });
                string strFilePath = strDataW[1];
                _frmMain.Log($"\"{strDataW[0]} {strDataW[1]}\" from {tcpClient.Client.RemoteEndPoint.ToString()}");
                try
                {
                    bContent = File.ReadAllBytes(path + strFilePath.Replace("/", @"\"));
                }
                catch (Exception ex)
                {
                    if (ex is FileNotFoundException || ex is DirectoryNotFoundException || ex is UnauthorizedAccessException)
                    {
                        bContent = Encoding.UTF8.GetBytes("<html><head><title>404</title></head><body><p>404 Not Found</p></body></html>");
                    }
                    else
                    {
                        bContent = Encoding.UTF8.GetBytes("<html><head><title>Error</title></head><body><p>An error has occured.</p></body></html>");
                        _frmMain.Log($"Error: {ex}");
                        return;
                    }
                }

                byte[] bHeader = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {bContent.Length}\r\n\r\n");
                byte[] bResult = JoinByteA(bHeader, bContent);
                nsInput.Write(bResult, 0, bResult.Length);
                nsInput.Close();
            }
            tcpClient.Close();
        }

        private byte[] JoinByteA(byte[] first, byte[] last)
        {
            byte[] bTmp = new byte[first.Length + last.Length];
            Buffer.BlockCopy(first, 0, bTmp, 0, first.Length);
            Buffer.BlockCopy(last, 0, bTmp, first.Length, last.Length);
            return bTmp;
        }
    }
}