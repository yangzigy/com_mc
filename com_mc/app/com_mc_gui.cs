using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Windows.Input;

namespace com_mc
{ //通用测控的界面部分，传感值是曲线和值的显示，通用的控件是控制控件
	public class CCmd_Button //指令基类
	{
		static public SolidColorBrush br_normal = new SolidColorBrush(Color.FromRgb(0xdd, 0xdd, 0xdd)); //普通按键颜色
		static public CCmd_Button bt_factory(CmdType t,CmdDes cd, Grid g) //简单工厂
		{
			switch(t)
			{
				case CmdType.bt: return new CCmd_Button(cd,g);
				case CmdType.text: return new CCmd_Text(cd, g);
				case CmdType.sw: return new CCmd_Switch(cd, g);
				case CmdType.rpl_bool: return new CCmd_rpl_bool(cd, g);
				case CmdType.label: return new CCmd_label(cd, g);
				case CmdType.para: return new CCmd_para(cd, g);
			}
			return null;
		}
		static public int bt_margin_len=1; //按钮的间距
		static public int ctrl_cols=2; //控制按钮的列数
		public static Dictionary<string, DataDes> dset; //引用外部的参数列表
		public static Dictionary<string, CmdDes> cmds; //引用外部的指令列表
		public delegate void STR_CB(string s);
		static public STR_CB send_cmd_str; //数据接收回调
		//对象内容
		public Grid grid;
		public System.Windows.Controls.Primitives.ButtonBase tb=null;
		public CmdDes cmddes; //缓存命令描述
		public Thickness bt_margin; //按钮间距
		public CCmd_Button(CmdDes cd, Grid g)
		{
			cmddes =cd;
			grid = g;
			int m= bt_margin_len;
			bt_margin=new Thickness(m,m,m,m);
		}
		public void add_ctrl(UIElement c, ref int row, ref int col) //向网格中添加控件，要求网格已经有一行了，若放不下自动添加行
		{
			int space_left = ctrl_cols - col;
			if(cmddes.c_span>space_left) //若不够了
			{
				grid.RowDefinitions.Add(new RowDefinition());
				row++; col = 0;
			}
			grid.Children.Add(c);
			Grid.SetColumn(c, col);
			Grid.SetRow(c, row);
			Grid.SetColumnSpan(c, cmddes.c_span);
			col += cmddes.c_span;
		}
		virtual public void ini(ref int row,ref int col) //初始化并添加到父窗体中
		{
			if (cmddes.repeat_T > 0) //若是重复指令
			{
				tb = new System.Windows.Controls.Primitives.ToggleButton();
			}
			else //若是单次触发指令
			{
				tb=new Button();
				tb.Click += new RoutedEventHandler((RoutedEventHandler)delegate (object sender, RoutedEventArgs e)
				{
					send_cmd();
				});
			}
			tb.Content = cmddes.name;
			tb.Tag = cmddes.name;
			tb.Margin = bt_margin;
			add_ctrl(tb, ref row, ref col);
		}
		int repeat_tick = 0; //重复发送计时
		virtual public void poll() //10Hz周期
		{
			if (tb is System.Windows.Controls.Primitives.ToggleButton) //若是重复指令
			{
				var tbt = tb as System.Windows.Controls.Primitives.ToggleButton;
				if(tbt.IsChecked ==true && cmddes.repeat_T!=0)
				{
					repeat_tick++;
					if (repeat_tick>=cmddes.repeat_T)
					{
						send_cmd();
						repeat_tick=0;
					}
				}
			}
		}
		public void send_cmd() //发送指令
		{
			try
			{
				string s = cmddes.cmd; //命令字符
				if (cmds.ContainsKey(cmddes.suffixname)) //若有后缀控件
				{
					s += " " + cmds[cmddes.suffixname].get_stat(); //从指令列表转了一圈，指令列表回调界面这边的函数得到界面状态
				}
				send_cmd_str(s);
			}
			catch { }
		}
	}
	public class CCmd_label : CCmd_Button //字符显示
	{
		public CCmd_label(CmdDes cd, Grid g) : base(cd,g) {}
		public Label lb = new Label();
		public override void ini(ref int row, ref int col)
		{
			lb.Content = cmddes.name;
			lb.Tag = cmddes.name;
			add_ctrl(lb, ref row, ref col);
		}
	}
	public class CCmd_Text : CCmd_Button //文本框
	{
		public CCmd_Text(CmdDes cd, Grid g) : base(cd, g) { }
		TextBox tt1 = new TextBox(); //参数显示
		public override void ini(ref int row, ref int col)
		{
			tt1.Text = cmddes.dft;
			tt1.VerticalContentAlignment = VerticalAlignment.Center;
			tt1.Margin = bt_margin;
			cmddes.get_stat= ()=> tt1.Text;
			add_ctrl(tt1, ref row, ref col);
		}
	}
	public class CCmd_Switch : CCmd_Button  //开关控件
	{
		public CCmd_Switch(CmdDes cd, Grid g) : base(cd, g) { }
		public Grid subgrid = new Grid(); //控件容器
		public Label lb_on = new Label();
		public Label lb_off = new Label();
		public Border bd = new Border();
		public override void ini(ref int row, ref int col)
		{
			//注册到主面板中
			subgrid.Margin = new Thickness(1, 2, 1, 2);
			add_ctrl(subgrid, ref row, ref col);
			tb = new Button();
			//加入鼠标事件
			tb.AddHandler(UIElement.MouseDownEvent, new RoutedEventHandler(mouseDown), true);
			lb_on.AddHandler(UIElement.MouseDownEvent, new RoutedEventHandler(mouseDown), true);
			lb_off.AddHandler(UIElement.MouseDownEvent, new RoutedEventHandler(mouseDown), true);
			//控件自身的属性
			tb.Content = cmddes.name;
			tb.Tag = cmddes.name;

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
			f1.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
			f1.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			f.AppendChild(f1);
			ControlTemplate ct = new ControlTemplate(typeof(Button));
			
			ct.VisualTree = f;
			tb.Template = ct;
			lb_on.Content = "开";
			lb_off.Content = "关";
			add_to_grid(bd, 0);
			Grid.SetColumnSpan(bd, 3);
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
			if (dset.ContainsKey(cmddes.refdname)) //若有反馈值
			{
				var t = dset[cmddes.refdname];
				judge_out(cmddes.dft);
				t.update_dis += delegate (DataDes tn) //数据更新回调函数
				{
					if (tn.update_times > 0)
					{
						//t.update_times--;
						tb.Background = Brushes.LightYellow;
						if (Mouse.LeftButton == MouseButtonState.Pressed) return; //鼠标按下就先不刷
						judge_out(tn.val.ToString());
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
				send_cmd_str(cmddes.cmd);
			}
			else //关
			{
				sw_action(-12);
				send_cmd_str(cmddes.cmdoff);
			}
		}
		public void judge_out(string s) //判断当前输出，设置到显示
		{
			if (s == cmddes.dft || s=="1") sw_action(8);
			else sw_action(-8); //若是关
		}
		public void sw_action(int a)
		{
			double k = bd.ActualWidth / 150.0f;
			if (k < 0.1) k = 1;
			tb.Margin = new Thickness((25-a)*k, 0, (25+a)*k, 0);
		}
		public void add_to_grid(UIElement c, int col)
		{
			subgrid.Children.Add(c);
			Grid.SetColumn(c, col);
		}
	}
	public class CCmd_rpl_bool : CCmd_Button  //带回复的指令
	{ //回复一般是多种指令公用的，所以不做周期了
		public CCmd_rpl_bool(CmdDes cd, Grid g) : base(cd, g) { }
		public Border bd = new Border();
		public bool result = true; //结果缓存
		public int sent_times = 0; //发送后倒计时，计时结束就不响应了
		public override void ini(ref int row, ref int col)
		{
			tb = new Button();
			tb.Click += new RoutedEventHandler((RoutedEventHandler)delegate (object sender, RoutedEventArgs e)
			{
				try
				{
					send_cmd_str(cmds[(string)((Button)sender).Tag].cmd);
					sent_times = 10;
				}
				catch { }
			});
			//注册到主面板中
			add_ctrl(tb, ref row, ref col);
			grid.Children.Add(bd);
			Grid.SetColumn(bd, col-1);
			Grid.SetRow(bd, row);
			
			//控件自身的属性
			tb.Content = cmddes.name;
			tb.Tag = cmddes.name;
			tb.HorizontalContentAlignment = HorizontalAlignment.Left;
			tb.Margin = bt_margin;

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
			if (dset.ContainsKey(cmddes.refdname)) //若有反馈值
			{
				var t = dset[cmddes.refdname];
				judge_out(t, cmddes.dft);
				t.update_dis += delegate (DataDes tn) //数据更新回调函数
				{
					if (sent_times > 0)
					{
						sent_times--; //发送后的计时
						if (t.update_times > 0) //若有刷新
						{
							judge_out(t, tn.val.ToString()); //显示核心和外环
						}
						else //若无刷新
						{
							bd.Background = new SolidColorBrush(Color.FromRgb(0xbb, 0xbb, 0xbb));
						}
					}
					else //若已经超时
					{
						bd.Background = new SolidColorBrush(Color.FromRgb(0xbb, 0xbb, 0xbb));
					}
				};
			}
		}
		public void judge_out(DataDes t, string s) //判断当前输出，设置到显示
		{
			if (t.val.ToString() == t.val.str_tab[1]) result=true; //若是成功
			else result = false;
			bd.Background = result ? Brushes.Green : Brushes.Red;
			bd.BorderBrush = result ? Brushes.LightGreen : Brushes.LightPink;
		}
	}
	public class CCmd_para : CCmd_Button  //参数型
	{
		public CCmd_para(CmdDes cd, Grid g) : base(cd, g) { }
		TextBox tt1 = new TextBox(); //参数显示
		public Image img_refresh = new Image();
		public int sent_times = 0; //发送后倒计时，计时结束就不响应了
		static public BitmapImage i_on; //刷新按钮
		static public BitmapImage i_off; //刷新按钮
		public override void ini(ref int row, ref int col)
		{
			//控件自身的属性
			tt1.Text = cmddes.dft;
			tt1.VerticalContentAlignment = VerticalAlignment.Center;
			var tm = bt_margin;
			tm.Right = 30;
			tt1.Margin = tm;
			cmddes.get_stat = () => tt1.Text;
			img_refresh.Source = i_off;
			img_refresh.Width = 30;
			img_refresh.AddHandler(UIElement.MouseDownEvent, new RoutedEventHandler(mouseDown), true);
			//注册到主面板中
			int cur_row = row;
			add_ctrl(tt1, ref row, ref col);
			col-=cmddes.c_span; //即使是最后一行，也是下一个控件换行
			add_ctrl(img_refresh, ref cur_row, ref col); //刷新图标不会引起换行
			img_refresh.VerticalAlignment = VerticalAlignment.Center;
			img_refresh.HorizontalAlignment = HorizontalAlignment.Right;

			//控件的接收配置
			if (dset.ContainsKey(cmddes.refdname)) //若有反馈值
			{
				var t = dset[cmddes.refdname];
				t.update_dis += delegate (DataDes tn) //数据更新回调函数
				{
					if (tn.update_times > 0) //若有刷新
					{
						img_refresh.Source = i_on;
						tt1.Text = tn.val.ToString();
					}
					else img_refresh.Source = i_off; //若无刷新
				};
			}
		}
		void mouseDown(object sender, RoutedEventArgs e)
		{
			try
			{
				send_cmd_str(cmddes.cmd);
				sent_times = 10;
			}
			catch { }
		}
	}
}
