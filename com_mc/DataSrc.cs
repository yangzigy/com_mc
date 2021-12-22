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
			var r = Activator.CreateInstance(t,cb) as DataSrc;
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
		public Socket_cfg cfg=new Socket_cfg(); //udp的配置，缓存，用于重连
		public bool is_reopen = false; //是否正在重连
		public bool is_open = false; //是否在开的状态，若关闭，也不用重连了
		public delegate void RX_FUN(byte[] buf);
		public IPEndPoint rmt_addr = new IPEndPoint(0, 0); //接收到数据后，对方的地址
		public DataSrc_udp(RX_CB cb) : base(cb) { }
		public override void fromDict(Dictionary<string, object> v)
		{
			base.fromDict(v);
			cfg.ip = (string)v["ip"];
			int t = (int)v["port"];
			cfg.port = (ushort)t;
			if (v.ContainsKey("rmt_ip")) cfg.rmt_ip = (string)v["rmt_ip"];
			if (v.ContainsKey("rmt_port")) cfg.rmt_port = (ushort)(int)v["rmt_port"];
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
			is_open = true;
			udp.BeginReceive(udp_rx_cb, udp); //开始接收
		}
		public void reopen(bool block = false) //重连，输入是否阻塞
		{
			if (is_reopen || is_open==false) return; //正在重连，或者在关闭状态，这里就不管了
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
			is_open = false;
			if (udp!=null) udp.Close();
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
		public float time_X = 1; //回放时间倍数
		public int state = 0;//0终止，1暂停，2回放
		public int total_line=0; //回放总行数
		//数据的ms数，都换成文件内的，可以不从0开始。
		//回放位置有两个：
		// 1、查找行位置
		// 2、当前回放时间ms数
		public int replay_line=0; //回放位置
		public int replay_ms = 0; //回放的时间进度，单位ms
		public DateTime pre_replay_ms=DateTime.Now; //上次更新回放时间的点
		public int cur_line_ms = 0; //当前回放行的ms数
		public List<int> line_ms_list = new List<int>(); //每一行的ms时间戳
		public List<string> data_lines = new List<string>(); //回放数据缓存
		public override void open(string s) //打开数据源，输入以什么名称打开的
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.txt|*.txt";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) throw new Exception("未选择文件");
			StreamReader sw = new StreamReader(ofd.FileName);
			string text = sw.ReadToEnd();
			sw.Close();
			var lines = text.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
			total_line = lines.Length;
			//建立回放数据
			line_ms_list.Clear();
			data_lines.Clear();
			for (int i = 0; i < lines.Length; i++) //1201.123	xxx,233
			{
				if (lines[i].Length < 10) continue;
				string ts = lines[i].Substring(0, 8); //取得时间戳字符
				int minute = Convert.ToInt32(ts.Substring(0, 2));
				int second = Convert.ToInt32(ts.Substring(2, 2));
				int ms = Convert.ToInt32(ts.Substring(5, 3));
				int tms = minute * 60000 + second * 1000 + ms; //转换为到文件建立时的ms数
				line_ms_list.Add(tms);
				data_lines.Add(lines[i]);
			}
			if (line_ms_list.Count <= 0) return;
			//提交回放任务
			ThreadPool.QueueUserWorkItem(delegate (object ss)
			{
				state = 2;
				set_replay_pos(0);
				try
				{
					for (replay_line = 0; replay_line < lines.Length; replay_line++) //1201.123	xxx,233
					{
						if (state==0) throw new Exception("终止");
						while (replay_ms < line_ms_list[replay_line]) //若回放时间小于数据时间，等着
						{
							if (state == 0) throw new Exception("终止");
							Thread.Sleep(20);  //睡20ms
							update_replay_ms(); //更新回放时间
						}
						string sline = lines[replay_line].Substring(9)+"\n";
						var b = Encoding.UTF8.GetBytes(sline);
						rx_event(b);
						while(state==1)//判断是否是暂停
						{
							Thread.Sleep(100);
						}
					}
				}
				catch (Exception e)
				{
					state = 0;
				}
			});
		}
		public void set_replay_pos(int ind) //设置回放位置
		{
			replay_line = ind;
			replay_ms = line_ms_list[ind]; //起始的时间位置在第一个数据处
			pre_replay_ms = DateTime.Now; //此时为时间基准
		}
		public void update_replay_ms() //累加回放时间
		{
			var d = (DateTime.Now - pre_replay_ms).TotalMilliseconds; //距上次更新的间隔时间
			replay_ms += (int)(d* time_X);
			pre_replay_ms = DateTime.Now; //此时为时间基准
		}
		public override void close()
		{
			stop();
		}
		public override string[] get_names()
		{
			return new string[] { name };
		}
		public void resume() //恢复
		{
			if(state==2) //若正在运行
			{
				state = 1;
				pre_replay_ms = DateTime.Now; //此时为时间基准
			}
		}
		public void suspend() //暂停
		{
			if(state==2) state = 1;
		}
		public void stop() //终止
		{
			state = 0;
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
