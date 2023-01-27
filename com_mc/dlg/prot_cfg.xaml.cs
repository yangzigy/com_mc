﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
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
		List<PEdit_Display> para_disobj = new List<PEdit_Display>(); //测量量的列表
		List<PEdit_Display> prot_disobj = new List<PEdit_Display>(); //协议域的列表
		List<PEdit_Display> prot_treeobj = new List<PEdit_Display>(); //协议域的树形结构
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
				PEdit_Display tl = new PEdit_Display();
				tl.name=item.Value.name;
				tl.type = item.Value.type.ToString();
				tl.len = item.Value.len.ToString();
				para_disobj.Add(tl);
				//dg_vir.Items.Add(tl); //这样加双击时报无法修改错误
			}
			dg_vir.ItemsSource=para_disobj; //刷新到界面上
			//刷新协议域
			prot_treeobj.Clear();
			foreach (var item in para_prot.prot_dict)
			{
				PEdit_Display tl = new PEdit_Display();
				if (item.Key.IndexOf(".") >= 0) continue; //若有点，说明是局部变量，不显示
				//为此节点加子节点
				tl.add_ProtDom(item.Value);
				prot_treeobj.Add(tl);
			}
			tv_prot.ItemsSource = prot_treeobj;
			//刷新协议根节点列表
			List<PEdit_Display> roots_dis = new List<PEdit_Display>();
			foreach (var item in para_prot.prot_root_obj_list)
			{
				PEdit_Display tl = new PEdit_Display();
				tl.name = item.name;
				tl.type = item.type.ToString();
				roots_dis.Add(tl);
			}
			dg_roots_list.ItemsSource = roots_dis;
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
			var ofd = new System.Windows.Forms.SaveFileDialog();
			ofd.Filter = "*.txt|*.txt";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
			string exs = System.IO.Path.GetExtension(ofd.FileName).Trim();
			StreamWriter sw = new StreamWriter(ofd.FileName);
			var js = para_prot.toJson();
			string s = Tool.json_ser.Serialize(js);
			sw.Write(s);
			sw.Close();
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
		private void tv_prot_MouseDown(object sender, MouseButtonEventArgs e) //协议树鼠标按下
		{
			try //若上次显示的属性不是协议
			{
				var t = pg_prot.SelectedObject as ProtDom_PropDis;
				if (t == null) throw new Exception();
			}
			catch (Exception ee)
			{
				//刷新属性显示
				tv_prot_SelectedItemChanged(null, null);
			}
		}
		private void tv_prot_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (tv_prot.SelectedItem !=null)
			{
				var ped = tv_prot.SelectedItem as PEdit_Display;
				string name = ped.name; //取得所选的名称
				var obj = para_prot.prot_dict[name]; //取得所选的对象
				var disobj = ProtDom_PropDis.get_disobj(obj);
				pg_prot.SelectedObject = disobj;
			}
		}
		private void bt_update_prop_Click(object sender, RoutedEventArgs e) //更新属性点击
		{
			var t = pg_prot.SelectedObject;
			var pe=t as Prop_Edit;
			pe.display_2_var(); //让每个显示对象更新对应的后台变量
		}
	}
