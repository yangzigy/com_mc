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

namespace lgd_gui
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
				cb.IsChecked = item.dis_curve;
				cb.Width = 150;
				cb.Background = Brushes.LightCoral;
				Series tmpserial=null;
				if (item.type==DataType.t_str) //字符型的，不让选择曲线
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
					chart1.Series.Add(tmpserial);
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
						if (it.type == DataType.t_val && (bool)cb.IsChecked) //若显示曲线
						{
							if (is_first == 1) //首次加入数据点，清除初始化点
							{
								clear_Click(null, null);
								is_first = 0;
							}
							double d = double.Parse(it.val);
							if(x_axis_id != "" && commc.dset.ContainsKey(x_axis_id))
							{
								if(tmpserial.Points.Count>0 && 
									Math.Abs(tmpserial.Points[tmpserial.Points.Count-1].XValue-x_tick)<0.1f) //跟上次一样
								{
									tmpserial.Points[tmpserial.Points.Count - 1].YValues[0]= d;
								}
								else tmpserial.Points.AddXY(x_tick, d);
								if (tn == x_axis_id) x_tick++;
							}
							else
							{
								long ticks0 = DateTime.Now.Ticks / 10000;
								tmpserial.Points.AddXY(ticks0 - st_ms, d);
							}
							if (tmpserial.Points.Count >= config.dis_data_len)
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
					if (it.update_times > 0)
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
			row_para_dis.Height = new GridLength((sp_measure.Children.Count+4)/5*20+20);
			//sp_measure.Height = row_para_dis.Height.Value;
#endregion
#region 指令ui初始化
			int i =0,j=0; //i行，j列
			foreach (var item in config.cmds)
			{ //本来有一行
				commc.cmds[item.name]=item;
				int rownu = para_grid.RowDefinitions.Count - 1; //添加一行
				var v=CCmd_Button.bt_factory(item.type);
				v.grid = para_grid;
				v.ini(item, ref i, ref j);
				if(j>=2)
				{
					para_grid.RowDefinitions.Add(new RowDefinition());
					i++;j=0;
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
		}
		private void OnTimedEvent(object sender, EventArgs e)
		{
			if (is_plugin)
			{
				string s = Mingw.so_poll_100();
				rx_line(s);
			}
		}
		private void chart1_CursorPositionChanged(object sender, System.Windows.Forms.DataVisualization.Charting.CursorEventArgs e)
		{
		//	for (int i = 0; i < config.curve.Length; i++)
			{
				try
				{
					//curve_value_textboxes[i].Text=(from p in chart1.Series[i].Points
					//    where p.XValue==chart1.ChartAreas[0].CursorX.Position
					//    select p.YValues[0]).First<double>().ToString();
					//curve_value_textboxes[i].Text = chart1.Series[i].Points[(int)chart1.ChartAreas[0].CursorX.Position].YValues[0].ToString();
				}
				catch
				{
					//curve_value_textboxes[i].Text = "no data";
				}
			}
		}
#region 串口
		Mingw.DllcallBack _so_tx_cb; //构造不被回收的委托
		int so_tx_cb(IntPtr p, int n) //构造不被回收的委托
		{
			//string ss = Marshal.PtrToStringAnsi(p);
			//send_uart_data(ss);
			byte[] ys = new byte[n];
			Marshal.Copy(p, ys, 0, n);
			try
			{
				uart.Write(ys, 0, n);
			}
			catch
			{ }
			return 0;
		}
		public void send_uart_cmd(string s) //向设备发送文本指令
		{ //支持多条指令同时发送
			string[] vs = s.Split("\n".ToCharArray(), StringSplitOptions.None);
			for(int i=0;i<vs.Length;i++)
			{
				//首先看看是不是软件指令
				if(ctrl_cmd(vs[i])) continue;
				//发送，或给插件处理
				if (is_plugin) Mingw.so_cmd(vs[i]);
				else send_uart_data(vs[i]);
			}
		}
		void send_uart_data(string s) //向设备发送数据
		{
			try
			{
				uart.WriteLine(s);
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
		List<string> uartline = new List<string>(); //接收到的行，需要调用方清除
		List<byte> uartbuf = new List<byte>(); //串口接收缓冲
		void uart_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			int n = 0;
			byte[] buf =new byte[0];
			try
			{
				n = uart.BytesToRead;
				buf = new byte[n];
				uart.Read(buf, 0, n);
			}
			catch
			{ }
			for(int i=0;i<buf.Length;i++)
			{
				string s = "";
				if (is_plugin) s = Mingw.so_rx(buf[i]); //使用插件
				else //直接文本的形式
				{
					uartbuf.Add(buf[i]);
					if (buf[i] == 0x0a)
					{
						s = Encoding.Default.GetString(uartbuf.ToArray(), 0, uartbuf.Count);
						uartbuf.Clear();
					}
				}
				if (s != "")
				{
					Dispatcher.BeginInvoke((EventHandler)delegate(object sd, EventArgs ea)
					{
						rx_line(s);
					}, invokeobj);
				}
			}
			return;
		}
		void proc_text(string line) //处理一行传感字符
		{
			line = line.Trim();
			if (line == "") return ;
			//首先看看是不是软件指令
			if(ctrl_cmd(line)) return;
			//给传感变量刷新
			commc.update_data(line);
		}
		void fit_screen() //曲线范围
		{
			double x_max = int.MinValue, y_max = int.MinValue;
			double x_min = int.MaxValue, y_min = int.MaxValue;
			foreach (var item in series_map) //遍历所有曲线，找极值
			{
				foreach (var p in item.Value.Points)
				{
					if (p.XValue > x_max) x_max = p.XValue;
					if (p.YValues[0] > y_max) y_max = p.YValues[0];
					if (p.XValue < x_min) x_min = p.XValue;
					if (p.YValues[0] < y_min) y_min = p.YValues[0];
				}
			}
			if ((int)(x_max + 1.5) < (int)(x_min - 1) || (int)(x_max + 1.5)<0)
			{
				return;
			}
			else if (y_max < y_min) return;
			chart1.ChartAreas[0].Axes[0].Maximum = (int)(x_max + 1.5);
			chart1.ChartAreas[0].Axes[0].Minimum = (int)(x_min - 1);
			chart1.ChartAreas[0].Axes[1].Maximum = (int)(y_max + 1.5);
			chart1.ChartAreas[0].Axes[1].Minimum = (int)(y_min - 1);
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

	#region 指令类
	public class CCmd_Button
	{
		static public SolidColorBrush br_normal = new SolidColorBrush(Color.FromRgb(0xdd, 0xdd, 0xdd)); //普通按键颜色
		static public CCmd_Button bt_factory(CmdType t) //工厂方法
		{
			switch(t)
			{
				case CmdType.bt: return new CCmd_Button();
				case CmdType.text: return new CCmd_Text();
				case CmdType.sw: return new CCmd_Switch();
				case CmdType.rpl_bool: return new CCmd_rpl_bool();
			}
			return null;
		}

		public System.Windows.Controls.Grid grid;
		public Button tb=new Button();
		public void add_ctrl(UIElement c, ref int row, ref int col)
		{
			grid.Children.Add(c);
			System.Windows.Controls.Grid.SetColumn(c, col++);
			System.Windows.Controls.Grid.SetRow(c, row);
		}
		virtual public void ini(CmdDes cd, ref int row,ref int col)
		{
			tb.Content = cd.name;
			tb.Tag = cd.name;
			add_ctrl(tb,ref row,ref col);
			tb.Click += new RoutedEventHandler((RoutedEventHandler)delegate (object sender, RoutedEventArgs e)
			{
				try
				{
					MainWindow.mw.send_uart_cmd(MainWindow.mw.commc.cmds[(string)((Button)sender).Tag].cmd);
				}
				catch { }
			});
		}
	}
	public class CCmd_Text : CCmd_Button //带输入文本框的按钮
	{
		TextBox tt1 = new TextBox(); //参数显示
		public override void ini(CmdDes cd,ref int row, ref int col)
		{
			if (col == 1) //若已经是一半了
			{
				grid.RowDefinitions.Add(new RowDefinition());
				row++; col = 0;
			}
			tb.Content = cd.name;
			tb.Tag = cd.name;
			add_ctrl(tb, ref row, ref col);
			tb.Click += new RoutedEventHandler((RoutedEventHandler)delegate (object sender, RoutedEventArgs e)
			{
				try
				{
					MainWindow.mw.send_uart_cmd(MainWindow.mw.commc.cmds[(string)((Button)sender).Tag].cmd + " " + tt1.Text);
				}
				catch { }
			});
			tt1.Text = string.Format("{0:0.00}", cd.dft);
			add_ctrl(tt1, ref row, ref col);
		}
	}
	public class CCmd_Switch : CCmd_Button  //开关控件
	{
		public System.Windows.Controls.Grid subgrid= new System.Windows.Controls.Grid(); //控件容器
		public Label lb_on = new Label();
		public Label lb_off = new Label();
		public Border bd = new Border();
		public override void ini(CmdDes cd, ref int row, ref int col)
		{
			//注册到主面板中
			subgrid.Margin = new Thickness(1, 2, 1, 2);
			add_ctrl(subgrid, ref row, ref col);
			//加入鼠标事件
			tb.AddHandler(Button.MouseDownEvent, new RoutedEventHandler(mouseDown), true);
			lb_on.AddHandler(Button.MouseDownEvent, new RoutedEventHandler(mouseDown), true);
			lb_off.AddHandler(Button.MouseDownEvent, new RoutedEventHandler(mouseDown), true);
			//控件自身的属性
			tb.Content = cd.name;
			tb.Tag = cd.name;

			FrameworkElementFactory f = new FrameworkElementFactory(typeof(Border), "Border");
			f.SetValue(Border.CornerRadiusProperty, new CornerRadius(15));
			f.SetValue(Border.BackgroundProperty, br_normal);
			FrameworkElementFactory f1 = new FrameworkElementFactory(typeof(ContentPresenter), "ContentPresenter");
			Binding bindingc = new Binding("Content");
			bindingc.Source = tb;
			bindingc.Mode = BindingMode.OneWay;
			f1.SetBinding(ContentPresenter.ContentProperty, bindingc);
			Binding bindingb = new Binding("Background");
			bindingb.Source = tb;
			bindingb.Mode = BindingMode.OneWay;
			f.SetBinding(Border.BackgroundProperty, bindingb);
			f1.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
			f1.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			f.AppendChild(f1);
			ControlTemplate ct = new ControlTemplate(typeof(Button));
			
			ct.VisualTree = f;
			tb.Template = ct;
			lb_on.Content = "开";
			lb_off.Content = "关";
			add_to_grid(bd, 0);
			System.Windows.Controls.Grid.SetColumnSpan(bd, 3);
			bd.BorderBrush = Brushes.LightGray;
			bd.Background = new SolidColorBrush(Color.FromRgb(0xbb, 0xbb, 0xbb));
			bd.CornerRadius = new CornerRadius(15);
			bd.BorderThickness = new Thickness(3);
			add_to_grid(lb_on,0);
			add_to_grid(lb_off,2);
			add_to_grid(tb,1);
			lb_off.HorizontalAlignment = HorizontalAlignment.Right;
			sw_action(8);
			//控件的接收配置
			if (MainWindow.mw.commc.dset.ContainsKey(cd.refdname)) //若有反馈值
			{
				var t = MainWindow.mw.commc.dset[cd.refdname];
				judge_out(t, cd.dft);
				//t.update_cb = tn => { t.update_times = 10; };//数据更新回调函数
				t.update_dis += delegate (string tn) //数据更新回调函数
				{
					if (t.update_times > 0)
					{
						//t.update_times--;
						tb.Background = Brushes.LightYellow;
						if (Mouse.LeftButton == MouseButtonState.Pressed) return; //鼠标按下就先不刷
						judge_out(t,t.val);
					}
					else tb.Background = br_normal;
				};
			}
		}
		void mouseDown(object sender, RoutedEventArgs e)
		{
			var p = Mouse.GetPosition(tb);
			if (p.X < tb.ActualWidth / 2) //开
			{
				sw_action(12);
				MainWindow.mw.send_uart_cmd(MainWindow.mw.commc.cmds[(string)tb.Tag].cmd);
			}
			else //关
			{
				sw_action(-12);
				MainWindow.mw.send_uart_cmd(MainWindow.mw.commc.cmds[(string)tb.Tag].cmdoff);
			}
		}
		public void judge_out(DataDes t,string s) //判断当前输出，设置到显示
		{
			if (t.val == t.str_tab[0]) sw_action(-8); //若是关
			else sw_action(8);
		}
		public void sw_action(int a)
		{
			tb.Margin = new Thickness(25-a, 0, 25+a, 0);
		}
		public void add_to_grid(UIElement c, int col)
		{
			subgrid.Children.Add(c);
			System.Windows.Controls.Grid.SetColumn(c, col);
		}
	}
	public class CCmd_rpl_bool : CCmd_Button  //带回复的指令
	{
		public Border bd = new Border();
		public bool result = true; //结果缓存
		public int sent_times = 0; //发送后倒计时，计时结束就不响应了
		public override void ini(CmdDes cd, ref int row, ref int col)
		{
			//注册到主面板中
			add_ctrl(tb, ref row, ref col);
			grid.Children.Add(bd);
			System.Windows.Controls.Grid.SetColumn(bd, col-1);
			System.Windows.Controls.Grid.SetRow(bd, row);
			
			//控件自身的属性
			tb.Content = cd.name;
			tb.Tag = cd.name;
			tb.HorizontalContentAlignment = HorizontalAlignment.Left;
			tb.Click += new RoutedEventHandler((RoutedEventHandler)delegate (object sender, RoutedEventArgs e)
			{
				try
				{
					MainWindow.mw.send_uart_cmd(MainWindow.mw.commc.cmds[(string)((Button)sender).Tag].cmd);
					sent_times = 10;
				}
				catch { }
			});

			bd.BorderBrush = Brushes.LightGreen;
			bd.Background = br_normal;
			bd.CornerRadius = new CornerRadius(10);
			bd.BorderThickness = new Thickness(3);
			bd.Width = 20;
			bd.Height = 20;
			bd.HorizontalAlignment= HorizontalAlignment.Right;
			bd.VerticalAlignment = VerticalAlignment.Center;
			bd.Margin = new Thickness(0,0,10,0);
			//控件的接收配置
			if (MainWindow.mw.commc.dset.ContainsKey(cd.refdname)) //若有反馈值
			{
				var t = MainWindow.mw.commc.dset[cd.refdname];
				judge_out(t, cd.dft);
				//t.update_cb = tn => { t.update_times = 10; };//数据更新回调函数
				t.update_dis += delegate (string tn) //数据更新回调函数
				{
					if (sent_times > 0)
					{
						sent_times--; //发送后的计时
						if (t.update_times > 0) //若有刷新
						{
							//t.update_times--;
							judge_out(t, t.val); //显示核心和外环
						}
						else //若无刷新
						{
							bd.Background = Brushes.Gray;
						}
					}
					else //若已经超时
					{
						bd.Background = Brushes.Gray;
					}
				};
			}
		}
		public void judge_out(DataDes t, string s) //判断当前输出，设置到显示
		{
			if (t.val == t.str_tab[1]) result=true; //若是成功
			else result = false;
			bd.Background = result ? Brushes.Green : Brushes.Red;
			bd.BorderBrush = result ? Brushes.LightGreen : Brushes.LightPink;
		}
	}
	#endregion
}
