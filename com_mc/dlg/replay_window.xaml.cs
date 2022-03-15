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
		}
		private void bt_replay_cmd_Click(object sender, RoutedEventArgs e) //回放指令
		{
			//FrameworkElement fe = sender as FrameworkElement;
			Button fe=sender as Button;	
			switch (fe.Tag)
			{
				case "home": //至首
					rplobj.set_replay_pos(0);
					break;
				case "end": //至尾
					rplobj.set_replay_pos(rplobj.line_ms_list.Count-1);
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
				sl_cur_row.Maximum = rplobj.line_ms_list.Count;
				sl_cur_row.ValueChanged -= sl_cur_row_ValueChanged;
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
	}
}
