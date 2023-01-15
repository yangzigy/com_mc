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
	/// Prot_Cfg_Window.xaml 的交互逻辑
	/// </summary>
	public partial class Prot_Cfg_Window : Window
	{
		public MC_Prot para_prot=new MC_Prot(); //变量和协议的整体 
		public MC_Prot cur_prot=null; //当前使用的协议
		public Prot_Cfg_Window()
		{
			InitializeComponent();
		}
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
		}
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = true;
			Visibility = Visibility.Hidden;
		}
		public void load_prot_from_json(Dictionary<string, object> v) //从文件加载协议
		{

		}
		private void mi_open_Click(object sender, RoutedEventArgs e) //打开协议处理
		{
			FrameworkElement fe = sender as FrameworkElement;
			switch (fe.Tag)
			{
				case "cur": //加载当前协议
					{
						var v = cur_prot.toJson(); //需要添加DataDes的额外配置
						load_prot_from_json(v);
					}
					break;
				case "file": //从文件加载协议
					{
						//object t = Tool.load_json_from_file<Dictionary<string, object>>(s);
						//load_prot_from_json();
					}
					break;
			}
		}
		private void mi_save_Click(object sender, RoutedEventArgs e) //存储协议配置
		{

		}
	}
}
