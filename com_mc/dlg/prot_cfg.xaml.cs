using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
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
		public MC_Prot para_prot = new MC_Prot(); //变量和协议的整体 
		public MC_Prot cur_prot = null; //当前使用的协议
		//将协议刷新到界面上
		List<ParaValue_Display> para_disobj = new List<ParaValue_Display>();
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
		public void load_prot_from_json(Dictionary<string, object> v) //从json加载协议
		{
			para_prot.fromJson(v); //将json转换为协议实体
			//首先刷新参数字典
			foreach (var item in para_prot.para_dict) 
			{
				ParaValue_Display tl = new ParaValue_Display();
				tl.name=item.Value.name;
				tl.type = item.Value.type.ToString();
				tl.len = item.Value.len.ToString();
				para_disobj.Add(tl);
				//dg_vir.Items.Add(tl); //这样加双击时报无法修改错误
			}
			dg_vir.ItemsSource=para_disobj; //刷新到界面上
			//刷新文本协议列表
			//List<ParaValue_Display> text_dis = new List<ParaValue_Display>();
			//foreach (var item in para_prot.textline_dict)
			//{
			//	ParaValue_Display tl = new ParaValue_Display();
			//	tl.name = item.Value.name;
			//	tl.len = item.Value.prot_list.Count.ToString();
			//	text_dis.Add(tl);
			//}
			//dg_text_prot.ItemsSource = text_dis;
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
		private void dg_vir_MouseDown(object sender, MouseButtonEventArgs e) //参数列表的鼠标按下
		{
			try //若上次显示的属性不是参数
			{
				var t = pg_prot.SelectedObject as ParaValue_PropDis;
				if (t == null) throw new Exception();
			}
			catch (Exception ee)
			{
				//刷新属性显示
				dg_vir_SelectedCellsChanged(null, null);
			}
		}
		private void dg_vir_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
		{
			if (dg_vir.SelectedIndex >= 0)
			{
				string name = para_disobj[dg_vir.SelectedIndex].name; //取得所选的名称
				var obj = para_prot.para_dict[name]; //取得所选的对象
				var disobj = ParaValue_PropDis.get_disobj(obj);
				pg_prot.SelectedObject = disobj;
			}
		}
	}
	#region 显示结构定义
	public class ParaValue_Display //参数值的列表显示结构，也可作为其他列表的显示结构
	{
		public string name { get; set; }
		public string type { get; set; }
		public string len { get; set; }
	}
	public class ParaValue_PropDis //在属性修改显示时使用的结构
	{
		[CategoryAttribute("常规"), DescriptionAttribute("名字")]
		public string name { get; set; } = "";  //参数的唯一id，在C#程序中使用
		[CategoryAttribute("常规"), DescriptionAttribute("类型")]
		public DataType type { get; set; } = DataType.df; //参数类型,默认是double(在factory处设置)
		[CategoryAttribute("常规"), DescriptionAttribute("长度")]
		public int len { get; set; } = 0; //数据长度
		[CategoryAttribute("常规"), DescriptionAttribute("显示字符表")]
		public List<string> 显示字符表 { get; set; } = new List<string>();//显示字符串表。可用于bool型指令，0为失败字符，1为成果字符
		public ParaValue_PropDis(ParaValue v)
		{
			name = v.name; type=v.type; len = v.len;
			显示字符表.Clear();
			显示字符表.AddRange(v.str_tab);
		}
		public virtual void to_obj(ParaValue v) //从显示变量转换到之前的内存对象中
		{
			v.name = name; v.type = type; v.len = len;
			v.str_tab.Clear();
			v.str_tab.AddRange(显示字符表);
		}
		static public ParaValue_PropDis get_disobj(ParaValue v)
		{
			ParaValue_PropDis r=null;
			if(v.GetType()== typeof(ParaValue_Str))
			{
				return new ParaValue_PropDis(v);
			}
			return new ParaValue_Val_PropDis(v as ParaValue_Val);
		}
	}
	public class ParaValue_Val_PropDis : ParaValue_PropDis //在属性修改显示时使用的结构
	{
		[CategoryAttribute("常规"), DescriptionAttribute("保留小数位数")]
		public int point_n { get; set; } = 0; //
		public ParaValue_Val_PropDis(ParaValue_Val v) : base(v)
		{
			point_n = v.point_n;
		}
		public override void to_obj(ParaValue v)
		{
			base.to_obj(v);
			var vt = v as ParaValue_Val;
			vt.point_n= point_n;
		}
	}
	public class ProtDom_PropDis //ProtDom对象的显示扩充
	{
		[CategoryAttribute("常规"), DescriptionAttribute("名字")]
		public string name { get; set; } = ""; //协议域名称(唯一，或没有)
		[CategoryAttribute("常规"), DescriptionAttribute("类型")]
		public DataType type { get; set; } = 0; //数据类型,默认是u8（此处无效，在factory处设置为u64）
		[CategoryAttribute("常规"), DescriptionAttribute("引用参数名")]
		public string ref_name { get; set; } = "";//引用参数的名称
	}
	public class PD_Node_PropDis : ProtDom_PropDis //协议域叶子节点
	{
		[CategoryAttribute("常规"), DescriptionAttribute("长度")]
		public int len { get; set; } = 0; //缓存本域的数据长度
		[CategoryAttribute("常规"), DescriptionAttribute("额外向后跳的字节数（文本行协议是列数）")]
		public int skip_n { get; set; } = 0; //处理完此列后，额外向后跳的字节数（文本行协议是列数）。例如文本行的一列要处理出多个参数，此处可填0。或者需要跳过一列，此处可填1
		[CategoryAttribute("常规"), DescriptionAttribute("处理变换kx+b")]
		public double pro_k { get; set; } = 1; //处理变换kx+b
		[CategoryAttribute("常规"), DescriptionAttribute("处理变换kx+b")]
		public double pro_b { get; set; } = 0;//处理变换kx+b
		[CategoryAttribute("常规"), DescriptionAttribute("bit处理中的起始bit")]
		public int bit_st { get; set; } = 0; //起始bit
		[CategoryAttribute("常规"), DescriptionAttribute("bit处理中的bit长度")]
		public int bit_len { get; set; } = 0; //bit长度，用此配置表示此域为按bit处理
		[CategoryAttribute("常规"), DescriptionAttribute("bit处理中是否有符号位")]
		public bool bit_singed { get; set; } = false; //是否是有符号数
	}
	public class PD_Array_PropDis : ProtDom_PropDis //数组型叶子节点，包括未定义和字符串，输出类型只能是未定义或字符串
	{
		[CategoryAttribute("常规"), DescriptionAttribute("长度")]
		public int len { get; set; } = 0; //缓存本域的数据长度
	}
	public class PD_Str_PropDis : PD_Node_PropDis //字符型叶子节点，分为十进制和hex，输出类型只能是值类型
	{
		[CategoryAttribute("常规"), DescriptionAttribute("协议读入方式")]
		public int str_type { get; set; } = 0; //hex或空
	}
	public class PD_Obj_PropDis : ProtDom_PropDis //协议对象
	{
		[CategoryAttribute("常规"), DescriptionAttribute("协议列表")]
		public List<string> 协议列表 { get; set; }
	}
	public class PD_Switch_PropDis : PD_Obj_PropDis //选择协议域方式
	{
		[CategoryAttribute("常规"), DescriptionAttribute("索引值类型")]
		public int 索引值类型 { get; set; } = 0; //索引值类型hex或空
		[CategoryAttribute("常规"), DescriptionAttribute("引用的协议域")]
		public string ref_prot { get; set; } = "";//引用的协议域名称
	}
	public class PD_Loop_PropDis : PD_Obj_PropDis //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		[CategoryAttribute("常规"), DescriptionAttribute("用于确定重复次数的引用协议域")]
		public string ref_len { get; set; } = ""; //用于确定重复次数的引用协议域
		[CategoryAttribute("常规"), DescriptionAttribute("直接指定重复次数")]
		public int loop_n { get; set; } = 0; //直接指定重复次数
	}
	#endregion
}
