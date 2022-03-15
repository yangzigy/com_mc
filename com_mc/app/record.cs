using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace cslib
{
	public class DataSrc_replay : DataSrc //回放模式
	{
		public DataSrc_replay(RX_CB cb) : base(cb) { name = "回放"; }
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
		public override void open(string s) //打开数据源，输入以什么名称打开的
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.txt|*.txt";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) throw new Exception("未选择文件");
			StreamReader sw = new StreamReader(ofd.FileName);
			string text = sw.ReadToEnd();
			sw.Close();
			var lines = text.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
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
				int tms = minute * 60000 + second * 1000 + ms; //转换为到小时的ms数
				line_ms_list.Add(tms); //加入时间戳列表
				string sline = lines[i].Substring(9) + "\n";
				data_lines.Add(sline); //加入数据行列表
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
							case 0: throw new Exception("终止");//终止
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
			if (replay_line < line_ms_list.Count)
			{
				update_replay_ms(); //更新回放时间
				if (replay_ms > line_ms_list[replay_line]) //若回放时间大于数据时间，发出
				{
					var b = Encoding.UTF8.GetBytes(data_lines[replay_line]);
					rx_event(b);
					replay_line++;
				}
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
			replay_ms += (int)(d* time_X);
			pre_replay_ms = DateTime.Now; //此时为时间基准
		}
		public override void close()
		{
			stop();
			if (close_cb != null) close_cb(); //调用事件
		}
		public override string[] get_names()
		{
			return new string[] { name };
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
}
