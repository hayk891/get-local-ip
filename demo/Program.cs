﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WebServer
{
	public class WebServer
	{
		private readonly HttpListener _listener = new HttpListener();
		private readonly Func<HttpListenerRequest, string> _responderMethod;

		public WebServer(IReadOnlyCollection<string> prefixes, Func<HttpListenerRequest, string> method)
		{
			if (!HttpListener.IsSupported)
			{
				throw new NotSupportedException("Needs Windows XP SP2, Server 2003 or later.");
			}

			// URI prefixes are required eg: "http://localhost:4000/lgetLocalIp/"
			if (prefixes == null || prefixes.Count == 0)
			{
				throw new ArgumentException("URI prefixes are required");
			}

			if (method == null)
			{
				throw new ArgumentException("responder method required");
			}

			foreach (var s in prefixes)
			{
				_listener.Prefixes.Add(s);
			}

			_responderMethod = method;
			_listener.Start();
		}

		public WebServer(Func<HttpListenerRequest, string> method, params string[] prefixes)
		   : this(prefixes, method)
		{
		}

		public void Run()
		{
			ThreadPool.QueueUserWorkItem(o =>
			{
				Console.WriteLine("Webserver running...");
				try
				{
					while (_listener.IsListening)
					{
						ThreadPool.QueueUserWorkItem(c =>
						{
							var ctx = c as HttpListenerContext;
							try
							{
								if (ctx == null)
								{
									return;
								}


								var rstr = _responderMethod(ctx.Request);
								var buf = Encoding.UTF8.GetBytes(rstr);
								ctx.Response.ContentLength64 = buf.Length;
								ctx.Response.OutputStream.Write(buf, 0, buf.Length);
							}
							catch
							{
								// ignored
							}
							finally
							{
								// always close the stream
								if (ctx != null)
								{
									ctx.Response.OutputStream.Close();
								}
							}
						}, _listener.GetContext());
					}
				}
				catch (Exception ex)
				{
					// ignored
				}
			});
		}

		public void Stop()
		{
			_listener.Stop();
			_listener.Close();
		}
	}

	internal class Program
	{
		public static string SendResponse(HttpListenerRequest request)
		{
			return string.Format(GetLocalIPAddress());
		}

		private static void Main(string[] args)
		{
			var ws = new WebServer(SendResponse, "http://localhost:4000/getLocalIp/");
			ws.Run();
			Console.WriteLine("A simple webserver. Press a key to quit.");
			Console.ReadKey();
			ws.Stop();
		}

		public static string GetLocalIPAddress()
		{
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (var ip in host.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					return ip.ToString();
				}
			}
			throw new Exception("No network adapters with an IPv4 address in the system!");
		}
	}
}