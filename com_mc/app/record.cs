using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace cslib
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct CMLOG_HEAD //日志帧头:8 Byte
	{
		public byte syn; //A0
		public byte type; //高4bit为虚拟信道号
		public UInt16 len; //数据长度，总长度为len+8
		public int ms; //小端，最后一个字节是高位，可能为0
		public bool type_bin { get { return (type & (1 << 0)) != 0; } set { type = (value ? type |= (1 << 0) : (byte)(type & (~(1 << 0)))); } } //是否为二进制
		public int vir { get { return (type & (0x0f << 4)) >> 4; } set { type = (byte)((type & 0x0f) | ((value & 0x0f) << 4)); } } //虚拟信道号
	}
	public class CMLOG_ROW //cmlog行定义
	{
		public CMLOG_HEAD h; //行中的头部
		public byte[] b; //行中的数据
	}
	public class CCmlog_Vir_Info //cmlog日志的虚拟信道信息
	{
		public bool is_sel { get; set; } = true; //是否选中了
		public int vir { get; set; } //虚拟信道号
		public int frame_n { get; set; } = 0; //帧数
		public int len { get; set; } = 0; //总长
		public CCmlog_Vir_Info() { }
		public CCmlog_Vir_Info(int i)
		{
			vir = i;
		}
	}
	public class DataSrc_replay : DataSrc //带时间戳的日志回放
	{
#region 带时间戳的回放：分2种，二进制和文本
		public DataSrc_replay(RX_CB cb) : base(cb) { name = "回放"; }
		public string rpl_filename = ""; //回放数据文件名
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
		public DateTime pre_replay_ms=DateTime.UtcNow; //上次更新回放时间的点
		public int cur_line_ms = 0; //当前回放行的ms数

		public byte[] org_data= null; //cmlog的原始数据记录
		public List<int> line_ms_list = new List<int>(); //每一行的ms时间戳
		public List<string> data_lines = new List<string>(); //文本格式回放数据缓存，若是二进制，则为hex显示数据
		public List<CMLOG_HEAD> line_cmlog_list = new List<CMLOG_HEAD>(); //cmlog的每一行的头
		public List<byte[]> bin_lines = new List<byte[]>(); //回放数据缓存(二进制)

		public int replay_st = 0;
		public int replay_end = 0; //回放的起始偏移和终止长度

		public CCmlog_Vir_Info[] cmlog_vir_info= new CCmlog_Vir_Info[16]; //cmlog格式的各虚拟信道的信息
		public override void open(string fname) //打开数据源，输入数据文件名
		{
			rpl_filename=fname;
			line_ms_list.Clear();
			data_lines.Clear();
			bin_lines.Clear();
			for (int i = 0; i < cmlog_vir_info.Length; i++) cmlog_vir_info[i] = new CCmlog_Vir_Info(i); //先清除
			if (is_bin)//若是二进制的
			{
				FileStream fs=new FileStream(fname, FileMode.Open, FileAccess.Read);
				org_data = new byte[fs.Length];
				fs.Read(org_data, 0, org_data.Length);
				fs.Close();
				//cmlog格式
				update_cmlog_data();
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
			replay_st = 0;
			replay_end = line_ms_list.Count; //设置起止位置
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
					//state = 0;
				}
			});
			if (open_cb != null) open_cb(); //调用事件
		}
		public void update_cmlog_data() //修改虚拟信道选择后更新cmlog的数据
		{
			line_ms_list.Clear();
			data_lines.Clear();
			bin_lines.Clear();
			line_cmlog_list.Clear();
			for (int i = 0; i < org_data.Length - 6;) //将内存中的数据添加到行列表
			{
				CMLOG_HEAD h = (CMLOG_HEAD)Tool.BytesToStruct(org_data, i, typeof(CMLOG_HEAD));
				if(h.syn!=0xa0) //对于错乱的数据，通过找头，能解出一部分，也不保证正确
				{
					i++; continue;
				}
				int len = h.len; //len这个域是长度
				int ms = h.ms;
				if (!cmlog_vir_info[h.vir].is_sel) //若没选中，需要跳过
				{
					i += Marshal.SizeOf(h) + len;
					continue;
				}
				line_ms_list.Add(ms); //加入时间戳列表
				cmlog_vir_info[h.vir].frame_n++; //累加虚拟信道的帧数
				cmlog_vir_info[h.vir].len += len;
				line_cmlog_list.Add(h);
				byte[] b = new byte[len];
				i += Marshal.SizeOf(h);
				Array.ConstrainedCopy(org_data, i, b, 0, len);
				i += len;
				bin_lines.Add(b); //加入数据行列表
				string sline = ""; //行的hex显示
				if (h.type_bin) //若是二进制
				{
					for (int j = 0; j < b.Length; j++)
					{
						sline += string.Format("{0:X2} ", b[j]);
					}
					sline += "\n";
				}
				else
				{
					sline = Encoding.UTF8.GetString(b); //文本的直接显示
					if (!sline.EndsWith("\n")) sline += "\n"; //带换行
				}
				data_lines.Add(sline);
			}
			replay_st = 0;
			replay_end = line_ms_list.Count; //设置起止位置
		}
		public void try_to_play()
		{
			try //让回放帧的错误不影响回放流程
			{
				while (replay_line < replay_end)
				{
					update_replay_ms(); //更新回放时间
					if (replay_ms > line_ms_list[replay_line]) //若回放时间大于数据时间，发出
					{
						replay_run_1_frame();
						replay_line++;
					}
					else break;
					if (state != 2) break;//若是单帧播放，或者停止了
				}
			}
			catch (Exception e) { }
		}
		public void replay_run_1_frame() //回放当前帧，不更新状态，慎重调用
		{
			if (is_bin) //若是二进制
			{
				rx_event(bin_lines[replay_line]); //调用数据源的回调函数
			}
			else //若是文本行
			{
				var b = Encoding.UTF8.GetBytes(data_lines[replay_line]);
				rx_event(b); //调用数据源的回调函数
			}
		}
		public void set_replay_pos(int ind) //设置回放位置
		{
			if (ind < replay_st || ind >= replay_end) return;
			replay_line = ind;
			replay_ms = line_ms_list[ind]; //起始的时间位置在第一个数据处
			pre_replay_ms = DateTime.UtcNow; //此时为时间基准
		}
		public void update_replay_ms() //累加回放时间
		{
			var d = (DateTime.UtcNow - pre_replay_ms).TotalMilliseconds; //距上次更新的间隔时间
			replay_ms += (int)(d* time_X); //用于倍速
			pre_replay_ms = DateTime.UtcNow; //此时为时间基准
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
				pre_replay_ms = DateTime.UtcNow; //此时为时间基准
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
		public void set_st_end(int st,int end) //设置起止行
		{
			replay_st= st; replay_end= end;
			int new_rpl_line = replay_line;
			if (new_rpl_line < st) new_rpl_line = st;
			else if (new_rpl_line >= end) new_rpl_line = end - 1;
			set_replay_pos(new_rpl_line);
		}
#endregion
#region 数据导出
		public List<byte> export_org() //将当前选中的数据导出成原始数据
		{
			List<byte> ret=new List<byte>();
			for (int i = replay_st; i < replay_end; i++)
			{
				if (is_bin) ret.AddRange(bin_lines[i]); //若是二进制
				else
				{
					var b = Encoding.UTF8.GetBytes(data_lines[i]);
					ret.AddRange(b);
				}
			}
			return ret;
		}
		public List<byte> export_cmlog() //将当前选中的数据导出成cmlog格式
		{
			List<byte> ret = new List<byte>();
			if (is_bin) //若是二进制
			{
				for (int i = replay_st; i < replay_end; i++)
				{
					var tb = Tool.StructToBytes(line_cmlog_list[i]);
					ret.AddRange(tb); //写入头部
					ret.AddRange(bin_lines[i]); //
				}
			}
			else
			{
				for (int i = replay_st; i < replay_end; i++)
				{
					var b = Encoding.UTF8.GetBytes(data_lines[i]);
					
					int len = b.Length;
					int ind = 0;
					while (len > 0)
					{
						CMLOG_HEAD head = new CMLOG_HEAD();
						head.syn = 0xa0;
						//head.vir = 0; //
						//head.type_bin = true; //二进制
						head.type = 0x0; //更高效
						int rec_len = len <= (65536 - 8) ? len : (65536 - 8); //为了总长64K以内，稍微小一点
						head.len = (UInt16)(rec_len);
						head.ms = line_ms_list[i];
						var tb = Tool.StructToBytes(head);
						ret.AddRange(tb); //写入头部
						for (int j = 0; j < rec_len; j++)
						{
							ret.Add(b[j+ind]);
						}
						len -= rec_len;
						ind += rec_len;
					}
				}
			}
			return ret;
		}
		public List<byte> export_timetext() //将当前选中的数据导出成带时间戳文本格式
		{ //文本数据输入，导出timetext没问题，cmlog的二进制输入，导出时按hex，cmlog的文本输入，按文本导出
			List<byte> ret = new List<byte>();
			for (int i = replay_st; i < replay_end; i++)
			{
				string s="";
				if(is_bin) //若是二进制格式，查看具体数据帧是文本还是二进制
				{
					if(line_cmlog_list[i].type_bin) s = data_lines[i]; //若是二进制，这个缓存里是hex
					else s = Encoding.UTF8.GetString(bin_lines[i]); //若是文本
				}
				else s = data_lines[i]; //文本数据输入，直接输出
				int sec = line_ms_list[i] / 1000;
				int min = sec / 60;
				DateTime dt = new DateTime(1970, 1, 1, 0, min, sec%60, line_ms_list[i]%1000);
				string tmp_time = dt.ToString("mmss.fff	"); //时间戳
				s = tmp_time + s;
				var tb = Encoding.UTF8.GetBytes(s);
				ret.AddRange(tb); //
			}
			return ret;
		}
#endregion
	}
