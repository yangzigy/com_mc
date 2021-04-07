using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Data;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms.DataVisualization;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Threading;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace com_mc
{
	public partial class MainWindow : Window
	{
		static public MainWindow mw;
		public Com_MC commc = new Com_MC(); //通用测控对象

		TextDataFile rec_file = new TextDataFile();
		object[] invokeobj=new object[2];
		Dictionary<string, Series> series_map=new Dictionary<string, Series>();
		Dictionary<string, CheckBox> checkb_map = new Dictionary<string, CheckBox>();
		long st_ms= DateTime.Now.Ticks/10000; //曲线起始ms
		long x_tick=0; //x轴数值
		string x_axis_id=""; //x轴的索引变量名，空则使用时间
		int is_first=1;
		bool is_plugin = true; //是否有插件？
		public DispatcherTimer dispatcherTimer = null;

		void state_dis_ini()
		{
			mw = this;
#region 传感参数部分
			chart1 = mainFGrid.Child as Chart;
			chart1.Legends[0].DockedToChartArea ="ChartArea1";
			chart1.Legends[0].BackColor=System.Drawing.Color.Transparent;
			//从配置中加载参数
			chart1.Series.Clear();
			foreach (var item in config.dset)
			{
				commc.dset[item.name] = item;
				item.update_cb = tn => { item.update_times = 10; };//数据更新回调函数
				item.update_dis = tn => {if (item.update_times > 0) item.update_times--;};
				if (item.is_dis == false) continue;
				CheckBox cb = new CheckBox();
				cb.Content = item.name;
				cb.IsChecked = item.is_cv;
				cb.Width = 150;
				cb.Background = Brushes.LightCoral;
				cb.Margin = new Thickness(2, 2, 2, 0);
				Series tmpserial=null;
				if (item.dtype==DestType.str) //字符型的，不让选择曲线
				{
					//cb.IsEnabled = false;
				}
				else //曲线型的
				{
					tmpserial = new Series()
					{
						BorderWidth = 2,
						ChartArea = "ChartArea1",
						ChartType = SeriesChartType.Line,
						//Color = System.Drawing.Color.Red,
						Legend = "Legend1",
						Name = item.name,
					};
					//chart1.Series.Add(tmpserial);
					series_map[item.name] = tmpserial;
				}
				checkb_map[item.name] = cb;
				sp_measure.Children.Add(cb);
				item.update_cb+=(delegate(string tn) //数据更新回调函数
				{
					try
					{
						var it = commc.dset[tn];
						//it.update_times = 10;
						if (it.dtype == DestType.val && (bool)cb.IsChecked) //若显示曲线
						{
							if (is_first == 1) //首次加入数据点，清除初始化点
							{
								clear_Click(null, null);
								is_first = 0;
							}
							double d = double.Parse(it.val);
							if(x_axis_id != "" && commc.dset.ContainsKey(x_axis_id)) //若有索引列
							{
								if(tmpserial.Points.Count>0 && 
									Math.Abs(tmpserial.Points[tmpserial.Points.Count-1].XValue-x_tick)<0.1f) //跟上次一样
								{
									tmpserial.Points[tmpserial.Points.Count - 1].YValues[0]= d; //更新最后一个值
								}
								else tmpserial.Points.AddXY(x_tick, d); //加入曲线
								if (tn == x_axis_id) x_tick++; //若是索引列，则x轴+1
							}
							else //没有索引列，就用时间ms数作为x轴
							{
								tmpserial.Points.AddXY(ticks0 - st_ms, d);
							}
							if (tmpserial.Points.Count >= config.dis_data_len) //若曲线数据过多，则向前移动
							{
								tmpserial.Points.RemoveAt(0);
							}
						}
					}
					catch { }
				});
				item.update_dis += (delegate (string tn) //数据更新回调函数
				{
					var it = commc.dset[tn];
					if (it.update_times > 0) //若有数据更新
					{
						//it.update_times--;
						cb.Background = Brushes.LightGreen;
						cb.Content = it.name + ":" + it.val;
					}
					else cb.Background = Brushes.LightCoral;
				});
			}
			chart1.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;

			chart1.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
			chart1.ChartAreas[0].AxisY.MajorTickMark.LineDashStyle = ChartDashStyle.DashDot;

			chart1.ChartAreas[0].Axes[0].Maximum = 100;
			chart1.ChartAreas[0].Axes[0].Minimum = 0;
			chart1.ChartAreas[0].Axes[1].Maximum = 30;
			chart1.ChartAreas[0].Axes[1].Minimum = 20;
			//chart1.Series[0].Points.Add(0);
			//chart1.Series[0].Points.Add(0);
			if(config.svar_ui_h!=0)
			{
				row_para_dis.Height = new GridLength(config.svar_ui_h);
			}
			else row_para_dis.Height = new GridLength((sp_measure.Children.Count+4)/5*20+27);
			//sp_measure.Height = row_para_dis.Height.Value;
#endregion
#region 指令ui初始化
			CCmd_para.i_on = new BitmapImage(new Uri("pack://application:,,,/pic/refresh_on.jpg"));
			CCmd_para.i_off = new BitmapImage(new Uri("pack://application:,,,/pic/refresh_off.jpg"));
			if(config.cmd_ui_w>0)
			{
				colD_cmd_ui.Width = new GridLength(config.cmd_ui_w);
			}
			if (config.ctrl_cols==3) //若是3列排布的,把默认控制按钮的位置改一下
			{ 
				//通用控制按钮加一列
				grid_ctrl_bts.ColumnDefinitions.Add(new ColumnDefinition());
				//通用控制按钮位置修改
				System.Windows.Controls.Grid.SetRow(bt_refresh_uart, 0);
				System.Windows.Controls.Grid.SetColumn(bt_refresh_uart, 2);
				System.Windows.Controls.Grid.SetRow(checkb_rec_data, 1);
				System.Windows.Controls.Grid.SetColumn(checkb_rec_data, 0);
				System.Windows.Controls.Grid.SetRow(cb_fit_screen, 1);
				System.Windows.Controls.Grid.SetColumn(cb_fit_screen, 1);
				System.Windows.Controls.Grid.SetRow(bt_clear, 1);
				System.Windows.Controls.Grid.SetColumn(bt_clear, 2);
				//面板border加长
				System.Windows.Controls.Grid.SetColumnSpan(bd_dft_and_cfg, 3);
				//配置按钮面板加一列
				para_grid.ColumnDefinitions.Add(new ColumnDefinition());
			}
			int i =0,j=0; //i行，j列
			foreach (var item in config.cmds)
			{ //本来有一行
				commc.cmds[item.name]=item;
				int rownu = para_grid.RowDefinitions.Count - 1; //添加一行
				var v=CCmd_Button.bt_factory(item.type,item, para_grid);
				v.ini(ref i, ref j);
				if(j>=config.ctrl_cols)
				{
					para_grid.RowDefinitions.Add(new RowDefinition());
					i++;j=0;
				}
			}
#endregion
#region 菜单指令
			i = 0;j = 0;
			mi_menu_cmd.Header = config.menu_name;
			mi_menu_cmd.Click += (s, e) =>  { mi_menu_cmd.IsSubmenuOpen = true; };
			foreach (var item in config.menu_cmd)
			{ //本来有一行
				commc.cmds[item.name] = item;
				int rownu = grid_menu_cmd.RowDefinitions.Count - 1; //添加一行
				var v = CCmd_Button.bt_factory(item.type, item, grid_menu_cmd);
				v.ini(ref i, ref j);
				if (j >= config.menu_cols)
				{
					grid_menu_cmd.RowDefinitions.Add(new RowDefinition());
					i++; j = 0;
				}
			}
#endregion
			_so_tx_cb = new Mingw.DllcallBack(so_tx_cb); //构造不被回收的委托
			try
			{
				Mingw.so_ini(_so_tx_cb);
			}
			catch
			{
				is_plugin = false;
			}
			dispatcherTimer = new DispatcherTimer();
			dispatcherTimer.Tick += new EventHandler(OnTimedEvent);
			dispatcherTimer.Interval = TimeSpan.FromMilliseconds(10);
			dispatcherTimer.Start();
			//配置初始化指令
			foreach (var item in config.ctrl_cmds)
			{
				ctrl_cmd(item);
			}
		}
		private void OnTimedEvent(object sender, EventArgs e)
		{
			if (is_plugin)
			{
				string s = Mingw.so_poll_100();
				rx_line(s); //是否有额外的数据过来
			}
		}
#region 串口
		Mingw.DllcallBack _so_tx_cb; //构造不被回收的委托
		int so_tx_cb(IntPtr p, int n) //构造不被回收的委托
		{
			byte[] ys = new byte[n];
			Marshal.Copy(p, ys, 0, n);
			send_data(ys);
			return 0;
		}
		public void send_cmd_str(string s) //向设备发送文本指令
		{ //支持多条指令同时发送
			string[] vs = s.Split("\n".ToCharArray(), StringSplitOptions.None);
			for(int i=0;i<vs.Length;i++)
			{
				//首先看看是不是软件指令
				if(ctrl_cmd(vs[i])) continue;
				//发送，或给插件处理
				if (is_plugin) Mingw.so_cmd(vs[i]);
				else send_data(vs[i]);
			}
		}
		void send_data(string s) //字符串版的发送函数
		{
			var b=Encoding.UTF8.GetBytes(s+"\n");
			send_data(b);
		}
		void send_data(byte[] b) //向设备发送数据
		{
			try
			{
				DataSrc.cur_ds.send_data(b);
			}
			catch (Exception ee)
			{
				MessageBox.Show(ee.Message);
			}
		}
		void rx_line(string s) //接收一行数据
		{
			if (s == "") return;
			Encoding utf8 = Encoding.UTF8;
			Encoding dft = Encoding.Default;
			byte[] temp = dft.GetBytes(s);
			s = utf8.GetString(temp);
			if ((bool)checkb_rec_data.IsChecked) //若需要记录，写文件
			{
				rec_file.write(s);
			}
			try
			{
				proc_text(s);
			}
			catch (Exception ee)
			{
				//MessageBox.Show("message: " + ee.Message + " trace: " + ee.StackTrace);
			}
		}
		List<byte> rxbuf = new List<byte>(); //串口接收缓冲
		int rx_Byte_1_s = 0; //每秒接收的字节数
		void rx_fun(byte[] buf) //数据源接收回调函数
		{
			rx_Byte_1_s += buf.Length;
			for (int i = 0; i < buf.Length; i++)
			{
				string s = "";
				if (is_plugin) s = Mingw.so_rx(buf[i]); //使用插件
				else //直接文本的形式
				{
					rxbuf.Add(buf[i]);
					if (buf[i] == 0x0a)
					{
						s = Encoding.Default.GetString(rxbuf.ToArray(), 0, rxbuf.Count);
						rxbuf.Clear();
					}
				}
				if (s != "")
				{
					Dispatcher.BeginInvoke((EventHandler)delegate (object sd, EventArgs ea)
					{
						rx_line(s);
					}, invokeobj);
				}
			}
		}
		long ticks0= DateTime.Now.Ticks / 10000; //每次收到数据时更新，每个包一个ms值
		void proc_text(string line) //处理一行传感字符
		{
			line = line.Trim();
			if (line == "") return ;
			//首先看看是不是软件指令
			if(ctrl_cmd(line)) return;
			//给传感变量刷新
			ticks0 = DateTime.Now.Ticks / 10000;
			commc.update_data(line);
		}
		double curv_x_max = int.MinValue, curv_y_max = int.MinValue;
		double curv_x_min = int.MaxValue, curv_y_min = int.MaxValue; //曲线极值
		void fit_screen_data() //只更新边界数据，不更新界面
		{
			curv_x_max = int.MinValue; curv_y_max = int.MinValue;
			curv_x_min = int.MaxValue; curv_y_min = int.MaxValue;
			foreach (var item in series_map) //遍历所有曲线，找极值
			{
				if (commc.dset[item.Key].is_cv == false) continue;
				foreach (var p in item.Value.Points)
				{
					if (p.XValue > curv_x_max) curv_x_max = p.XValue;
					if (p.YValues[0] > curv_y_max) curv_y_max = p.YValues[0];
					if (p.XValue < curv_x_min) curv_x_min = p.XValue;
					if (p.YValues[0] < curv_y_min) curv_y_min = p.YValues[0];
				}
			}
		}
		void fit_screen() //曲线范围
		{
			fit_screen_data();
			if ((int)(curv_x_max + 1.5) < (int)(curv_x_min - 1) || (int)(curv_x_max + 1.5)<0)
			{
				return;
			}
			else if (curv_y_max < curv_y_min) return;
			chart1.ChartAreas[0].Axes[0].Maximum = (int)(curv_x_max + 1.5);
			chart1.ChartAreas[0].Axes[0].Minimum = (int)(curv_x_min - 1);
			chart1.ChartAreas[0].Axes[1].Maximum = (int)(curv_y_max + 1.5);
			chart1.ChartAreas[0].Axes[1].Minimum = (int)(curv_y_min - 1);
		}
#endregion
		bool ctrl_cmd(string s) //返回是否是控制指令
		{
			bool r=false;
			if(s.Length<=0 || s[0]!='^') return r;
			string[] vs = s.Split(" ,\t".ToCharArray(), StringSplitOptions.None);
			if(vs.Length<=0) return r;
			switch (vs[0])
			{
				case "^clear": //清除当前数据
					clear_data();
					return true;
				case "^x_axis": //改变x轴坐标模式
					s = x_axis_id;
					if (vs.Length == 1) x_axis_id = "";
					else x_axis_id = vs[1];
					if (s != x_axis_id) //若发生了变化
					{
						clear_data();
					}
					return true;
			}
			return r;
		}
		public void clear_data() //清除当前曲线数据
		{
			foreach (var seri in series_map)
			{
				seri.Value.Points.Clear();
			}
			st_ms = DateTime.Now.Ticks / 10000;
			x_tick=0;
		}
	}
}
