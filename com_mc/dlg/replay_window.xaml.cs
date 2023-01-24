using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using cslib;

namespace com_mc
{
	/// <summary>
	/// replay_window.xaml 的交互逻辑
	/// </summary>
	public partial class Replay_Window : Window
	{
		public Replay_Window()
		{
			InitializeComponent();
		}
		public DataSrc_replay rplobj = null; //回放数据源
		public uint tick = 0;
		public ImageBrush resume_bkimg = new ImageBrush(); //恢复图标
		public ImageBrush suspend_bkimg = new ImageBrush(); //暂停图标
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			resume_bkimg.ImageSource = new BitmapImage(new Uri("pack://application:,,,/pic/3.PNG"));
			suspend_bkimg.ImageSource = new BitmapImage(new Uri("pack://application:,,,/pic/3.1.PNG"));
			new_file_loaded();
		}
		public void new_file_loaded() //新的文件加载
		{
			tb_replay_st.Text = rplobj.replay_st.ToString();
			tb_replay_end.Text = rplobj.replay_end.ToString();
			List<CCmlog_Vir_Info> tlist = new List<CCmlog_Vir_Info>();
			for (int i = 0; i < rplobj.cmlog_vir_info.Length; i++)
			{
				if (rplobj.cmlog_vir_info[i].frame_n <= 0) continue;
				tlist.Add(rplobj.cmlog_vir_info[i]);
			}
			dg_vir.ItemsSource=tlist;
		}
		private void bt_replay_cmd_Click(object sender, RoutedEventArgs e) //回放指令
		{
			//FrameworkElement fe = sender as FrameworkElement;
			Button fe=sender as Button;	
			switch (fe.Tag)
			{
				case "home": //至首
					rplobj.set_replay_pos(rplobj.replay_st);
					break;
				case "end": //至尾
					rplobj.set_replay_pos(rplobj.replay_end- 1);
					break;
				case "pre": //前一帧
					rplobj.set_replay_pos(rplobj.replay_line-1);
					if (rplobj.state == 1) rplobj.state = 3; //若是暂停的，改成单步
					break;
				case "next": //下一帧
					rplobj.set_replay_pos(rplobj.replay_line+1);
					if (rplobj.state == 1) rplobj.state = 3; //若是暂停的，改成单步
					break;
				case "resume": //恢复
					fe.Background = suspend_bkimg;
					fe.Tag = "suspend";
					fe.ToolTip = "暂停";
					rplobj.resume();
					break;
				case "suspend": //暂停
					fe.Background = resume_bkimg;
					fe.Tag = "resume";
					fe.ToolTip = "恢复";
					rplobj.suspend();
					break;
				case "set_row": //设置行
					int row = 0;
					if (int.TryParse(tb_set_row.Text, out row))
					{
						rplobj.set_replay_pos(row);
					}
					break;
			}
		}
		public void poll() //10Hz
		{
			tick++;
			if(tick%3==0) //3.3Hz
			{
				lb_row_num.Content = string.Format("{0}/{1}行", rplobj.replay_line, rplobj.line_ms_list.Count);
				sl_cur_row.Maximum = rplobj.replay_end;
				sl_cur_row.ValueChanged -= sl_cur_row_ValueChanged; //修改界面的拖动条，会触发事件形成循环
				sl_cur_row.Value = rplobj.replay_line;
				sl_cur_row.ValueChanged += sl_cur_row_ValueChanged;
				//加载原始数据，5行
				if (rplobj.line_ms_list.Count > 0)
				{
					int st = rplobj.replay_line - 2;
					int end = rplobj.replay_line + 2 + 1;
					if (st < 0) st = 0; else if (st >= rplobj.line_ms_list.Count) st = rplobj.line_ms_list.Count - 1;
					if (end < 0) end = 0; else if (end > rplobj.line_ms_list.Count) end = rplobj.line_ms_list.Count;
					string s = "";
					for(int i = st; i<end;i++)
					{
						if(i==rplobj.replay_line) //若是正要回放的行
						{
							s += "*";
						}
						s += string.Format("{0}:	{1}",i,rplobj.data_lines[i]);
					}
					tb_org_text.Text = s;
				}
			}
			//查看变速设置
			string sp = cb_speed.Text;
			if(float.TryParse(sp,out rplobj.time_X)==false)
			{
				rplobj.time_X = 10000; //一下全放出来
			}
		}
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = true;
			Visibility = Visibility.Hidden;
		}
		private void sl_cur_row_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) //滚动条拖动
		{
			rplobj.set_replay_pos((int)sl_cur_row.Value);
		}
		private void bt_st_end_Click(object sender, RoutedEventArgs e) //设置起止行
		{
			try
			{
				//从界面读取起止值，并检验
				int st = Convert.ToInt32(tb_replay_st.Text);
				int end = Convert.ToInt32(tb_replay_end.Text);
				if (st >= rplobj.line_ms_list.Count) st = rplobj.line_ms_list.Count - 1;
				if (end > rplobj.line_ms_list.Count) end = rplobj.line_ms_list.Count;
				if (st < 0 || end < st) return;
				
				rplobj.set_st_end(st, end); //设置回放引擎
				//从回放后台读取起止值，重新设置界面
				sl_cur_row.Minimum = rplobj.replay_st;
				sl_cur_row.Maximum = rplobj.replay_end;
				tb_replay_st.Text = rplobj.replay_st.ToString();
				tb_replay_end.Text = rplobj.replay_end.ToString();
			}
			catch (Exception ee)
			{ }
		}
		private void bt_search_by_ms_Click(object sender, RoutedEventArgs e) //按ms查询
		{
			try
			{
				int ms = Convert.ToInt32(tb_ms_search.Text);
				for (int i = 0; i < rplobj.line_ms_list.Count; i++)
				{
					if (rplobj.line_ms_list[i]>ms) //若有一个数据的ms值比查找值大了，就是他了
					{
						rplobj.set_replay_pos(i);
						break;
					}
				}
			}
			catch (Exception ee)
			{ }
		}
		private void bt_replay_cur_Click(object sender, RoutedEventArgs e) //回放当前帧
		{
			rplobj.replay_run_1_frame();
		}
		private void bt_update_vir_Click(object sender, RoutedEventArgs e) //更新选择
		{
			foreach (var item in dg_vir.Items)
			{
				var t = item as CCmlog_Vir_Info;
				rplobj.cmlog_vir_info[t.vir].is_sel = t.is_sel;
			}
			rplobj.update_cmlog_data();
		}
	}
}
