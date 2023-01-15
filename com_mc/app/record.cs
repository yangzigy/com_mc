using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace cslib
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct CMLOG_HEAD //日志帧头
	{
		public byte syn; //A0
		public byte type; //
		public byte len; //数据长度-1，总长度为len+7
		public byte ms_l; //毫秒最低字节
		public UInt16 ms_h; //毫秒高2字节，小端
		public bool type_bin { get { return (type & (1 << 0)) != 0; } set { type = (value ? type |= (1 << 0) : (byte)(type & (~(1 << 0)))); } } //是否为二进制
		public int vir { get { return (type & (0x0f << 4)) >> 4; } set { type = (byte)((type & 0x0f) & ((value & 0x0f) << 4)); } } //虚拟信道号
		public int ms { get { return ms_h * 256 + ms_l; } set { ms_l = (byte)(value & 0xff); ms_h = (UInt16)(value >> 8); } }
	}

#region 带时间戳的回放：分2种，二进制和文本
	public class DataSrc_replay : DataSrc //带时间戳的日志回放
	{
		public DataSrc_replay(RX_CB cb) : base(cb) { name = "回放"; }
		public bool is_bin=false; //回放数据是否是二进制的
		public float time_X = 1; //回放时间倍数
		public int state = 0;//0终止，1暂停，2回放，3单步
		public EVENT_CB open_cb = null; //打开数据源回调
		public EVENT_CB close_cb = null; //关闭数据源回调
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
		public List<byte[]> bin_lines = new List<byte[]>(); //回放数据缓存(二进制)
		public override void open(string fname) //打开数据源，输入数据文件名
		{
			line_ms_list.Clear();
			data_lines.Clear();
			bin_lines.Clear();
			if (is_bin)//若是二进制的
			{
				FileStream fs=new FileStream(fname, FileMode.Open, FileAccess.Read);
				byte[] dbuf = new byte[fs.Length];
				fs.Read(dbuf, 0,dbuf.Length);
				fs.Close();
				for (int i = 0; i < dbuf.Length - 4;) //将内存中的数据添加到行列表
				{
					uint tt = (uint)Tool.BytesToStruct(dbuf, i, typeof(uint));
					int len = (int)((tt >> 20) & 0x0fff); //数据包长度
					int ms = (int)(tt & 0x000fffff) * 10; //取得ms数
					line_ms_list.Add(ms); //加入时间戳列表
					byte[] b = new byte[len];
					i += 4;
					Array.ConstrainedCopy(dbuf, i, b, 0, len);
					i += len;
					bin_lines.Add(b); //加入数据行列表
					string sline = ""; //行的hex显示
					for (int j = 0; j < b.Length; j++)
					{
						sline += string.Format("{0:X2} ",b[j]);
					}
					sline += "\n";
					data_lines.Add(sline);
				}
			}
			else //文本型
			{
				StreamReader sw = new StreamReader(fname);
				string text = sw.ReadToEnd();
				sw.Close();
				var lines = text.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
				//建立回放数据
				for (int i = 0; i < lines.Length; i++) //1201.123	xxx,233
				{
					if (lines[i].Length < 10) continue;
					string ts = lines[i].Substring(0, 8); //取得时间戳字符
					int minute = Convert.ToInt32(ts.Substring(0, 2));
					int second = Convert.ToInt32(ts.Substring(2, 2));
					int ms = Convert.ToInt32(ts.Substring(5, 3));
					int tms = minute * 60000 + second * 1000 + ms; //转换为到小时的ms数
					line_ms_list.Add(tms); //加入时间戳列表
					string sline = lines[i].Substring(9) + "\n";
					data_lines.Add(sline); //加入数据行列表
				}
			}
			if (line_ms_list.Count <= 0) return;
			//提交回放任务
			ThreadPool.QueueUserWorkItem(delegate (object ss)
			{
				state = 1; //初始化为暂停状态
				set_replay_pos(0);
				try
				{
					while (true)
					{
						switch (state)
						{
							case 1: //暂停
								Thread.Sleep(100);
								break;
							case 2: //回放
								try_to_play();
								break;
							case 3: //单步
								try_to_play();
								state = 1; //恢复暂停状态
								break;
							default: throw new Exception("终止");//终止
						}
						Thread.Sleep(20);
					}
				}
				catch (Exception e)
				{
					state = 0;
				}
			});
			if (open_cb != null) open_cb(); //调用事件
		}
		public void try_to_play()
		{
			while (replay_line < line_ms_list.Count)
			{
				update_replay_ms(); //更新回放时间
				if (replay_ms > line_ms_list[replay_line]) //若回放时间大于数据时间，发出
				{
					if (is_bin) //若是二进制
					{
						rx_event(bin_lines[replay_line]);
					}
					else //若是文本行
					{
						var b = Encoding.UTF8.GetBytes(data_lines[replay_line]);
						rx_event(b);
					}
					replay_line++;
				}
				else break;
			}
		}
		public void set_replay_pos(int ind) //设置回放位置
		{
			if (ind < 0 || ind >= line_ms_list.Count) return;
			replay_line = ind;
			replay_ms = line_ms_list[ind]; //起始的时间位置在第一个数据处
			pre_replay_ms = DateTime.Now; //此时为时间基准
		}
		public void update_replay_ms() //累加回放时间
		{
			var d = (DateTime.Now - pre_replay_ms).TotalMilliseconds; //距上次更新的间隔时间
			replay_ms += (int)(d* time_X); //用于倍速
			pre_replay_ms = DateTime.Now; //此时为时间基准
		}
		public override void close()
		{
			stop();
			if (close_cb != null) close_cb(); //调用事件
		}
		public void resume() //恢复
		{
			if(state==1) //若暂停了
			{
				state = 2;
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
#endregion
#region 文本文件直接全部回放
	public class DataSrc_file : DataSrc //回放模式
	{
		public DataSrc_file(RX_CB cb) : base(cb) { name = "文件"; }
		public override void open(string s)
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.txt|*.txt";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) throw new Exception("未选择文件");
			FileStream sw = new FileStream(ofd.FileName,FileMode.Open);
			byte[] buf = new byte[sw.Length];
			sw.Read(buf,0,buf.Length);
			sw.Close();
			rx_event(buf);
		}
	}
#endregion
#region 二进制带时间戳日志记录
	public class BinDataFile : LogFile //二进制协议
	{
		public DateTime cur_time = DateTime.Now; //记录文件创建时间
		public BinDataFile() { suffix = ".dat"; }
		public override void create()
		{
			base.create();
			cur_time = DateTime.Now; //父类中只有UTC秒的记录
		}
		public override void log_pass(byte[] b, int ind, int len) //
		{
			while (len > 0)
			{
				CMLOG_HEAD head=new CMLOG_HEAD();
				head.syn = 0xa0;
				//head.vir = 1; //二进制的默认给1
				//head.type_bin = true; //二进制
				head.type = 0x11; //更高效
				int rec_len=len<= 256 ? len: 256;
				head.len = (byte)(rec_len - 1);
				int ms = (int)((DateTime.Now.Ticks - cur_time.Ticks) / 10000); //表示0001年1月1日午夜 12:00:00 以来所经历的 100 纳秒数
				head.ms = ms;
				var tb = Tool.StructToBytes(head);
				sw.Write(tb); //写入头部
				sw.Write(b, ind, rec_len); //写入数据
				sw.Flush(); //flush
				len -= rec_len;
			}
		}
		public override void log_pass(string s) //禁止文本形式写入
		{
			throw new Exception("未实现文本接口");
		}
	}
#endregion
}
