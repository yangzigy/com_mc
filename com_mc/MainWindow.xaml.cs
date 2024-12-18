using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms.DataVisualization;
using cslib;
using System.Threading;
using System.Windows.Forms.DataVisualization.Charting;

namespace com_mc
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window
	{
		public ConfigList cfglist = null;
		System.Windows.Forms.DataVisualization.Charting.Chart chart1;
		public Timer timer10Hz;
		public Replay_Window rpl_win = new Replay_Window(); //回放对话框
		public Prot_Cfg_Window prot_win = new Prot_Cfg_Window(); //协议编辑对话框
		public uint tick = 0;

		public Dictionary<string, DataSrc> ds_tab = new Dictionary<string, DataSrc>(); //字符与数据源的对应关系
		public MainWindow()
		{
			//首先保存错误
			var domain = AppDomain.CurrentDomain;
			domain.UnhandledException += (sender, targs) =>
			{
				Console.WriteLine(Config.config.ToString());
				var ex = targs.ExceptionObject as Exception;
				if (ex != null)
				{
					MessageBox.Show("message: " + ex.Message + " trace: " + ex.StackTrace);
					//log("message: " + ex.Message + " trace: " + ex.StackTrace);
				}
			};
			InitializeComponent();
		}
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
			//初始化界面
			chart1 = mainFGrid.Child as Chart;
			chart1.Legends[0].DockedToChartArea = "ChartArea1";
			chart1.Legends[0].BackColor = System.Drawing.Color.Transparent;
			Dictionary<string, object> td = new Dictionary<string, object>();
			td["type"] = "replay_filedlg";
			rpl_win.rplobj = DataSrc.factory(td, rx_fun) as DataSrc_replay_filedlg; //记录回放对象
			state_dis_ini();
			//加载配置文件列表
			cfglist = ConfigList.load(AppDomain.CurrentDomain.BaseDirectory + "/cm_cfgs.txt");
			for (int i=0;i< cfglist.cfgs.Count;i++)
			{
				var me = new MenuItem();
				me.Header = cfglist.cfgs[i].des;
				me.Tag = cfglist.cfgs[i]; //将配置属性直接写在Tag里
				me.Click += (RoutedEventHandler)delegate(object se, RoutedEventArgs ee)
				{
					ini_by_config(((Config_Prop)(((MenuItem)se).Tag)).fname); //按这个配置初始化
				};
				mi_file.Items.Add(me);
			}
			//加载默认配置文件
			//string configfilename = AppDomain.CurrentDomain.BaseDirectory + "/config.txt";
			ini_by_config(cfglist.cfgs[0].fname); //默认按配置列表的第一项初始化，即使没有cm_cfgs.txt，第一项也会默认一个config.txt
			prot_win.cur_prot = commc.mc_prot; //将当前在用的协议引用给协议编辑对话框

			timer10Hz = new Timer((TimerCallback)delegate (object state)
			{
				Dispatcher.Invoke((EventHandler)delegate (object sd, EventArgs ea)
				{
					foreach (var item in commc.dset) //刷新每个参数
					{
						item.Value.update_dis(item.Value); //周期刷新，输入名称给指令对象索引，本身只需更新刷新计数
					}
					foreach (var item in cmd_ctrl_dict) //对于每个控制控件，执行poll
					{
						item.Value.poll();
					}
					tick++;
					if (tick % 5 == 0) //2Hz
					{
						if (cb_fit_screen.IsChecked == true) fit_screen();
						else fit_screen_data();
					}
					else if (tick % 10 == 1) //1Hz
					{
						lb_rx_Bps.Content = string.Format("接收:{0} Bps", rx_Byte_1_s);
						rx_Byte_1_s = 0;
					}
					if (tick % 2 == 0) //5Hz
					{
						foreach (var item in series_map) //遍历所有曲线，看是否添加到控件
						{
							var mco = commc.dset[item.Key];  //测控对象
							mco.is_cv = (bool)(checkb_map[item.Key].IsChecked); //设置曲线标志
							bool is_display = chart1.Series.Contains(item.Value); //是否已经显示了曲线
							if (mco.is_cv) //若要显示曲线
							{
								if (!is_display) //若还没加入
								{
									chart1.Series.Add(item.Value);
								}
							}
							else //若不显示曲线
							{
								chart1.Series.Remove(item.Value);
							}
						}
						//查看记录模式
						int tmp_rec_mod = 0; //临时的记录格式
						if (rb_rec_org.IsChecked == true)
						{
							tmp_rec_mod = 1; //记录原始数据
							if (checkb_rec_data.Content as string != "记录org") checkb_rec_data.Content = "记录org";
						}
						else if (rb_rec_timetext.IsChecked == true)
						{
							tmp_rec_mod = 2; //记录带时间戳的文本
							if (checkb_rec_data.Content as string != "记录ttlog") checkb_rec_data.Content = "记录ttlog";
						}
						else if (rb_rec_cmlog.IsChecked == true)
						{
							tmp_rec_mod = 3;
							if (checkb_rec_data.Content as string != "记录cmlog") checkb_rec_data.Content = "记录cmlog";
						}
						//设置记录
						if (checkb_rec_data.IsChecked == true)
						{
							switch (tmp_rec_mod)
							{
								case 1:
									rec_mod = 0; //先停止记录
									rec_text.suffix = ".org";
									rec_mod = 1; //记录原始数据
									break;
								case 2:
									rec_mod = 0; //先停止记录
									rec_text.suffix = ".ttlog";
									rec_mod = 2; //记录带时间戳的文本
									break;
								case 3: rec_mod = 3; break;
								default: break;
							}
						}
						else
						{
							rec_mod = 0; //不记录
							rec_text.close(); //让日志从新记一个
							rec_bin_file.close();
						}
					}
					try //回放对话框的显示处理
					{
						rpl_win.poll(); //10Hz
					}
					catch (Exception ee)
					{
					}
				}, invokeobj);
			}, this, 0, 100);
			string[] args = Environment.GetCommandLineArgs(); //取得参数
			if(args.Length == 2) //若是打开文件
			{
				try
				{
					FileInfo file= new FileInfo(args[1]);
					if (file.Exists == true && file.Extension==".cmlog") 
					{
						//在列表中找到“回放”
						var list = cb_datasrc.Items;
						for (int i=0;i<list.Count;i++) 
						{
							if (list[i] as string =="回放")
							{
								cb_datasrc.SelectedIndex= i;
								(rpl_win.rplobj as DataSrc_replay_filedlg).open_direct_fn = args[1];
								btnConnCom_Click(bt_open_datasrc, null); //打开数据源
								break;
							}
						}
					}
				}
				catch (Exception ee)
				{
					MessageBox.Show("输入文件名错误");
				}
			}
		}
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Environment.Exit(0);
		}
		void ini_by_config(string fname) //从配置文件初始化程序，输入配置文件名
		{
			Config.configPath = fname; //记录配置文件名
			//首先按字典读入，便于为了多配置文件综合，并能给协议部分动态结构初始化用
			Config.cfg_dict = Tool.load_json_from_file<Dictionary<string, object>>(fname);
			if(Config.cfg_dict.ContainsKey("ext_cfg_files")) //若有额外配置文件
			{ //加载额外配置
				ArrayList list = Config.cfg_dict["ext_cfg_files"] as ArrayList;
				foreach (var item in list)
				{
					string tf = item as string;
					string s = Tool.relPath_2_abs(Config.configPath, tf); //都是以配置文件为基础的
					object t = Tool.load_json_from_file<Dictionary<string, object>>(s);
					Tool.dictinary_update(ref t, Config.cfg_dict); //用软件配置更新协议配置文件里加载的配置
					Config.cfg_dict = t as Dictionary<string, object>;
				}
				//将主配置字典转换为字符串
				string mainjsonstr = Tool.json_ser.Serialize(Config.cfg_dict);
				Config.config = Tool.json_ser.Deserialize<Config>(mainjsonstr);
			}
			else Config.config = Config.load(fname); //加载主配置文件
			//首先清除上一个配置
			bt_open_datasrc.Content = "关闭端口";
			btnConnCom_Click(bt_open_datasrc, null); //关闭数据源
			DataSrc.dslist.Clear();
			rpl_win.rplobj.close();
			//开始配置
			Config.config.plugin_path=Tool.relPath_2_abs(fname, Config.config.plugin_path);//把插件目录的位置变为绝对路径
			//加载数据源
			if (Config.config.data_src != null)
			{
				foreach (var item in Config.config.data_src) //对于每一种配置的数据源
				{
					var ds = DataSrc.factory(item, rx_fun);
					DataSrc.dslist.Add(ds);
				}
			}
			DataSrc.dslist.Add(rpl_win.rplobj);
			rpl_win.rplobj.open_cb = () => bt_replay_dlg_Click(null, null);
			Dictionary<string, object> td = new Dictionary<string, object>();
			td["type"] = "file";
			var fo = DataSrc.factory(td, rx_fun) as DataSrc_file; //文件输入对象
			DataSrc.dslist.Add(fo);

			Title = Config.config.title_str;
			// 获取COM口列表
			bt_refresh_uart_Click(null, null);
			if (Config.config.mv_w != 0) Width = Config.config.mv_w;
			if (Config.config.mv_h != 0) Height = Config.config.mv_h;
			//指令基类静态变量配置
			CCmd_Button.bt_margin_len = Config.config.bt_margin;
			CCmd_Button.ctrl_cols = Config.config.ctrl_cols;
			CCmd_Button.cmds = commc.cmds;
			CCmd_Button.dset = commc.dset;
			CCmd_Button.send_cmd_str = send_cmd_str;
			//测控部分初始化
			mc_ini(); //内部先清除上一个配置
		}
