using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace lgd_gui
{
	public enum DSType //通信类型
	{
		uart, udp, tcp_sv, tcp_cl
	}
	public class DataSrc //数据源
	{
		static public DataSrc factory(DSType t,RX_CB cb) //工厂方法
		{
			switch (t)
			{
				case DSType.uart:
					return new DataSrc_uart(cb);
				case DSType.udp:
					return new DataSrc_udp(cb);
			}
			return null;
		}
		static public Dictionary<DSType, DataSrc> dsdict = new Dictionary<DSType, DataSrc>(); //数据源列表
		static public DataSrc cur_ds; //当前数据源
		static public void ini(RX_CB cb) //初始化默认的串口数据源
		{
			cur_ds = factory(DSType.uart,cb);
			dsdict[DSType.uart]= cur_ds;
		}
		static public void open(Config cfg,string portname) //从配置或名称传入
		{
			if (portname != "socket") //若是串口
			{
				cur_ds = dsdict[DSType.uart];
				(cur_ds as DataSrc_uart).uart.PortName = portname;
			}
			else cur_ds = dsdict[cfg.socket.type]; //若是网络
			cur_ds.open(cfg);
		}

		public DataSrc(RX_CB cb)
		{
			rx_event = cb;
		}
		public delegate void RX_CB(byte[] b);
		public RX_CB rx_event; //串口接收事件
		virtual public void open(Config cfg)
		{ }
		virtual public void close()
		{ }
		virtual public void send_data(byte[] b) //向设备发送数据
		{ }
	}

	public class DataSrc_udp : DataSrc //udp通信方式
	{
		const uint IOC_IN = 0x80000000;
		const uint IOC_VENDOR = 0x18000000;
		uint SIO_UDP_CONNRESET = (IOC_IN | IOC_VENDOR | 12); //由于WindowsSB必须设置一下否则出现远程主机强迫关闭了一个现有的连接
		public UdpClient udp = null; //接收数据转发
		public DataSrc_udp(RX_CB cb) : base(cb) { }
		void udp_rx_cb(IAsyncResult ar)
		{
			IPEndPoint ipend = null;
			byte[] buf;
			try
			{
				buf = udp.EndReceive(ar, ref ipend);
				rx_event(buf);
			}
			catch (Exception e)
			{
			}
			try
			{
				udp.BeginReceive(udp_rx_cb, udp);
			}
			catch (Exception e)
			{
				return;
			}
		}
		public override void open(Config cfg)
		{
			udp = new UdpClient(new IPEndPoint(IPAddress.Parse(cfg.socket.ip), cfg.socket.port));
			udp.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
			udp.Connect(new IPEndPoint(IPAddress.Parse(cfg.socket.rmt_ip), cfg.socket.rmt_port));
			udp.Client.ReceiveBufferSize = 1000000;
			udp.BeginReceive(udp_rx_cb, udp); //开始接收
		}
		public override void close()
		{
			udp.Close();
		}
		public override void send_data(byte[] b)
		{
			udp.Send(b,b.Length);
		}
	}
	public class DataSrc_uart : DataSrc //串口方式
	{
		public SerialPort uart = new SerialPort(); //串口
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
		public override void open(Config cfg)
		{
			uart.BaudRate = cfg.uart_b;
			uart.Open();
		}
		public override void close()
		{
			uart.Close();
		}
		public override void send_data(byte[] b)
		{
			uart.Write(b, 0,b.Length);
		}
	}
}
