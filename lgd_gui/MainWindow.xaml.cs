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
			string[] commPort = SerialPort.GetPortNames();
			List<string> dsrclist = new List<string>(commPort);
			comPort.ItemsSource = dsrclist;
			comPort.SelectedIndex = 0;
			uart.BaudRate = config.uart_b;
			uart.DataReceived += new SerialDataReceivedEventHandler(uart_DataReceived);
		}
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			//初始化界面
			state_dis_ini();
			timer10Hz = new Timer((TimerCallback)delegate (object state)
			{
				Dispatcher.BeginInvoke((EventHandler)delegate (object sd, EventArgs ea)
				{
					tick++;
					if (tick % 5 == 0) //2Hz
					{
						fit_screen();
					}
					foreach (var item in commc.dset) //刷新每个参数
					{
						if (item.Value.update_times > 0)
						{
							item.Value.update_times--;
							checkb_map[item.Value.name].Background = Brushes.LightGreen;
							checkb_map[item.Value.name].Content = item.Value.name + ":" + item.Value.val;
						}
						else
						{
							checkb_map[item.Value.name].Background = Brushes.LightCoral;
						}
					}
				}, invokeobj);
			}, this, 0, 100);
		}
		#region click
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
			foreach (var seri in series_map)
			{
				seri.Value.Points.Clear();
			}
			st_ms = DateTime.Now.Ticks / 10000;
		}
		#endregion
	}
}