#region click
		private void bt_load_config_Click(object sender, RoutedEventArgs e) //加载指定配置
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.txt|*.txt|*.cfg|*.cfg";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
			ini_by_config(ofd.FileName);
		}
		private void bt_save_curve_data_Click(object sender, RoutedEventArgs e) //保存曲线数据
		{
			var ofd = new System.Windows.Forms.SaveFileDialog();
			ofd.Filter = "*.csv|*.csv";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
			StreamWriter sw = null;
			try
			{
				sw = new StreamWriter(ofd.FileName);
			}
			catch (Exception ex) { MessageBox.Show(ex.Message); return; }
			//由于x轴一定是整数，所以按x轴作为索引。取得曲线的x轴极值坐标
			fit_screen_data();
			Int64 i = 0;
			//遍历所有曲线的标题，写入文件，按标题来取值
			string s = "";
			var cv_id = new List<int>(); //参与输出的曲线的名称，按顺序
			for(int j=0;j<chart1.Series.Count;j++)
			{
				if(chart1.Series[j].Points.Count>0) //若此曲线有点
				{
					cv_id.Add(j);
					s += chart1.Series[j].Tag as string + ","; //注意曲线的名字需要用tag，name带下划线
				}
			}
			if (cv_id.Count == 0) return;
			s = s.Substring(0, s.Length - 1);
			sw.WriteLine(s);
			var mi = new int[cv_id.Count]; //各曲线的索引
			for (i = (Int64)(curv_x_min-1); i < curv_x_max+1; i++) //i为横坐标值，同一个ms值只采一次
			{
				s = "";
				int flag = 0; //是否有曲线输出了值
				int mult_flag = 0; //是否有曲线在此ms值上有多个值
				for(int j =0;j<cv_id.Count;j++) //遍历所有曲线
				{
					var ser = chart1.Series[cv_id[j]];
					int tmp_mult = 0; //此曲线上需要检测下一个值看是否是这个ms值
					for (; mi[j] < ser.Points.Count; mi[j]++)
					{
						int cur_x_val = (int)(ser.Points[mi[j]].XValue);
						if (cur_x_val > i) break; //若还没到，就过
						if (cur_x_val == i) //就是现在要的这个值
						{
							if (tmp_mult > 0) //若已经取得了本ms的值，现在是想看看下一个值
							{
								//mi[j]--;
								mult_flag = 1; //标志有曲线有多值
								break;
							}
							s += string.Format("{0}", ser.Points[mi[j]].YValues[0]);
							flag = 1;
							tmp_mult = 1; //需要看下一个点是否是本ms
							continue;
						}
					}
					s += ","; //每个曲线加一个逗号
				}
				if (flag== 0) continue; //当前ms数上没有值
				if(mult_flag>0) //若此ms值上有多值，还得来一次
				{
					i--;
				}
				s=s.Substring(0, s.Length - 1);
				sw.WriteLine(s);
			}
			sw.Close();
		}
		private void bt_load_curve_data_Click(object sender, RoutedEventArgs e) //加载曲线数据
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.csv|*.csv";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
			StreamReader sw = new StreamReader(ofd.FileName);
			string text = sw.ReadToEnd();
			sw.Close();
			var lines = text.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
			if(lines.Length<=1) //只有标题，不加载
			{
				MessageBox.Show("无数据");
				return;
			}
			var tags = lines[0].Split(",".ToCharArray(), StringSplitOptions.None); //要带着空位
			var cv_ind = new int[tags.Length]; //曲线索引的列表
			for(int i=0;i<tags.Length;i++) //看看数据的标题是否都在曲线中
			{
				int j;
				cv_ind[i] = -1;
				for (j = 0; j < chart1.Series.Count; j++)
				{
					if (chart1.Series[j].Tag as string == tags[i]) //若是这个曲线， 注意曲线的名字需要用tag，name带下划线
					{
						cv_ind[i] = j;
						break;
					}
				}
			}
			clear_data(); //首先清空数据
			Title= Config.config.title_str+":"+ ofd.FileName;
			for (int i = 1; i < lines.Length; i++)
			{
				tags = lines[i].Split(",".ToCharArray(), StringSplitOptions.None); //要带着空位
				for (int j = 0; j < tags.Length; j++)
				{
					if(cv_ind[j]<0) continue; //若没有对应的曲线
					var ser = chart1.Series[cv_ind[j]];
					double y = 0;
					if (!double.TryParse(tags[j], out y)) continue;
					ser.Points.AddXY(i,y);
				}
			}
		}
		private void bt_refresh_uart_Click(object sender, RoutedEventArgs e) //建立数据源的名称列表
		{
			List<string> dsrclist = new List<string>();
			foreach (var item in DataSrc.dslist)
			{
				var l=item.get_names();
				foreach (var it in l)
				{
					ds_tab[it] = item; //此名称索引到同一个数据源，例如COM1、COM2都索引到uart数据源
					dsrclist.Add(it);
				}
			}
			cb_datasrc.ItemsSource = dsrclist;
			cb_datasrc.SelectedIndex = 0;
		}
		private void btnConnCom_Click(object sender, RoutedEventArgs e)
		{
			var btn = sender as Button;
			try
			{
				if (btn.Content.ToString() == "打开端口")
				{
					string ds_name = cb_datasrc.Text;
					DataSrc.cur_ds = ds_tab[ds_name];
					DataSrc.cur_ds.open(ds_name);
					Title = Config.config.title_str + ":" + cb_datasrc.Text;
					btn.Content = "关闭端口";
				}
				else
				{
					btn.Content = "打开端口";
					if(DataSrc.cur_ds!=null) DataSrc.cur_ds.close();
				}
			}
			catch(Exception ee)
			{
				MessageBox.Show("message: " + ee.Message);
			}
		}
		private void clear_Click(object sender, RoutedEventArgs e) //清除数据
		{
			clear_data();
		}
		private void bt_fitscreen_Click(object sender, RoutedEventArgs e) //适应屏幕
		{
			fit_screen();
		}
		private void Chart_CursorPositionChanged(object sender, System.Windows.Forms.DataVisualization.Charting.CursorEventArgs e)
		{ //此函数不进，不知道为啥
			chart1.Annotations[0].Visible = true;
			System.Windows.Forms.DataVisualization.Charting.TextAnnotation ell =
				(System.Windows.Forms.DataVisualization.Charting.TextAnnotation)(chart1.Annotations[0]);
			double x = chart1.ChartAreas[0].CursorX.Position;
			double y = chart1.ChartAreas[0].CursorY.Position;
			ell.AxisX = chart1.ChartAreas[0].AxisX;
			ell.AxisY = chart1.ChartAreas[0].AxisY;
			ell.AnchorX = x;
			ell.AnchorY = y;
			ell.Text = string.Format("x:{0},y:{1}", (int)x, y);
			//chart1.Series[0].LabelToolTip = "asdf";
		}
		private void mi_help_Click(object sender, RoutedEventArgs e) //帮助按钮
		{
			var b = Properties.Resources.readme;
			var s = Encoding.UTF8.GetString(b);
			//MessageBox.Show(s);
			Dlg_help h = new Dlg_help();
			h.helptext = s;
			h.Show();
		}
		private void bt_replay_dlg_Click(object sender, RoutedEventArgs e) //显示回放对话框
		{
			rpl_win.Show();
			rpl_win.Activate();
			if (rpl_win.WindowState == WindowState.Minimized) rpl_win.WindowState = WindowState.Normal;
			rpl_win.new_file_loaded();
		}
		private void bt_prot_dlg_Click(object sender, RoutedEventArgs e) //开协议编辑器
		{
			prot_win.Show();
		}
