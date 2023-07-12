using System;
using System.Collections.Generic;
using System.IO;
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
using System.Xml.Linq;
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
		public Dictionary<string, CRpl_Para_Info> para_info_dict = new Dictionary<string, CRpl_Para_Info>(); //所有测量量的显示
		
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			resume_bkimg.ImageSource = new BitmapImage(new Uri("pack://application:,,,/pic/3.PNG"));
			suspend_bkimg.ImageSource = new BitmapImage(new Uri("pack://application:,,,/pic/3.1.PNG"));
			//加载新的回放文件
			new_file_loaded();
		}
		public void new_file_loaded() //新的文件加载
		{
			Title="relay: "+ rplobj.rpl_filename;
			//首先将界面失能，等用到的时候使能
			bt_update_vir.IsEnabled = false;
			bt_export_cmlog.IsEnabled= false;
			bt_export_org.IsEnabled= false;
			bt_export_timetext.IsEnabled= false;
			if (rplobj.line_ms_list.Count <= 0) return;
			tb_replay_st.Text = rplobj.replay_st.ToString();
			tb_replay_end.Text = rplobj.replay_end.ToString();
			List<CCmlog_Vir_Info> tlist = new List<CCmlog_Vir_Info>();
			for (int i = 0; i < rplobj.cmlog_vir_info.Length; i++)
			{
				if (rplobj.cmlog_vir_info[i].frame_n <= 0) continue;
				tlist.Add(rplobj.cmlog_vir_info[i]);
			}
			dg_vir.ItemsSource = tlist;
			//根据文件格式，确定哪个按钮能用
			bt_export_cmlog.IsEnabled = true;
			bt_export_org.IsEnabled = true;
			bt_export_timetext.IsEnabled = true;
			if (rplobj.is_bin) //若是文本的，没有更新选择按钮
			{
				bt_update_vir.IsEnabled= true;
			}
		}
		private void bt_replay_cmd_Click(object sender, RoutedEventArgs e) //回放指令
		{
			//FrameworkElement fe = sender as FrameworkElement;
			Button fe = sender as Button;
			switch (fe.Tag)
			{
				case "home": //至首
					rplobj.set_replay_pos(rplobj.replay_st);
					break;
				case "end": //至尾
					rplobj.set_replay_pos(rplobj.replay_end - 1);
					break;
				case "pre": //前一帧
					rplobj.set_replay_pos(rplobj.replay_line - 1);
					if (rplobj.state == 1) rplobj.state = 3; //若是暂停的，改成单步
					break;
				case "next": //下一帧
					rplobj.set_replay_pos(rplobj.replay_line);
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
			if (tick % 3 == 0) //3.3Hz
			{
				lb_row_num.Content = string.Format("{0}/{1}行", rplobj.replay_line, rplobj.line_ms_list.Count);
				lb_row_st.Content = string.Format("起:{0}", rplobj.replay_st);
				lb_row_end.Content = string.Format("止:{0}", rplobj.replay_end);
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
					for (int i = st; i < end; i++)
					{
						if (i == rplobj.replay_line) //若是正要回放的行
						{
							s += "*";
						}
						s += string.Format("{0}:{1}	{2}", i, rplobj.line_ms_list[i], rplobj.data_lines[i]);
					}
					tb_org_text.Text = s;
				}
			}
			//查看变速设置
			string sp = cb_speed.Text;
			if (float.TryParse(sp, out rplobj.time_X) == false)
			{
				rplobj.time_X = 10000; //一下全放出来
			}
		}
		public void save_file(string filter, Func<byte[]> fun) //打开保存文件对话框，输入处理部分，取得文件的二进制内容
		{
			var ofd = new System.Windows.Forms.SaveFileDialog();
			ofd.Filter = filter;
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
			string exs = System.IO.Path.GetExtension(ofd.FileName).Trim();
			FileStream fs = new FileStream(ofd.FileName, FileMode.Create, FileAccess.Write);
			var b=fun();
			fs.Write(b, 0, b.Length);
			fs.Close();
		}
		public void export_org() //导出为原始数据
		{
			save_file("*.org|*.org", () => rplobj.export_org().ToArray() );
		}
		public void export_cmlog() //将当前选中的数据导出成cmlog格式
		{
			save_file("*.cmlog|*.cmlog", () => rplobj.export_cmlog().ToArray() );
		}
		public void export_timetext() //将当前选中的数据导出成带时间戳文本格式
		{
			save_file("*.ttlog|*.ttlog", () => rplobj.export_timetext().ToArray() );
		}
		public void export_csv() //将当前选中的数据导出成csv
		{
			//首先建立导出对话框
			Com_Dlg export_csv_dlg = new Com_Dlg(); //导出csv对话框
			ScrollViewer sv_csvdlg = new ScrollViewer(); //导出csv对话框中的ScrollViewer
			StackPanel sp_csvdlg = new StackPanel(); //导出csv对话框中的StackPanel
			sp_csvdlg.Orientation = Orientation.Vertical; //垂直排布
			sv_csvdlg.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			sv_csvdlg.Content = sp_csvdlg;
			export_csv_dlg.grid_main.Children.Add(sv_csvdlg);
			Grid.SetColumn(sv_csvdlg, 0);
			Grid.SetRow(sv_csvdlg, 0);
			Grid.SetRowSpan(sv_csvdlg, 2);
			export_csv_dlg.Height = 500;
			export_csv_dlg.Width = 300;
			export_csv_dlg.Title = "选择导出的变量";
			//在对话框中加入参数列表
			para_info_dict.Clear();
			foreach (var item in MainWindow.mw.commc.dset)
			{
				CRpl_Para_Info info = new CRpl_Para_Info();
				info.cb = new CheckBox();
				info.cb.Content = item.Value.name;
				info.cb.Background = Brushes.LightCoral;
				info.cb.Margin = new Thickness(2, 2, 2, 0);
				para_info_dict[item.Key] = info;
				sp_csvdlg.Children.Add(info.cb);
			}
			if (export_csv_dlg.ShowDialog()==false) return;

			save_file("*.csv|*.csv", () =>
			{
				MC_Prot para_prot = new MC_Prot(); //变量和协议的整体 
				//首先从当前协议中构造新的协议对象
				para_prot.fromJson(MainWindow.mw.commc.mc_prot.toJson()); //将json转换为协议实体
				//将参数对象中的回调函数换掉
				foreach (var item in para_prot.para_dict)
				{
					item.Value.update_cb = (pv) =>
					{
						para_info_dict[pv.name].is_assigned = true;
					};
				}
				//将commc中的mc_prot域换了，并进行全速回放
				string s = "ms,"; //第一列为ms数
				//先写第一行
				List<CRpl_Para_Info> vallist = new List<CRpl_Para_Info>(); //选中的变量列表
				foreach (var item in para_info_dict)
				{
					if (item.Value.cb.IsChecked == false) continue;
					s += string.Format("{0},", item.Key);
					vallist.Add(item.Value);
					item.Value.pv = para_prot.para_dict[item.Key]; //把参数记下来，便于访问
				}
				if (vallist.Count<=0) return new byte[0]; //没有选择任何变量
				s=s.Remove(s.Length - 1)+'\n'; //将最后一个字符逗号改为换行
				//对于每一行，输出到文件
				MC_Prot tmp_commc_prot = MainWindow.mw.commc.mc_prot; //缓存软件中用的协议对象
				try
				{
					MainWindow.mw.commc.mc_prot = para_prot; //把软件中用的协议对象替换掉，借用软件的数据流
					MainWindow.mw.commc.pro_obj.ini(para_prot, MainWindow.mw.send_data, MainWindow.mw.rx_pack); //注册发送函数、接收函数

					for (int i = rplobj.replay_st; i < rplobj.replay_end; i++)
					{
						//首先清空所有变量的标志
						foreach (var item in vallist) item.is_assigned = false;
						//然后回放一行
						byte[] tb = null;
						if (rplobj.is_bin) tb = rplobj.bin_lines[i]; //若是二进制
						else tb = Encoding.UTF8.GetBytes(rplobj.data_lines[i]);
						rplobj.rx_event(tb);
						//查看所选的变量，看有哪些赋值了
						bool has_data = false; //是否有值
						foreach (var item in vallist)
						{
							if (item.is_assigned)
							{
								has_data = true;
								break;
							}
						}
						if (has_data) //若有数据，就输出一行
						{
							string ts = rplobj.line_ms_list[i].ToString()+","; //先输出ms
							foreach (var item in vallist) //输出每一列
							{
								ts += item.pv.ToString() + ",";
							}
							ts=ts.Remove(ts.Length - 1)+'\n'; //将最后一个字符逗号改为换行
							s += ts;
						}
					}
				}
				catch (Exception e)
				{
				}
				finally
				{
					MainWindow.mw.commc.mc_prot = tmp_commc_prot; //把软件中用的协议对象换回来
					MainWindow.mw.commc.pro_obj.ini(tmp_commc_prot, MainWindow.mw.send_data, MainWindow.mw.rx_pack); //注册发送函数、接收函数
				}
				byte[] b = Encoding.UTF8.GetBytes(s);
				return b;
			});
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
					if (rplobj.line_ms_list[i] > ms) //若有一个数据的ms值比查找值大了，就是他了
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
			if (rplobj.line_ms_list.Count <= 0) return;
			if (rplobj.replay_line >= rplobj.data_lines.Count) return;
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
		private void bt_export_Click(object sender, RoutedEventArgs e) //导出工具
		{
			FrameworkElement fe = sender as FrameworkElement;
			switch (fe.Tag)
			{
				case "cmlog": export_cmlog(); break;
				case "org": export_org(); break;
				case "timetext": export_timetext(); break;
				case "csv": export_csv(); break;
			}
		}
		private void tb_org_text_MouseWheel(object sender, MouseWheelEventArgs e) //原始数据显示框的鼠标滚轮
		{
			int d = e.Delta;
			sl_cur_row.Value -= Math.Round(d/100.0); //如不取整会导致上下振
		}
	}
	public class CRpl_Para_Info //回放对话框中测量量参数信息，包括选择控件和输出缓存
	{
		public CheckBox cb;
		public bool is_assigned=false; //是否被赋过值了 （仅在导出csv时）
		public ParaValue pv; //缓存变量，方便访问（仅在导出csv时）
	}
}
