using System;
using System.Net;
using System.Net.Sockets;

namespace KartRider
{
	public class RouterListener
	{
		public static string sIP;

		public static int port;

		public static string forceConnect;

		public static System.Net.IPEndPoint CurrentUDPServer { get; set; }

		public static string ForceConnect { get; set; }

		public static TcpListener Listener { get; private set; }

		public static SessionGroup MySession { get; set; }

		/// <summary>지금 접속을 받고 있는지 (시작 실패 / 주소 변경 실패 시 false)</summary>
		public static bool Running { get; private set; }

		/// <summary>마지막 시작 실패 이유</summary>
		public static string LastError { get; private set; }

		/// <summary>서버를 다시 시작했거나 시작에 실패했을 때 (화면 표시 갱신용)</summary>
		public static event Action ServerChanged;

		private static volatile bool restarting;

		static RouterListener()
		{
			string str = "127.0.0.1";
			RouterListener.sIP = str;
			int str1 = 39312;
			RouterListener.port = str1;
		}

		public static void OnAcceptSocket(IAsyncResult ar)
		{
			// 서버를 다시 시작하는 중이거나, 다시 시작하기 전의 옛 서버가 남긴 콜백이면 무시
			TcpListener current = ar.AsyncState as TcpListener;
			if (restarting || (current != null && !object.ReferenceEquals(current, RouterListener.Listener))) return;
			try
			{
				Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				Socket clientSocket = RouterListener.Listener.EndAcceptSocket(ar);
				Console.WriteLine("[서버] 클라이언트 접속: {0}", clientSocket.RemoteEndPoint);
				RouterListener.forceConnect = RouterListener.sIP;
				if ((RouterListener.ForceConnect == "" ? false : RouterListener.ForceConnect != "0.0.0.0"))
				{
					RouterListener.forceConnect = RouterListener.ForceConnect;
				}
				RouterListener.MySession = new SessionGroup(clientSocket, null);
				GameSupport.PcFirstMessage();
			}
			catch (Exception ex)
			{
				AppLog.Error("클라이언트 접속 처리 중 예외 (여기서 예외가 나면 게임이 바로 끊깁니다)", ex);
			}
			try { RouterListener.Listener.BeginAcceptSocket(new AsyncCallback(RouterListener.OnAcceptSocket), RouterListener.Listener); }
			catch (Exception ex2) { if (RouterListener.Running) AppLog.Error("접속 대기 재개 실패", ex2); }
		}

		public static void Start()
		{
			Console.WriteLine("Load server IP : {0}:{1}", (object)RouterListener.sIP, (object)RouterListener.port);
			RouterListener.ForceConnect = "";
			try
			{
				var l = new TcpListener(IPAddress.Parse(RouterListener.sIP), RouterListener.port);
				l.Start();
				RouterListener.Listener = l;
				RouterListener.Running = true;
				RouterListener.LastError = null;
				Console.WriteLine("[서버] 접속 대기 시작");
				l.BeginAcceptSocket(new AsyncCallback(RouterListener.OnAcceptSocket), l);
				RouterListener.CurrentUDPServer = new System.Net.IPEndPoint(IPAddress.Parse(RouterListener.sIP), 39311);
			}
			catch (Exception ex)
			{
				RouterListener.Running = false;
				RouterListener.LastError = ex.Message;
				AppLog.Error("서버 시작 실패 " + RouterListener.sIP + ":" + RouterListener.port, ex);
			}
		}

		/// <summary>
		/// 서버 주소/포트를 바꿔서 다시 시작 (이미 접속한 게임 연결은 그대로 유지됨).
		/// 새 주소로 시작하지 못하면 이전 주소로 되돌려서 서버가 꺼진 채로 남지 않게 함.
		/// </summary>
		public static bool Restart(string newIp, int newPort, out string error)
		{
			error = null;
			string oldIp = RouterListener.sIP;
			int oldPort = RouterListener.port;
			restarting = true;
			try
			{
				try { if (RouterListener.Listener != null) RouterListener.Listener.Stop(); } catch { }
				RouterListener.Running = false;
				try
				{
					var l = new TcpListener(IPAddress.Parse(newIp), newPort);
					l.Start();
					RouterListener.sIP = newIp;
					RouterListener.port = newPort;
					RouterListener.Listener = l;
					RouterListener.Running = true;
					RouterListener.LastError = null;
					RouterListener.CurrentUDPServer = new System.Net.IPEndPoint(IPAddress.Parse(newIp), 39311);
					Console.WriteLine("[서버] 서버 주소를 {0}:{1} 로 바꿔 다시 시작했어요", newIp, newPort);
					l.BeginAcceptSocket(new AsyncCallback(RouterListener.OnAcceptSocket), l);
					return true;
				}
				catch (Exception ex)
				{
					error = ex.Message;
					Console.WriteLine("[서버] {0}:{1} 로 시작하지 못했어요: {2}", newIp, newPort, ex.Message);
					try
					{
						var back = new TcpListener(IPAddress.Parse(oldIp), oldPort);
						back.Start();
						RouterListener.Listener = back;
						RouterListener.Running = true;
						RouterListener.LastError = null;
						Console.WriteLine("[서버] 이전 주소 {0}:{1} 로 계속 실행해요", oldIp, oldPort);
						back.BeginAcceptSocket(new AsyncCallback(RouterListener.OnAcceptSocket), back);
					}
					catch (Exception ex2) { RouterListener.LastError = ex2.Message; }
					return false;
				}
			}
			finally
			{
				restarting = false;
				Action h = ServerChanged;
				if (h != null) h();
			}
		}
	}
}