#endregion
#region 鼠标操作
		Point pre_m = new Point(0, 0);//鼠标上次拖动的像素位置
		Point pre_left = new Point(-1, 0);//鼠标上次左键按下的位置
		double axis_x_min = 0;
		double axis_x_max = 1000;
		double axis_y_min = 0;
		double axis_y_max = 10;
		void set_chart1_range() //设置曲线的显示区域
		{
			if (axis_x_max > int.MaxValue) axis_x_max = int.MaxValue;
			if (axis_x_min < int.MinValue) axis_x_min = int.MinValue;
			if (axis_y_max > int.MaxValue) axis_y_max = int.MaxValue;
			if (axis_y_min < int.MinValue) axis_y_min = int.MinValue;
			long x0 = (long)(axis_x_min);
			long x1 = (long)(axis_x_max);
			if (x1 < x0 + 1) x1 = x0 + 1;
			if (axis_y_max < axis_y_min + 0.1) axis_y_max = axis_y_min + 0.1;
			long y0 = (long)(axis_y_min * 10);
			long y1 = (long)(axis_y_max * 10);
			chart1.ChartAreas[0].Axes[0].Minimum = x0;
			chart1.ChartAreas[0].Axes[0].Maximum = x1; //横轴是整数
			chart1.ChartAreas[0].Axes[1].Minimum = y0 / 10.0;
			chart1.ChartAreas[0].Axes[1].Maximum = y1 / 10.0;
		}
		double curv_x_max = int.MinValue, curv_y_max = int.MinValue;
		double curv_x_min = int.MaxValue, curv_y_min = int.MaxValue; //曲线极值
		void fit_screen_data() //只更新边界数据，不更新界面
		{
			curv_x_max = int.MinValue; curv_y_max = int.MinValue;
			curv_x_min = int.MaxValue; curv_y_min = int.MaxValue;
			foreach (var item in series_map) //遍历所有曲线，找极值
			{
				if (commc.dset[item.Key].is_cv == false) continue; //不显示的不管
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
			if ((int)(curv_x_max + 1.5) < (int)(curv_x_min - 1) || (int)(curv_x_max + 1.5) < 0)
			{
				return;
			}
			else if (curv_y_max < curv_y_min) return;
			axis_x_max = (int)(curv_x_max + 1.5);
			axis_x_min = (int)(curv_x_min - 1);
			axis_y_max = (int)(curv_y_max + 1.5);
			axis_y_min = (int)(curv_y_min - 1);
			set_chart1_range();
		}
		void set_legend(double x) //根据横坐标设置曲线图例值
		{
			foreach (var item in series_map) //遍历所有曲线
			{ //此处应判断曲线是否被显示了，不显示的就不处理了
				int i;
				double d = double.NaN;
				for (i = 0; i < item.Value.Points.Count; i++) //遍历本曲线的所有点
				{
					var ps = item.Value.Points;
					if (ps[i].XValue > x) //若此点大于游标，这是右侧点，找前一点作为左侧点
					{
						if (i < 1) d = double.NaN;//若没有左侧点，则为空
						else //插值
						{
							double w = ps[i].XValue - ps[i - 1].XValue; //两点x距离
							double d1 = ps[i].XValue - x; //靠右的这块距离
							if (w < 0.01 || d1 < 0.01) //若离i点很近
							{
								d = ps[i].YValues[0];
							}
							else d = (ps[i].YValues[0] * (w - d1) + ps[i - 1].YValues[0] * d1) / w;
						}
						break;
					}
				}
				var tm = commc.dset[item.Value.Tag as string]; //测控数据对象， //注意曲线的名字需要用tag，name带下划线
				item.Value.LegendText = tm.name + ":" +
					(double.IsNaN(d) ? "null" : d.ToString(string.Format("F{0}", (tm.val as ParaValue_Val).point_n)));
			}
		}
		private void Chart_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e) //曲线控件的鼠标移动
		{
			double x = e.X;
			double y = e.Y;
			if (x < 0 || x >= chart1.Width || y < 0 || y >= chart1.Height) return;
			try
			{
				double rx = chart1.ChartAreas[0].AxisX.PixelPositionToValue(x);
				double ry = chart1.ChartAreas[0].AxisY.PixelPositionToValue(y);
				if (e.Button == System.Windows.Forms.MouseButtons.Right) //右键
				{
					double rx0 = chart1.ChartAreas[0].AxisX.PixelPositionToValue(pre_m.X);
					double ry0 = chart1.ChartAreas[0].AxisY.PixelPositionToValue(pre_m.Y);
					double rdx = rx - rx0;
					double rdy = ry - ry0; //物坐标的增量
					axis_x_max -= rdx;
					axis_x_min -= rdx;
					axis_y_max -= rdy;
					axis_y_min -= rdy;
					set_chart1_range(); //设置曲线显示区
				}
				pre_m.X = x;
				pre_m.Y = y;
			}
			catch (Exception ee)
			{
			}
		}
		private void Chart_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			int d = e.Delta;
			if (d != 0) //若有鼠标滚轮
			{
				float t = d / 1000.0f; //比例
				if (t < -0.5f) t = -0.5f;
				else if (t > 0.5f) t = 0.5f; //t为正，缩小范围，相当于放大
				double lx = axis_x_max - axis_x_min;
				double ly = axis_y_max - axis_y_min; //长度
				//判断是否过大过小（以最佳显示范围为基准）
				if (lx > 2 * (curv_x_max - curv_x_min) && t < 0 &&
					ly > 2 * (curv_y_max - curv_y_min) && t < 0) return;
				if (lx < 1 && t > 0) return;
				if (ly < 0.1 && t > 0) return;
				t = 1 - t;
				double rdx = (lx * t - lx) / 2;
				double rdy = (ly * t - ly) / 2;
				axis_x_max += rdx;
				axis_x_min -= rdx;
				axis_y_max += rdy;
				axis_y_min -= rdy;
				set_chart1_range(); //设置曲线显示区
			}
		}
		DateTime tm_left_down = DateTime.UtcNow; //左键按下的时间
		static double past_rx;
		static double past_ry; //记录上次曲线区域的值（默认X、Y坐标轴下的）
		private void Chart_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			chart1.Focus(); //为了解决不聚焦不能响应滚轮的问题
			if (e.Button==System.Windows.Forms.MouseButtons.Left)
			{
				double rx = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.X);
				double ry = chart1.ChartAreas[0].AxisY.PixelPositionToValue(e.Y);
				chart1.ChartAreas[0].CursorX.Position = rx;
				chart1.ChartAreas[0].CursorY.Position = ry;
				chart1.Annotations[0].Visible = true;
				System.Windows.Forms.DataVisualization.Charting.TextAnnotation ell =
					(System.Windows.Forms.DataVisualization.Charting.TextAnnotation)(chart1.Annotations[0]);
				ell.AxisX = chart1.ChartAreas[0].AxisX;
				ell.AxisY = chart1.ChartAreas[0].AxisY;
				ell.AnchorX = rx;
				ell.AnchorY = ry;
				ell.Text = string.Format("x:{0},y:{1:0.0}", (int)rx, ry);
				if (x_axis_id == "") //若使用时间做x轴
				{
					lb_measure.Content = "dx: " + ((rx - past_rx) * 0.001f).ToString("F3") + "s dy: " + (ry - past_ry).ToString("F2");
				}
				else lb_measure.Content = "dx: " + (rx - past_rx).ToString("F0") + " dy: " + (ry - past_ry).ToString("F2");
				past_rx = rx;
				past_ry = ry;
				set_legend(rx); //根据横坐标设置曲线图例值
				//左键按下
				pre_left = new Point(e.X, e.Y);
				tm_left_down = DateTime.UtcNow; //左键按下的时间
			}
		}
		private void Chart_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if(pre_left.X>=0)
			{
				if (e.X == pre_left.X && e.Y == pre_left.Y) goto End;
				if ((DateTime.UtcNow - tm_left_down).TotalMilliseconds < 300) goto End; //若点击时间短
				try
				{
					double rx = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.X);
					double ry = chart1.ChartAreas[0].AxisY.PixelPositionToValue(e.Y);
					double rx0 = chart1.ChartAreas[0].AxisX.PixelPositionToValue(pre_left.X);
					double ry0 = chart1.ChartAreas[0].AxisY.PixelPositionToValue(pre_left.Y);
					axis_x_min = Math.Min(rx, rx0);
					axis_x_max = Math.Max(rx, rx0);
					axis_y_min = Math.Min(ry, ry0);
					axis_y_max = Math.Max(ry, ry0);
					set_chart1_range();
				}
				catch
				{ }
			}
		End:
			pre_left.X = -1; //变为无效
		}
#endregion
#region 工具click
		private void mi_log_tools_Click(object sender, RoutedEventArgs e) //日志数据格式转换
		{
			FrameworkElement fe = sender as FrameworkElement;
			switch (fe.Tag)
			{
				case "hex2bin": //hex原始数据转二进制
					Log_Tools.fun_hex2bin();
					break;
				case "bin2text": //二进制原始数据转文本（二进制文件提取）
					Log_Tools.fun_bin2text();
					break;
				case "bin2cmlog": //定长帧转cmlog
					Log_Tools.fun_bin2cmlog();
					break;
				case "file_merge": //任意2文件合并
					Log_Tools.fun_file_merge();
					break;
				case "merge_cmlog": //cmlog文件合并
					Log_Tools.fun_merge_cmlog();
					break;
				case "cmlog_vir": //cmlog信道号修改
					Log_Tools.fun_cmlog_vir_change();
					break;
				case "cmlog_time": //cmlog修改基准时间戳
					Log_Tools.fun_cmlog_time();
					break;
			}
		}
#endregion
	}
}
