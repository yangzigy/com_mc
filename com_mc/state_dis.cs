using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Windows.Data;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms.DataVisualization;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Threading;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using cslib;

namespace com_mc
{
	public partial class MainWindow : Window
	{
		static public MainWindow mw;
		public Dictionary<string, CCmd_Button> cmd_ctrl_dict = new Dictionary<string, CCmd_Button>(); //控制控件，用于轮询
		
		public Com_MC commc=new Com_MC(); //本程序的测控总体
		
		public LogFile rec_file = new LogFile(); //文本记录文件
		public BinDataFile rec_bin_file = new BinDataFile(); //二进制记录文件
		public object[] invokeobj=new object[2];
		public Dictionary<string, Series> series_map=new Dictionary<string, Series>();
		public Dictionary<string, CheckBox> checkb_map = new Dictionary<string, CheckBox>();
		public long st_ms= DateTime.Now.Ticks/10000; //曲线起始ms
		public long x_tick=0; //x轴数值
		public string x_axis_id=""; //x轴的索引变量名，空则使用时间
		public int is_first =1;
		public Timer threadTimer = null; //ui线程定时器
		public void state_dis_ini()
		{
			rec_file.ts_fmt="mmss.fff	"; //用于时间戳的时间格式，回放约定的格式
			mw = this;
			mi_menu_cmd.Click += (s, e) => { mi_menu_cmd.IsSubmenuOpen = true; };
			threadTimer = new Timer(OnTimedEvent, null, 0, 10); //100Hz
		}
		public void mc_ini() //测控界面初始化
		{
			deinit(); //先去除初始化
			commc.ini(Config.config.prot_cfg); //测控后台初始化
#region 传感参数部分
			chart1 = mainFGrid.Child as Chart;
			chart1.Legends[0].DockedToChartArea = "ChartArea1";
			chart1.Legends[0].BackColor = System.Drawing.Color.Transparent;
			//从配置中加载参数
			chart1.Series.Clear();
			foreach (var item in commc.dset)
			{
				var ds = item.Value;
				ds.update_cb = tn => { ds.update_times = 10; };//数据更新回调函数
				ds.update_dis = tn => { if (ds.update_times > 0) ds.update_times--; };
				if (ds.is_dis == false) continue;
				CheckBox cb = new CheckBox();
				cb.Content = ds.name;
				cb.IsChecked = ds.is_cv;
				//cb.Width = 150;
				cb.Background = Brushes.LightCoral;
				cb.Margin = new Thickness(2, 2, 2, 0);
				Series tmpserial = null;
				if (ds.val.type == DataType.str || ds.val.type == DataType.undef) //字符型的，不让选择曲线
				{
					ds.is_val = false;
					//cb.IsEnabled = false;
				}
				else //曲线型的
				{
					ds.is_val = true;
					if(ds.dis_data_len==0) ds.dis_data_len = Config.config.dis_data_len; //若自己没配置，用统一的
					tmpserial = new Series()
					{
						BorderWidth = 2,
						ChartArea = "ChartArea1",
						ChartType = SeriesChartType.Line,
						//Color = System.Drawing.Color.Red,
						Legend = "Legend1",
						Name = ds.name,
					};
					chart1.Series.Add(tmpserial);
					series_map[ds.name] = tmpserial;
				}
				checkb_map[ds.name] = cb;
				sp_measure.Children.Add(cb);
				ds.update_cb += (delegate (DataDes dd) //数据更新回调函数
				{
					try
					{
						//it.update_times = 10;
						if (dd.is_val && (bool)cb.IsChecked) //若显示曲线
						{
							if (is_first == 1) //首次加入数据点，清除初始化点
							{
								clear_Click(null, null);
								is_first = 0;
							}
							double d = dd.val.get_val();
							if (Math.Abs(d) >= (double)Decimal.MaxValue) throw new Exception("");
							if (x_axis_id != "" && commc.dset.ContainsKey(x_axis_id)) //若有索引列
							{
								if (tmpserial.Points.Count > 0 &&
									Math.Abs(tmpserial.Points[tmpserial.Points.Count - 1].XValue - x_tick) < 0.1f) //跟上次一样
								{
									tmpserial.Points[tmpserial.Points.Count - 1].YValues[0] = d;
								}
								else tmpserial.Points.AddXY(x_tick, d);
								if (dd.name == x_axis_id) x_tick++; //若是x轴的索引参数
							}
							else //没有索引列，就用时间ms数作为x轴
							{
								tmpserial.Points.AddXY(ticks0 - st_ms, d);
							}
							if (tmpserial.Points.Count >= dd.dis_data_len)
							{
								tmpserial.Points.RemoveAt(0);
							}
						}
					}
					catch { }
				});
				ds.update_dis += (delegate (DataDes dd) //数据更新回调函数
				{
					if (dd.update_times > 0) //若有数据更新
					{
						//it.update_times--;
						cb.Background = Brushes.LightGreen;
						cb.Content = dd.name + ":" + dd.val.ToString();
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
			sp_measure.Columns = Config.config.svar_cols;
			if (Config.config.svar_ui_h != 0) //传感变量区域高度
			{
				row_para_dis.Height = new GridLength(Config.config.svar_ui_h);
			}
			else row_para_dis.Height = new GridLength(
				(sp_measure.Children.Count + sp_measure.Columns-1) / sp_measure.Columns * 22 + 15); //行数*22
			//sp_measure.Height = row_para_dis.Height.Value;
#endregion
#region 指令ui初始化
			CCmd_para.i_on = new BitmapImage(new Uri("pack://application:,,,/pic/refresh_on.jpg"));
			CCmd_para.i_off = new BitmapImage(new Uri("pack://application:,,,/pic/refresh_off.jpg"));
			if (Config.config.cmd_ui_w > 0) //指令区域宽度
			{
				colD_cmd_ui.Width = new GridLength(Config.config.cmd_ui_w);
			}
			if (Config.config.ctrl_cols >= 3) //若是3列排布的,把默认控制按钮的位置改一下
			{
				//通用控制按钮列数增加
				for (int k = 0; k < Config.config.ctrl_cols - 2; k++)
				{
					grid_ctrl_bts.ColumnDefinitions.Add(new ColumnDefinition()); //通用控制按钮
					para_grid.ColumnDefinitions.Add(new ColumnDefinition());//配置按钮面板
				}
				//通用控制按钮位置修改
				System.Windows.Controls.Grid.SetRow(cb_datasrc, 0);
				System.Windows.Controls.Grid.SetColumn(cb_datasrc, 0);
				System.Windows.Controls.Grid.SetRow(bt_open_datasrc, 0);
				System.Windows.Controls.Grid.SetColumn(bt_open_datasrc, 1);
				System.Windows.Controls.Grid.SetRow(bt_refresh_uart, 0);
				System.Windows.Controls.Grid.SetColumn(bt_refresh_uart, 2);

				System.Windows.Controls.Grid.SetRow(checkb_rec_data, 1);
				System.Windows.Controls.Grid.SetColumn(checkb_rec_data, 0);
				System.Windows.Controls.Grid.SetRow(cb_fit_screen, 1);
				System.Windows.Controls.Grid.SetColumn(cb_fit_screen, 1);
				System.Windows.Controls.Grid.SetRow(bt_clear, 1);
				System.Windows.Controls.Grid.SetColumn(bt_clear, 2);
				//面板border加长
				System.Windows.Controls.Grid.SetColumnSpan(bd_dft_and_cfg, Config.config.ctrl_cols);
			}
			else //2列布局，也要从新写一遍
			{
				//通用控制按钮位置修改
				System.Windows.Controls.Grid.SetRow(cb_datasrc, 0);
				System.Windows.Controls.Grid.SetColumn(cb_datasrc, 0);
				System.Windows.Controls.Grid.SetRow(bt_open_datasrc, 0);
				System.Windows.Controls.Grid.SetColumn(bt_open_datasrc, 1);

				System.Windows.Controls.Grid.SetRow(bt_refresh_uart, 1);
				System.Windows.Controls.Grid.SetColumn(bt_refresh_uart, 0);
				System.Windows.Controls.Grid.SetRow(checkb_rec_data, 1);
				System.Windows.Controls.Grid.SetColumn(checkb_rec_data, 1);

				System.Windows.Controls.Grid.SetRow(cb_fit_screen, 2);
				System.Windows.Controls.Grid.SetColumn(cb_fit_screen, 0);
				System.Windows.Controls.Grid.SetRow(bt_clear, 2);
				System.Windows.Controls.Grid.SetColumn(bt_clear, 1);
				//面板border
				System.Windows.Controls.Grid.SetColumnSpan(bd_dft_and_cfg, 2);
			}
			int i = 0, j = 0; //i行，j列
			foreach (var item in Config.config.cmds) //遍历配置中的指令，加入到列表中
			{ //本来有一行
				commc.cmds[item.name] = item; //加入指令列表
				var v = CCmd_Button.bt_factory(item.type, item, para_grid);
				v.ini(ref i, ref j);
				if (j >= Config.config.ctrl_cols) //自动添加行只在控件本身放不下的情况下做。放下了就需要这里添加行
				{
					para_grid.RowDefinitions.Add(new RowDefinition());
					i++; j = 0;
				}
				cmd_ctrl_dict[item.name] = v; //加入控件列表
			}
#endregion
#region 菜单指令
			i = 0; j = 0;
			mi_menu_cmd.Header = Config.config.menu_name;
			foreach (var item in Config.config.menu_cmd)
			{ //本来有一行
				commc.cmds[item.name] = item;
				int rownu = grid_menu_cmd.RowDefinitions.Count - 1; //添加一行
				var v = CCmd_Button.bt_factory(item.type, item, grid_menu_cmd);
				v.ini(ref i, ref j);
				if (j >= Config.config.menu_cols)
				{
					grid_menu_cmd.RowDefinitions.Add(new RowDefinition());
					i++; j = 0;
				}
			}
#endregion
		}
		public void deinit() //去除初始化
		{
			commc.clear();
			checkb_map.Clear();
			series_map.Clear();

			sp_measure.Children.Clear(); //传感变量部分，不分行列，只需清除子节点

			grid_ctrl_bts.ColumnDefinitions.Clear(); //默认控制按钮，有3行，不用也不用删
			grid_ctrl_bts.ColumnDefinitions.Add(new ColumnDefinition());
			grid_ctrl_bts.ColumnDefinitions.Add(new ColumnDefinition()); //默认命令按钮列为2列

			para_grid.ColumnDefinitions.Clear(); //控制按钮
			para_grid.RowDefinitions.Clear();
			para_grid.RowDefinitions.Add(new RowDefinition()); //本来有一行
			para_grid.ColumnDefinitions.Add(new ColumnDefinition()); //本来有2列
			para_grid.ColumnDefinitions.Add(new ColumnDefinition()); //本来有2列
			para_grid.Children.Clear();

			grid_menu_cmd.RowDefinitions.Clear(); //菜单控制部分
			grid_menu_cmd.ColumnDefinitions.Clear();
			grid_menu_cmd.RowDefinitions.Add(new RowDefinition()); //本来有1行
			grid_menu_cmd.ColumnDefinitions.Add(new ColumnDefinition()); //本来有2列
			grid_menu_cmd.ColumnDefinitions.Add(new ColumnDefinition()); //本来有2列
			grid_menu_cmd.Children.Clear();

		}
		public void OnTimedEvent(object state) //100Hz
		{
			if(commc.pro_obj!=null) commc.pro_obj.so_poll_100();
		}
#region 数据接收
		public void send_cmd_str(string s) //向设备发送文本指令
		{ //支持多条指令同时发送
			string[] vs = s.Split("\n".ToCharArray(), StringSplitOptions.None);
			for(int i=0;i<vs.Length;i++)
			{
				//首先看看是不是软件指令
				if(ctrl_cmd(vs[i])) continue;
				//发送
				commc.pro_obj.send_cmd(vs[i]);
			}
		}
		public void send_data(string s) //字符串版的发送函数
		{
			var b=Encoding.UTF8.GetBytes(s+"\n");
			send_data(b);
		}
		public void send_data(byte[] b) //向设备发送数据
		{
			try
			{
				DataSrc.cur_ds.send_data(b);
			}
			catch (Exception ee)
			{
				//MessageBox.Show(ee.Message);
			}
		}
		public void rx_line(string s) //接收一行数据，必是符合通用文本行协议的
		{
			s = s.Trim();
			if (s == "") return;
			Dispatcher.BeginInvoke((EventHandler)delegate (object sd, EventArgs ea)
			{
				if ((bool)checkb_rec_data.IsChecked) //若需要记录，写文件
				{
					rec_file.log(s); //文本文件记录
				}
				try
				{
					if (ctrl_cmd(s)) return;//首先看看是不是软件指令
					ticks0 = DateTime.Now.Ticks / 10000;	//给传感变量刷新
					commc.mc_prot.pro_line(s);
				}
				catch (Exception ee)
				{
					//MessageBox.Show("message: " + ee.Message + " trace: " + ee.StackTrace);
				}
			}, invokeobj);
		}
		public void rx_pack(byte[] b, int off, int n) //接收一包数据
		{
			Dispatcher.BeginInvoke((Action)(() =>
			{
				if ((bool)checkb_rec_data.IsChecked) //若需要记录，写文件
				{
					rec_bin_file.write(b,off,n); //二进制文件的记录
				}
				try
				{
					ticks0 = DateTime.Now.Ticks / 10000;//给传感变量刷新
					commc.mc_prot.pro(b, off, n);
				}
				catch (Exception ee)
				{
					//MessageBox.Show("message: " + ee.Message + " trace: " + ee.StackTrace);
				}
			}));
		}
		int rx_Byte_1_s = 0; //每秒接收的字节数
		public void rx_fun(byte[] buf) //数据源接收回调函数
		{
			rx_Byte_1_s += buf.Length;
			commc.pro_obj.rx_fun(buf);
		}
		public long ticks0 = DateTime.Now.Ticks / 10000; //每次收到数据时更新，每个包一个ms值
#endregion
		public bool ctrl_cmd(string s) //返回是否是控制指令
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
namespace cslib
{
	public class DataSrc_replay_filedlg : DataSrc_replay //带时间戳的日志回放,打开文件对话框包裹
	{
		public DataSrc_replay_filedlg(RX_CB cb) : base(cb) { }
		public override void open(string s)
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.txt|*.txt|*.dat|*.dat";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) throw new Exception("未选择文件");
			string exs = Path.GetExtension(ofd.FileName).Trim();
			if (exs == ".dat") is_bin = true;//若是二进制的
			base.open(ofd.FileName);
		}
	}
}