#region 显示结构定义
	public class PEdit_Display //参数值的列表显示结构，也可作为其他列表的显示结构
	{
		public string name { get; set; }
		public string type { get; set; }
		public string len { get; set; }
		public List<PEdit_Display> sub { get; set; }=new List<PEdit_Display>(); //为了树形控件
		public void add_ProtDom(ProtDom v) //递归加载协议域
		{
			name = v.name; type = v.type.ToString();
			var p = v as PD_Obj; 
			if(p!=null) //若是Obj，具有子协议域
			{
				foreach (var item in p.prot_list) //文本的不给prot_ref_list赋值，所以要通过名称索引
				{
					PEdit_Display tp= new PEdit_Display();
					tp.add_ProtDom(p.p_mcp.prot_dict[item]);
					sub.Add(tp);
				}
			}
		}
	}
	public interface Prop_Edit //属性编辑的接口
	{
		void display_2_var(); //从显示对象更新到后台变量
	}
	public class ParaValue_PropDis : Prop_Edit //在属性修改显示时使用的结构
	{
		[CategoryAttribute("常规"), DescriptionAttribute("名字")]
		public string name { get; set; } = "";  //参数的唯一id，在C#程序中使用
		[CategoryAttribute("常规"), DescriptionAttribute("类型")]
		public DataType type { get; set; } = DataType.df; //参数类型,默认是double(在factory处设置)
		[CategoryAttribute("常规"), DescriptionAttribute("长度")]
		public int len { get; set; } = 0; //数据长度
		[CategoryAttribute("常规"), DescriptionAttribute("显示字符表")]
		public List<string> 显示字符表 { get; set; } = new List<string>();//显示字符串表。可用于bool型指令，0为失败字符，1为成果字符
		
		public ParaValue backend_var; //关联的后台变量
		public ParaValue_PropDis(ParaValue v)
		{
			backend_var = v;
			name = v.name; type=v.type; len = v.len;
			显示字符表.Clear();
			显示字符表.AddRange(v.str_tab);
		}
		public virtual void display_2_var() //从显示对象更新到后台变量
		{
			backend_var.name = name; backend_var.type=type; backend_var.len = len;
			backend_var.str_tab.Clear();
			backend_var.str_tab.AddRange(显示字符表);
		}
		static public ParaValue_PropDis get_disobj(ParaValue v) //从参数类构造参数的显示对象
		{
			ParaValue_PropDis r=null;
			if(v.GetType()== typeof(ParaValue_Str)) //如果是字符型参数
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
		public override void display_2_var()
		{
			base.display_2_var();
			var vt = backend_var as ParaValue_Val;
			vt.point_n= point_n;
		}
	}
	public class ProtDom_PropDis : Prop_Edit //ProtDom对象的显示扩充
	{
		[CategoryAttribute("常规"), DescriptionAttribute("名字")]
		public string name { get; set; } = ""; //协议域名称(唯一，或没有)
		[CategoryAttribute("常规"), DescriptionAttribute("类型")]
		public ProtType type { get; set; } = 0; //数据类型,默认是u8（此处无效，在factory处设置为u64）
		[CategoryAttribute("常规"), DescriptionAttribute("引用参数名")]
		public string ref_name { get; set; } = "";//引用参数的名称

		public ProtDom backend_var; //关联的后台变量
		public ProtDom_PropDis(ProtDom v)
		{
			backend_var = v;
			name = v.name; type = v.type; ref_name = v.ref_name;
		}
		public virtual void display_2_var() //从显示对象更新到后台变量
		{
			backend_var.name= name; backend_var.type= type; backend_var.ref_name = ref_name;
		}
		static public ProtDom_PropDis get_disobj(ProtDom v) //从协议域构造协议的显示对象
		{
			switch (v.type)
			{
				case ProtType.undef: return new PD_Array_PropDis(v as PD_Array);
				case ProtType.str: return new PD_Str_PropDis(v as PD_Str);
				case ProtType.u8:
				case ProtType.u16:
				case ProtType.u32:
				case ProtType.u64:
				case ProtType.s8:
				case ProtType.s16:
				case ProtType.s32:
				case ProtType.s64:
				case ProtType.f:
				case ProtType.df: return new PD_Node_PropDis(v as PD_Node);
				case ProtType.obj: return new PD_Obj_PropDis(v as PD_Obj);
				case ProtType.sw: return new PD_Switch_PropDis(v as PD_Switch);
				case ProtType.loop: return new PD_Loop_PropDis(v as PD_Loop);
				case ProtType.text: return new PD_LineSwitch_PropDis(v as PD_LineSwitch);
				case ProtType.tline: return new PD_LineObj_PropDis(v as PD_LineObj);
				default: throw new Exception("type err");
			}
		}
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
		public PD_Node_PropDis(PD_Node v) : base(v)
		{
			len = v.len; skip_n = v.skip_n; pro_k = v.pro_k; pro_b = v.pro_b; bit_st = v.bit_st; bit_len=v.bit_len; 
			bit_singed = v.bit_singed!=0;
		}
		public override void display_2_var()
		{
			base.display_2_var();
			var bv = backend_var as PD_Node;
			bv.len = len; bv.skip_n=skip_n; bv.pro_k = pro_k; bv.pro_b = pro_b; bv.bit_st = bit_st; bv.bit_len = bit_len;
			bv.bit_singed = bit_singed ? 1 : 0;
		}
	}
	public class PD_Array_PropDis : ProtDom_PropDis //数组型叶子节点，包括未定义和字符串，输出类型只能是未定义或字符串
	{
		[CategoryAttribute("常规"), DescriptionAttribute("长度")]
		public int len { get; set; } = 0; //缓存本域的数据长度
		public PD_Array_PropDis(PD_Array v) : base(v)
		{
			len = v.len;
		}
		public override void display_2_var()
		{
			base.display_2_var();
			var bv = backend_var as PD_Array;
			bv.len = len;
		}
	}
	public class PD_Str_PropDis : PD_Node_PropDis //字符型叶子节点，分为十进制和hex，输出类型只能是值类型
	{
		[CategoryAttribute("常规"), DescriptionAttribute("协议读入方式")]
		public bool is_hex { get; set; } = false; //hex或空
		public PD_Str_PropDis(PD_Str v) : base(v)
		{
			is_hex = v.str_type=="hex";
		}
		public override void display_2_var()
		{
			base.display_2_var();
			var bv = backend_var as PD_Str;
			bv.str_type = is_hex?"hex":"";
		}
	}
	public class PD_Obj_PropDis : ProtDom_PropDis //协议对象
	{
		[CategoryAttribute("常规"), DescriptionAttribute("协议列表")]
		public List<string> 协议列表 { get; set; }= new List<string>();
		public PD_Obj_PropDis(PD_Obj v) : base(v)
		{
			协议列表.AddRange(v.prot_list);
		}
		public override void display_2_var()
		{
			base.display_2_var();
			var bv = backend_var as PD_Obj;
			bv.prot_list.Clear();
			bv.prot_list.AddRange(协议列表);
		}
	}
	public class PD_Switch_PropDis : ProtDom_PropDis //选择协议域方式
	{
		[CategoryAttribute("常规"), DescriptionAttribute("索引值类型")]
		public bool is_hex { get; set; } = false; //索引值类型hex或空
		[CategoryAttribute("常规"), DescriptionAttribute("引用的协议域")]
		public string ref_prot { get; set; } = "";//引用的协议域名称
		[CategoryAttribute("常规"), DescriptionAttribute("分支后是否重新计数偏移")]
		public bool is_reset { get; set; } = false;//
		[CategoryAttribute("常规"), DescriptionAttribute("分支的值和对应的协议名")]
		public Dictionary<int, string> protname_map { get; set; } = new Dictionary<int, string>(); //各协议描述符头部，由int对协议名进行索引

		public PD_Switch_PropDis(PD_Switch v) : base(v)
		{
			is_hex = v.cfg_str_type=="hex";
			ref_prot = v.ref_type;
			is_reset = v.is_reset != 0;
			foreach (var item in v.protname_map)
			{
				protname_map[item.Key]=item.Value;
			}
		}
		public override void display_2_var()
		{
			base.display_2_var();
			var bv = backend_var as PD_Switch;
			bv.cfg_str_type=is_hex?"hex":"";
			bv.ref_type = ref_prot;
			bv.is_reset = is_reset ? 1 : 0;
			bv.protname_map.Clear();
			foreach (var item in protname_map)
			{
				bv.protname_map[item.Key] = item.Value;
			}
		}
	}
	public class PD_Loop_PropDis : PD_Obj_PropDis //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		[CategoryAttribute("常规"), DescriptionAttribute("用于确定重复次数的引用协议域")]
		public string ref_len { get; set; } = ""; //用于确定重复次数的引用协议域
		[CategoryAttribute("常规"), DescriptionAttribute("直接指定重复次数")]
		public int loop_n { get; set; } = 0; //直接指定重复次数
		public PD_Loop_PropDis(PD_Loop v) : base(v)
		{
			ref_len = v.ref_len; 
			if(v.ref_len=="") loop_n = (v.ref_para as ParaValue_Val).data.ds32;
		}
		public override void display_2_var()
		{
			base.display_2_var();
			var bv = backend_var as PD_Loop;
			bv.ref_len = ref_len;
			(bv.ref_para as ParaValue_Val).data.ds32 = loop_n;
		}
	}
	public class PD_LineSwitch_PropDis : PD_Obj_PropDis //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		[CategoryAttribute("常规"), DescriptionAttribute("编码格式")]
		public Encoding cur_encoding { get; set; } = Encoding.UTF8; //
		public PD_LineSwitch_PropDis(PD_LineSwitch v) : base(v)
		{
			cur_encoding = v.cur_encoding;
		}
		public override void display_2_var()
		{
			base.display_2_var();
			var bv = backend_var as PD_LineSwitch;
			bv.cur_encoding=cur_encoding;
		}
	}

	public class PD_LineObj_PropDis : PD_Obj_PropDis //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		[CategoryAttribute("常规"), DescriptionAttribute("协议头")]
		public string head { get; set; } = ""; //
		[CategoryAttribute("常规"), DescriptionAttribute("列数")]
		public int col_n { get; set; } = 0; //列数
		[CategoryAttribute("常规"), DescriptionAttribute("分隔符列表")]
		public string[] split_char_list { get; set; } = new string[0]; //
		public PD_LineObj_PropDis(PD_LineObj v) : base(v)
		{
			head = v.head; col_n = v.col_n; split_char_list=v.split_char_list;
		}
		public override void display_2_var()
		{
			base.display_2_var();
			var bv = backend_var as PD_LineObj;
			bv.head = head; bv.col_n = col_n; bv.split_char_list=split_char_list;
		}
	}
#endregion
}