#region 原始数据文件直接全部回放
	public class DataSrc_file : DataSrc //回放模式
	{
		public DataSrc_file(RX_CB cb) : base(cb) { name = "文件"; }
		public override void open(string s)
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.org|*.org";
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
		public DateTime cur_time = DateTime.UtcNow; //记录文件创建时间
		public BinDataFile() { suffix = ".cmlog"; }
		public override void create()
		{
			base.create();
			cur_time = DateTime.UtcNow; //父类中只有UTC秒的记录
		}
		public override void log_pass(byte[] b, int ind, int len) //
		{
			throw new Exception("未实现直接记录接口");
			//log_cmlog(b, ind, len,0);
		}
		public void log_cmlog(byte[] b, int ind, int len,int vir,bool type_bin) //输入信道号
		{
			update_file_name();
			while (len > 0)
			{
				CMLOG_HEAD head = new CMLOG_HEAD();
				head.syn = 0xa0;
				head.vir = vir; //
				head.type_bin = type_bin; //二进制
				//head.type = 0x11; //更高效
				int rec_len = len <= (65536-8) ? len : (65536 - 8); //本次实际要写入的数据长度(为了总长64K以内，稍微小一点)
				head.len = (UInt16)(rec_len);
				int ms = (int)((DateTime.UtcNow.Ticks - cur_time.Ticks) / 10000); //表示0001年1月1日午夜 12:00:00 以来所经历的 100 纳秒数
				head.ms = ms;
				var tb = Tool.StructToBytes(head);
				sw.Write(tb); //写入头部
				sw.Write(b, ind, rec_len); //写入数据
				sw.Flush(); //flush
				len -= rec_len;
				ind += rec_len;
			}
		}
		public override void log_pass(string s) //禁止文本形式写入
		{
			throw new Exception("未实现文本接口");
		}
	}
#endregion
}
