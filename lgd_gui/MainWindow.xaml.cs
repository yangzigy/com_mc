using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms.DataVisualization;
using System.Windows.Forms.Integration;
using System.Threading;

namespace lgd_gui
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window
	{
		public Config config = new Config();     // 存放系统设置
		public string configfilename = AppDomain.CurrentDomain.BaseDirectory + "/config.txt";
		System.Windows.Forms.DataVisualization.Charting.Chart chart1;
		public SerialPort uart = new SerialPort();
		public Timer timer10Hz;
		public uint tick = 0;

		public MainWindow()
		{
			//首先保存错误
			var domain = AppDomain.CurrentDomain;
			domain.UnhandledException += (sender, targs) =>
			{
				Console.WriteLine(config.ToString());
				Console.WriteLine(chart1.ToString());
				var ex = targs.ExceptionObject as Exception;
				if (ex != null)
				{
					MessageBox.Show("message: " + ex.Message + " trace: " + ex.StackTrace);
					//log("message: " + ex.Message + " trace: " + ex.StackTrace);
				}
			};
			InitializeComponent();
			if (File.Exists(configfilename)) //加载配置文件
			{
				config = Config.load(configfilename);
			}
			// 获取COM口列表
			bt_refresh_uart_Click(null, null);
			uart.BaudRate = config.uart_b;
			uart.DataReceived += new SerialDataReceivedEventHandler(uart_DataReceived);
		}
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (config.mv_w != 0) Width = config.mv_w;
			if (config.mv_h != 0) Height = config.mv_h;
			//初始化界面
			state_dis_ini();
			timer10Hz = new Timer((TimerCallback)delegate (object state)
			{
				Dispatcher.BeginInvoke((EventHandler)delegate (object sd, EventArgs ea)
				{
					tick++;
					if (tick % 5 == 0) //2Hz
					{
						if (cb_fit_screen.IsChecked == true) fit_screen();
						else fit_screen_data();
					}
					foreach (var item in commc.dset) //刷新每个参数
					{
						item.Value.update_dis(item.Value.name);
					}
				}, invokeobj);
			}, this, 0, 100);
		}
		#region click
		private void bt_save_curve_data_Click(object sender, RoutedEventArgs e) //保存曲线数据
		{

		}
		private void bt_refresh_uart_Click(object sender, RoutedEventArgs e)
		{
			string[] commPort = SerialPort.GetPortNames();
			List<string> dsrclist = new List<string>(commPort);
			comPort.ItemsSource = dsrclist;
			comPort.SelectedIndex = 0;
		}
		private void btnConnCom_Click(object sender, RoutedEventArgs e)
		{
			var btn = sender as Button;
			if (btn.Content.ToString() == "打开串口")
			{
				try
				{
					uart.PortName = comPort.Text;
					uart.Open();
					btn.Content = "关闭串口";
				}
				catch
				{
				}
			}
			else
			{
				btn.Content = "打开串口";
				uart.Close();
			}
		}
		private void clear_Click(object sender, RoutedEventArgs e)
		{
			clear_data();
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
		#endregion
		Point pre_m = new Point(0, 0);//鼠标上次拖动的像素位置
		Point pre_left = new Point(-1, 0);//鼠标上次左键按下的位置
		void set_chart1_range(Int64 x0,Int64 x1,Int64 y0,Int64 y1) //输入10倍的坐标
		{
			if (x1 <= x0) x1 = x0 + 1;
			if (y1 <= y0) y1 = y0 + 1;
			if (x1 > int.MaxValue) x1 = int.MaxValue;
			if (x0 < int.MinValue) x0 = int.MinValue;
			if (y1 > int.MaxValue) y1 = int.MaxValue;
			if (y0 < int.MinValue) y0 = int.MinValue;
			//Console.WriteLine("{0},{1},{2},{3}",x0,x1,y0,y1);
			chart1.ChartAreas[0].Axes[0].Minimum = x0 / 10.0;
			chart1.ChartAreas[0].Axes[0].Maximum = x1 / 10.0;
			chart1.ChartAreas[0].Axes[1].Minimum = y0 / 10.0;
			chart1.ChartAreas[0].Axes[1].Maximum = y1 / 10.0;
		}
		private void Chart_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e) //曲线控件的鼠标移动
		{
			double x = e.X;
			double y = e.Y;
			if (x < 0 || x >= chart1.Width || y < 0 || y >= chart1.Height) return;
			double rx = chart1.ChartAreas[0].AxisX.PixelPositionToValue(x);
			double ry = chart1.ChartAreas[0].AxisY.PixelPositionToValue(y);
			if (e.Button == System.Windows.Forms.MouseButtons.Right) //右键
			{
				double rx0 = chart1.ChartAreas[0].AxisX.PixelPositionToValue(pre_m.X);
				double ry0 = chart1.ChartAreas[0].AxisY.PixelPositionToValue(pre_m.Y);
				double rdx = rx - rx0;
				double rdy = ry - ry0; //物坐标的增量
				Int64 x0 = (Int64)((chart1.ChartAreas[0].Axes[0].Minimum - rdx)*10);
				Int64 x1 = (Int64)((chart1.ChartAreas[0].Axes[0].Maximum - rdx)*10);
				Int64 y0 = (Int64)((chart1.ChartAreas[0].Axes[1].Minimum - rdy)*10);
				Int64 y1 = (Int64)((chart1.ChartAreas[0].Axes[1].Maximum - rdy)*10);
				set_chart1_range(x0, x1, y0, y1);
			}
			else if(e.Button==System.Windows.Forms.MouseButtons.Left) //左键
			{

			}
			pre_m.X = x;
			pre_m.Y = y;
		}
		private void Chart_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			int d = e.Delta;
			if (d != 0) //若有鼠标滚轮
			{
				float t = d / 1000.0f; //比例
				if (t < -0.5f) t = -0.5f;
				else if (t > 0.5f) t = 0.5f; //t为正，缩小范围，相当于放大
				double rx_max = chart1.ChartAreas[0].Axes[0].Maximum;
				double rx_min = chart1.ChartAreas[0].Axes[0].Minimum;
				double ry_max = chart1.ChartAreas[0].Axes[1].Maximum;
				double ry_min = chart1.ChartAreas[0].Axes[1].Minimum;
				double lx = rx_max - rx_min;
				double ly = ry_max - ry_min; //长度
				if(lx>2*(curv_x_max-curv_x_min) && t<0 &&
					ly>2*(curv_y_max-curv_y_min) && t<0) return ;
				if(lx<1 && t>0) return ;
				if(ly<1 && t>0) return ;
				t = 1 - t;
				double rdx = (lx * t - lx) / 2;
				double rdy = (ly * t - ly) / 2;
				Int64 x1 = (Int64)((rx_max + rdx)*10);
				Int64 x0 = (Int64)((rx_min - rdx) * 10);
				Int64 y1 = (Int64)((ry_max + rdy) * 10);
				Int64 y0 = (Int64)((ry_min - rdy) * 10);
				set_chart1_range(x0, x1, y0, y1);
			}
		}
		private void Chart_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if(e.Button==System.Windows.Forms.MouseButtons.Left)
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
				//左键按下
				pre_left = new Point(e.X, e.Y);
			}
		}
		private void Chart_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if(pre_left.X>=0)
			{
				double rx = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.X);
				double ry = chart1.ChartAreas[0].AxisY.PixelPositionToValue(e.Y);
				double rx0 = chart1.ChartAreas[0].AxisX.PixelPositionToValue(pre_left.X);
				double ry0 = chart1.ChartAreas[0].AxisY.PixelPositionToValue(pre_left.Y);
				Int64 x0 = (Int64)(Math.Min(rx, rx0) * 10);
				Int64 x1 = (Int64)(Math.Max(rx,rx0) * 10);
				Int64 y0 = (Int64)(Math.Min(ry, ry0) * 10);
				Int64 y1 = (Int64)(Math.Max(ry, ry0) * 10);
				set_chart1_range(x0, x1, y0, y1);
				pre_left.X=-1; //变为无效
			}
		}
	}
}
