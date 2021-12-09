using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace com_mc
{
	public abstract class DataSrc //数据源
	{
		static public JavaScriptSerializer json_ser = new JavaScriptSerializer();
		static public DataSrc factory(Dictionary<string, object> v,RX_CB cb) //简单工厂
		{
			string s = (string)v["type"];
			Type t = Type.GetType("com_mc.DataSrc_" + s);
			var r = Activator.CreateInstance(t) as DataSrc;
			r.fromDict(v); //初始化对象
			return r;
		}
		static public DataSrc cur_ds=null; //当前数据源
		static public List<DataSrc> dslist = new List<DataSrc>();
		//static public void open(Config cfg,string portname) //从配置或名称传入
		//{
		//	if (portname != "socket") //若是串口
		//	{
		//		cur_ds = dsdict[DSType.uart];
		//		(cur_ds as DataSrc_uart).uart.PortName = portname;
		//	}
		//	else cur_ds = dsdict[cfg.socket.type]; //若是网络
		//	cur_ds.open(cfg);
		//}
		public string name = ""; //数据源的名称，如果是串口，则为串口号
		public DataSrc(RX_CB cb)
		{
			rx_event = cb;
		}
		virtual public void fromDict(Dictionary<string, object> v) //从配置加载数据源对象
		{
			if(v.ContainsKey("name")) name = (string)v["name"];
		}
		public delegate void RX_CB(byte[] b);
		public RX_CB rx_event; //串口接收事件
		abstract public void open(string s); //打开数据源，输入以什么名称打开的
		virtual public void close()
		{ }
		abstract public string[] get_names(); //获取本数据源的名称，串口号等
		virtual public void send_data(byte[] b) //向设备发送数据
		{ }
	}
	public class DataSrc_udp : DataSrc //udp通信方式
	{
		const uint IOC_IN = 0x80000000;
		const uint IOC_VENDOR = 0x18000000;
		uint SIO_UDP_CONNRESET = (IOC_IN | IOC_VENDOR | 12); //由于WindowsSB必须设置一下否则出现远程主机强迫关闭了一个现有的连接
		public UdpClient udp = null; //接收数据转发
		public Socket_cfg cfg; //udp的配置，缓存，用于重连
		public bool is_reopen = false; //是否正在重连
		public delegate void RX_FUN(byte[] buf);
		public IPEndPoint rmt_addr = new IPEndPoint(0, 0); //接收到数据后，对方的地址
		public DataSrc_udp(RX_CB cb) : base(cb) { }
		public override void fromDict(Dictionary<string, object> v)
		{
			base.fromDict(v);
			cfg.ip = (string)v["ip"];
			cfg.port = (ushort)v["port"];
			if (v.ContainsKey("rmt_ip")) cfg.rmt_ip = (string)v["rmt_ip"];
			if (v.ContainsKey("rmt_port")) cfg.rmt_port = (ushort)v["rmt_port"];
		}
		void udp_rx_cb(IAsyncResult ar) //接收失败后进行重连
		{
			IPEndPoint ipend = null;
			byte[] buf = null;
			try
			{
				buf = udp.EndReceive(ar, ref ipend);
			}
			catch (Exception e)
			{
				reopen(true);
				return;
			}
			try
			{
				if (buf != null)
				{
					rmt_addr = ipend;
					rx_event(buf);
				}
			}
			catch (Exception e) { } //用户的错不管
			try
			{
				udp.BeginReceive(udp_rx_cb, udp);
			}
			catch (Exception e)
			{
				reopen(true);
				return;
			}
		}
		public override void open(string s) //打开数据源，输入以什么名称打开的
		{
			udp = new UdpClient(new IPEndPoint(IPAddress.Parse(cfg.ip), cfg.port));
			udp.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
			//udp.Connect(new IPEndPoint(IPAddress.Parse(cfg.socket.rmt_ip), cfg.socket.rmt_port)); //若指定了目标，就收不到其他地址了
			udp.Client.ReceiveBufferSize = 1000000;
			udp.BeginReceive(udp_rx_cb, udp); //开始接收
		}
		public void reopen(bool block = false) //重连，输入是否阻塞
		{
			if (is_reopen) return; //正在重连，这里就不管了
			is_reopen = true;
			do
			{
				Thread.Sleep(1000);
				try
				{
					close();
					open(name);
					break; //若有错，不会进回调
				}
				catch {	}
			} while (block);
			is_reopen = false;
		}
		public override void close()
		{
			udp.Close();
			udp = null;
		}
		public override string[] get_names()
		{
			return new string[] { name };
		}
		public void send_data(string ip, int port, byte[] b)
		{
			udp.Send(b, b.Length, ip, port);
		}
		public override void send_data(byte[] b)
		{
			send_data(cfg.rmt_ip,cfg.rmt_port, b);
		}
	}
	public class DataSrc_uart : DataSrc //串口方式
	{
		public SerialPort uart = new SerialPort(); //串口
		public int uart_b = 115200;
		public DataSrc_uart(RX_CB cb) : base(cb)
		{
			uart.DataReceived += (sender,e) =>
			{
				int n = 0;
				byte[] buf = new byte[0];
				try
				{
					n = uart.BytesToRead;
					buf = new byte[n];
					uart.Read(buf, 0, n);
				}
				catch
				{ }
				rx_event(buf);
			};
		}
		public override void fromDict(Dictionary<string, object> v)
		{
			base.fromDict(v);
			if (v.ContainsKey("uart_b")) uart_b = (int)v["uart_b"];
		}
		public override void open(string s) //打开数据源，输入以什么名称打开的
		{
			uart.PortName = s;
			uart.BaudRate = uart_b;
			uart.Open();
		}
		public override void close()
		{
			uart.Close();
		}
		public override string[] get_names()
		{
			return SerialPort.GetPortNames();
		}
		public override void send_data(byte[] b)
		{
			uart.Write(b, 0,b.Length);
		}
	}
	public class DataSrc_replay : DataSrc //回放模式
	{
		public DataSrc_replay(RX_CB cb) : base(cb) { name = "回放"; }
		public override void open(string s) //打开数据源，输入以什么名称打开的
		{
		}
		public override string[] get_names()
		{
			return new string[] { name };
		}
	}
	public class Socket_cfg //网络通信配置
	{
		public Socket_cfg()
		{
			ip = "127.0.0.1";
			port = 12345;
			rmt_ip = "127.0.0.1";
			rmt_port = 12346;
		}
		public string ip { set; get; }
		public ushort port { set; get; }
		public string rmt_ip { set; get; }
		public ushort rmt_port { get; set; } //对方的ip和端口
	}
}
