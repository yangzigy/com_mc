using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace lgd_gui
{
	public enum SocketType //通信类型
	{
		none, udp, tcp_sv, tcp_cl
	}
	public class DataSrc //数据源
	{
		static public DataSrc factory(SocketType t) //工厂方法
		{
			switch (t)
			{
				case SocketType.udp:
					return new DataSrc_udp();
			}
			return null;
		}
		public delegate void uart_rx_fun(byte[] b);
		public uart_rx_fun uart_rx_event; //串口接收事件
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
		void udp_rx_cb(IAsyncResult ar)
		{
			IPEndPoint ipend = null;
			byte[] buf;
			try
			{
				buf = udp.EndReceive(ar, ref ipend);
				uart_rx_event(buf);
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
}
