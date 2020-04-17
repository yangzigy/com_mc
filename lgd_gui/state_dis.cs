using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
		public Com_MC commc = new Com_MC(); //通用测控对象
		TextDataFile rec_file = new TextDataFile();
		object[] invokeobj=new object[2];
		Dictionary<string,System.Windows.Forms.DataVisualization.Charting.Series> series_map=new Dictionary<string,System.Windows.Forms.DataVisualization.Charting.Series>();
		Dictionary<string, CheckBox> checkb_map = new Dictionary<string, CheckBox>();
		long st_ms= DateTime.Now.Ticks/10000; //曲线起始ms
		int is_first=1;
		bool is_plugin = true; //是否有插件？
		public DispatcherTimer dispatcherTimer = null;

		void state_dis_ini()
		{
#region 传感参数部分
			chart1 = mainFGrid.Child as System.Windows.Forms.DataVisualization.Charting.Chart;
			chart1.Legends[0].DockedToChartArea ="ChartArea1";
			chart1.Legends[0].BackColor=System.Drawing.Color.Transparent;
			//从配置中加载参数
			chart1.Series.Clear();
			foreach (var item in config.dset)
			{
				commc.dset[item.name] = item;
				var tmpserial = new System.Windows.Forms.DataVisualization.Charting.Series()
				{
					BorderWidth = 2,
					ChartArea = "ChartArea1",
					ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line,
					//Color = System.Drawing.Color.Red,
					Legend = "Legend1",
					Name = item.name,
				};
				CheckBox cb = new CheckBox();
				cb.Content = item.name;
				cb.IsChecked = item.dis_curve;
				cb.Width = 150;
				cb.Background = Brushes.LightCoral;
				if(item.type==DataType.t_str) //字符型的，不让选择曲线
				{
					//cb.IsEnabled = false;
				}
				else //曲线型的
				{
					chart1.Series.Add(tmpserial);
					series_map[item.name] = tmpserial;
				}
				checkb_map[item.name] = cb;
				sp_measure.Children.Add(cb);
				item.update_cb=(delegate(string tn) //数据更新回调函数
				{
					try
					{
						var it = commc.dset[tn];
						it.update_times = 10;
						if (it.type == DataType.t_val && (bool)cb.IsChecked) //若显示曲线
						{
							if (is_first == 1) //首次加入数据点，清除初始化点
							{
								clear_Click(null, null);
								is_first = 0;
							}
							long ticks0 = DateTime.Now.Ticks / 10000;
							if (tmpserial.Points.Count >= config.dis_data_len)
							{
								tmpserial.Points.RemoveAt(0);
							}
							double d = double.Parse(it.val);
							tmpserial.Points.AddXY(ticks0 - st_ms, d);
						}
					}
					catch { }
				});
			}
			//chart1.ChartAreas[0].AxisX.LineColor = System.Drawing.Color.White;
			//chart1.ChartAreas[0].AxisX.LabelStyle.ForeColor = System.Drawing.Color.White;
			//chart1.ChartAreas[0].AxisX.InterlacedColor = System.Drawing.Color.White;
			//chart1.ChartAreas[0].AxisX.TitleForeColor = System.Drawing.Color.White;
			//chart1.ChartAreas[0].AxisX.MajorGrid.LineColor=System.Drawing.Color.White;
			//chart1.ChartAreas[0].AxisX.MajorTickMark.LineColor=System.Drawing.Color.White;
			chart1.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.DashDot;

			chart1.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.DashDot;
			chart1.ChartAreas[0].AxisY.MajorTickMark.LineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.DashDot;

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
				Button tb = new Button(); //设置按钮
				tb.Content = item.name;
				tb.Tag = item.name;
				int col_add=1; //列的增量
				if(item.type==CmdType.bt) //若是单个按键
				{
					tb.Click += new RoutedEventHandler((RoutedEventHandler)delegate(object sender, RoutedEventArgs e)
					{
						try
						{
							send_uart_cmd(commc.cmds[(string)((Button)sender).Tag].cmd);
						}
						catch { }
					});
				}
				else if(item.type==CmdType.text) //若是按键+文本
				{
					if(j==1) //若已经是一半了
					{
						para_grid.RowDefinitions.Add(new RowDefinition());
						i++; j=0;
					}
					TextBox tt1 = new TextBox(); //参数显示
					tt1.Text = string.Format("{0:0.00}", item.dft);
					para_grid.Children.Add(tt1);
					System.Windows.Controls.Grid.SetColumn(tt1, 1);
					System.Windows.Controls.Grid.SetRow(tt1, i);
					tb.Click += new RoutedEventHandler((RoutedEventHandler)delegate(object sender, RoutedEventArgs e)
					{
						try
						{
							send_uart_cmd(commc.cmds[(string)((Button)sender).Tag].cmd + " " + tt1.Text);
						}
						catch { }
					});
					col_add=2;
				}
				para_grid.Children.Add(tb);
				System.Windows.Controls.Grid.SetColumn(tb, j);
				System.Windows.Controls.Grid.SetRow(tb, i);
				j+=col_add;
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
			{
			}
			return 0;
		}
		void send_uart_cmd(string s) //向设备发送文本指令
		{
			if (is_plugin) Mingw.so_cmd(s);
			else send_uart_data(s);
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
			{
			}
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
		void proc_text(string line) //处理一行字符
		{
			line = line.Trim();
			if (line == "") return ;
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
	}
	public class ButtonBrush
	{
		public static readonly DependencyProperty ButtonPressBackgroundProperty = DependencyProperty.RegisterAttached(
			"ButtonPressBackground", typeof(Brush), typeof(ButtonBrush), new PropertyMetadata(default(Brush)));

		public static void SetButtonPressBackground(DependencyObject element, Brush value)
		{
			element.SetValue(ButtonPressBackgroundProperty, value);
		}
		public static Brush GetButtonPressBackground(DependencyObject element)
		{
			return (Brush)element.GetValue(ButtonPressBackgroundProperty);
		}
	}
}
