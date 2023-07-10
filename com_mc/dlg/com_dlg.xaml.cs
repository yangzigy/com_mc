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
using System.Windows.Shapes;

namespace com_mc
{
	/// <summary>
	/// com_dlg.xaml 的交互逻辑
	/// </summary>
	public partial class Com_Dlg : Window
	{
		public Grid mgrid=null;
		public bool rst=false; //需要重新定义返回结果
		public Com_Dlg()
		{
			InitializeComponent();
			mgrid = grid_main;
		}
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = true;
			Visibility = Visibility.Hidden;
		}
		private void bt_ok_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true; //重写Closing以后，这个失效
			rst=true;
			Close();
		}
		private void bt_cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			rst = false;
			Close();
		}
	}
}
